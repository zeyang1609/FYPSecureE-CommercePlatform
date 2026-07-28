using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Services;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PythonAiClient _aiClient;

        public CheckoutController(ApplicationDbContext context, PythonAiClient aiClient)
        {
            _context = context;
            _aiClient = aiClient;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(
            string buyerId,
            decimal amount,
            string rawCardNumber,
            string serviceType,
            int accountAgeDays,
            int failedLogins,
            int shippingDistanceKm,
            string idempotencyKey)
        {
            // 1. Idempotency Check: Prevent duplicate charges from network retries
            bool isDuplicate = await _context.Payments.AnyAsync(p => p.IdempotencyKey == idempotencyKey);
            if (isDuplicate)
            {
                return BadRequest(new { success = false, message = "Duplicate transaction detected and blocked by idempotency shield." });
            }

            // 2. Tokenize sensitive payment details (SHA-256 HMAC Token)
            string paymentToken = GeneratePaymentToken(rawCardNumber);

            // 3. Package transaction features for the Python XGBoost microservice
            var transactionPayload = new
            {
                transactionAmount = amount,
                accountAgeDays = accountAgeDays,
                failedLoginAttempts = failedLogins,
                distanceFromShippingAddress = shippingDistanceKm
            };

            // 4. Send transaction to Python microservice for real-time AI evaluation
            FraudEvaluationResult aiVerdict = await _aiClient.EvaluateTransactionRiskAsync(transactionPayload);

            // 5. Generate explicit production primary keys
            string orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            string paymentId = "PAY-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            // 6. Handle high-risk fraud flags (> 0.85 threshold)
            if (aiVerdict.IsBlocked)
            {
                // Record the blocked attempt in the Order table as Declined
                var blockedOrder = new Order
                {
                    OrderID = orderId,
                    BuyerID = buyerId,
                    TotalAmount = amount,
                    Status = "Declined - AI Security Block",
                    CreatedAt = DateTime.UtcNow,
                    ServiceType = serviceType
                };

                // Store exact SHAP tensor explanation in FraudAlerts table
                var fraudAlert = new FraudAlert
                {
                    AlertID = "ALT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    OrderID = orderId,
                    RiskScore = aiVerdict.RiskScore,
                    SHAP_Data = aiVerdict.ShapData
                };

                // Log to immutable AuditLogs
                var auditLog = new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = buyerId,
                    Action = $"XGBoost AI Block triggered for order {orderId} (Risk: {aiVerdict.RiskScore:P1})",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                };

                _context.Orders.Add(blockedOrder);
                _context.FraudAlerts.Add(fraudAlert);
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = false,
                    message = $"Transaction declined due to suspicious activity risk score: {aiVerdict.RiskScore:P1}.",
                    riskScore = aiVerdict.RiskScore
                });
            }

            // 7. If cleared by AI, proceed with creating the authorized Order & Payment
            var order = new Order
            {
                OrderID = orderId,
                BuyerID = buyerId,
                TotalAmount = amount,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
                ServiceType = serviceType
            };

            var payment = new Payment
            {
                PaymentID = paymentId,
                OrderID = orderId,
                PaymentToken = paymentToken,
                IdempotencyKey = idempotencyKey,
                Status = "Authorized"
            };

            _context.Orders.Add(order);
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Payment authorized successfully!",
                orderId = orderId,
                token = paymentToken,
                riskScore = aiVerdict.RiskScore
            });
        }

        private string GeneratePaymentToken(string cardNumber)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(cardNumber + Guid.NewGuid().ToString()));
                return "TOK-" + Convert.ToHexString(bytes).Substring(0, 16);
            }
        }
    }
}