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
        /// Sends chat message string to Python for TF-IDF + Random Forest NLP spam/phishing evaluation.
        /// </summary>
        public async Task<bool> ScanChatMessageAsync(string messagePayload)
        {
            try
            {
                var requestData = new { message = messagePayload };
                var response = await _httpClient.PostAsJsonAsync("api/ai/scan-chat", requestData);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ChatScanResult>();
                    return result?.IsMalicious ?? false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AI NLP Service Unreachable: {ex.Message}");
            }

            return false;
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
        public string Reason { get; set; } = string.Empty;
    }
}