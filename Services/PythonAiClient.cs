using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace FYP.Services
{
    public class PythonAiClient
    {
        private readonly HttpClient _httpClient;

        public PythonAiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:5000/");
        }

        /// <summary>
        /// Sends checkout transaction features to the Python XGBoost microservice for risk scoring.
        /// </summary>
        public async Task<FraudEvaluationResult> EvaluateTransactionRiskAsync(object payload)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/ai/evaluate-risk", payload);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<FraudEvaluationResult>()
                           ?? new FraudEvaluationResult();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Fraud Service Unreachable: {ex.Message}");
            }

            // Fail open / default response if microservice is offline
            return new FraudEvaluationResult { RiskScore = 0.0m, IsBlocked = false, ShapData = "{}" };
        }

        /// <summary>
        /// Sends image byte payload to Python for OpenCV ELA and NSFW neural network evaluation.
        /// </summary>
        public async Task<ImageScanResult> ScanImageForForgeryAsync(byte[] imageBytes)
        {
            try
            {
                using var content = new ByteArrayContent(imageBytes);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                var response = await _httpClient.PostAsync("api/ai/scan-image", content);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ImageScanResult>()
                           ?? new ImageScanResult();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Vision Service Unreachable: {ex.Message}");
            }

            return new ImageScanResult { IsForgeryDetected = false, ForgeryReason = "Service offline - passed by default." };
        }

        /// <summary>
        /// Sends chat message string to Python for multilingual NLP spam/phishing evaluation.
        /// Returns full scan result with block status and reason.
        /// </summary>
        public async Task<ChatScanResult> ScanChatMessageAsync(string messagePayload)
        {
            try
            {
                var requestData = new { message = messagePayload };
                var response = await _httpClient.PostAsJsonAsync("api/ai/scan-chat", requestData);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ChatScanResult>()
                           ?? new ChatScanResult();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI NLP Service Unreachable: {ex.Message}");
            }

            return new ChatScanResult { IsMalicious = false, IsBlocked = false, Reason = "NLP service offline — message allowed." };
        }

        /// <summary>
        /// Sends buyer history and candidate products to Python for content-based recommendations.
        /// </summary>
        public async Task<List<string>> GetRecommendationsAsync(object payload)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/ai/recommend-products", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RecommendationResult>();
                    return result?.RecommendedIds ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Recommendation Service Unreachable: {ex.Message}");
            }

            return new List<string>();
        }

        /// <summary>
        /// Sends sales history to Python for AI demand forecasting (Linear Regression).
        /// </summary>
        public async Task<List<DemandForecastItem>> ForecastDemandAsync(object payload)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/ai/forecast-demand", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<DemandForecastResult>();
                    return result?.Forecasts ?? new List<DemandForecastItem>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI Forecasting Service Unreachable: {ex.Message}");
            }

            return new List<DemandForecastItem>();
        }
    }

    // --- Data Transfer Objects (DTOs) ---

    public class FraudEvaluationResult
    {
        public decimal RiskScore { get; set; }
        public bool IsBlocked { get; set; }
        public string ShapData { get; set; } = "{}";
    }

    public class ImageScanResult
    {
        public bool IsForgeryDetected { get; set; }
        public string ForgeryReason { get; set; } = string.Empty;
    }

    public class ChatScanResult
    {
        public bool IsMalicious { get; set; }
        public bool IsBlocked { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class RecommendationResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("recommended_ids")]
        public List<string> RecommendedIds { get; set; } = new List<string>();
    }

    public class DemandForecastResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("forecasts")]
        public List<DemandForecastItem> Forecasts { get; set; } = new List<DemandForecastItem>();
    }

    public class DemandForecastItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string ProductId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("predicted_7_day_sales")]
        public int Predicted7DaySales { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("restock_needed")]
        public bool RestockNeeded { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("restock_amount")]
        public int RestockAmount { get; set; }
    }
}