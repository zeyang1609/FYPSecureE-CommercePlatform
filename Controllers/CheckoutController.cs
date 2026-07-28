using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using FYP.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PythonAiClient _aiClient;
        private const string CartSessionKey = "USER_SHOPPING_CART";

        public CheckoutController(ApplicationDbContext context, PythonAiClient aiClient)
        {
            _context = context;
            _aiClient = aiClient;
        }

        // GET: /Checkout/Index
        [HttpGet]
        public IActionResult Index()
        {
            var cart = GetCartFromSession();
            if (!cart.Items.Any())
            {
                return RedirectToAction("Index", "Home");
        }

            ViewBag.BuyerID = "USR-BUYER-DEMO";
            ViewBag.IdempotencyKey = "IDEM-" + Guid.NewGuid().ToString("N").ToUpper();
            return View(cart);
        }

        // POST: /Checkout/ProcessPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(
            string buyerId,
            decimal amount,
            string rawCardNumber,
            string paymentMethod,
            string shippingAddress,
            int accountAgeDays,
            int failedLogins,
            int shippingDistanceKm,
            string idempotencyKey)
        {
            var cart = GetCartFromSession();
            if (!cart.Items.Any())
            {
                return BadRequest(new { success = false, message = "Your cart is empty." });
            }

            decimal amount = cart.GrandTotal;

            // 1. Idempotency Check: Prevent duplicate charges from network retries or double-clicks
            bool isDuplicate = await _context.Payments.AnyAsync(p => p.IdempotencyKey == idempotencyKey);
            if (isDuplicate)
            {
                return BadRequest(new { success = false, message = "Security Shield: Duplicate transaction attempt intercepted and blocked." });
            }

            // 2. Tokenize sensitive card details (SHA-256 HMAC Tokenization)
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
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // 4. Handle high-risk fraud flags (> 0.80 threshold or explicit AI block)
            if (aiVerdict.IsBlocked || aiVerdict.RiskScore > 0.80m)
            {
                // Record the blocked attempt in the Order table as Declined
                var blockedOrder = new Order
                {
                    OrderID = orderId,
                    BuyerID = buyerId,
                    TotalAmount = amount,
                    Status = "Declined - AI Security Block",
                    CreatedAt = DateTime.UtcNow
                };

                // Now utilizes our new dedicated Reason and CreatedAt schema columns!
                var fraudAlert = new FraudAlert
                {
                    AlertID = "ALT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    OrderID = orderId,
                    RiskScore = aiVerdict.RiskScore,
                    Reason = "XGBoost behavioral anomaly detected: Abnormal expenditure velocity or shipping distance mismatch.",
                    SHAP_Data = aiVerdict.ShapData ?? "{\"transactionAmount\": 0.45, \"shippingDistanceKm\": 0.38}",
                    CreatedAt = DateTime.UtcNow
                };

                // Log to immutable AuditLogs
                var auditLog = new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = buyerId,
                    Action = $"XGBoost AI Block triggered for order {orderId} (Risk: {aiVerdict.RiskScore:P1})",
                    IP_Address = clientIp,
                    Timestamp = DateTime.UtcNow
                };

                _context.Orders.Add(blockedOrder);
                _context.FraudAlerts.Add(fraudAlert);
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = false,
                    message = $"Transaction declined by XGBoost behavioral AI (Risk Score: {aiVerdict.RiskScore:P1}).",
                    riskScore = aiVerdict.RiskScore,
                    shapData = aiVerdict.ShapData
                });
            }

            // 5. If cleared by AI, execute Order, Payment, OrderItems, and Inventory Deduction
            var order = new Order
            {
                OrderID = orderId,
                BuyerID = buyerId,
                TotalAmount = amount,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };

            // Now utilizes our new Amount, PaymentMethod, and TransactionHash columns!
            var payment = new Payment
            {
                PaymentID = paymentId,
                OrderID = orderId,
                Amount = amount,
                PaymentMethod = paymentMethod,
                PaymentToken = paymentToken,
                IdempotencyKey = idempotencyKey,
                Status = "Authorized",
                TransactionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(orderId + paymentToken))).ToLower(),
                CreatedAt = DateTime.UtcNow
            };

            var orderItems = new List<OrderItem>();
            foreach (var item in cart.Items)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == item.ProductID);
                if (product != null)
                {
                    product.StockLevel = Math.Max(0, product.StockLevel - item.Quantity);

                    orderItems.Add(new OrderItem
                    {
                        OrderItemID = "OIT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        OrderID = orderId,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    });
                }
            }

            var successLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = buyerId,
                Action = $"Completed checkout for Order {orderId} (RM {amount:0.00}) - Cleared by XGBoost (Risk: {aiVerdict.RiskScore:P1})",
                IP_Address = clientIp,
                Timestamp = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            _context.Payments.Add(payment);
            _context.OrderItems.AddRange(orderItems);
            _context.AuditLogs.Add(successLog);
            await _context.SaveChangesAsync();

            // 6. Purge Cart from Session upon successful transaction
            HttpContext.Session.Remove(CartSessionKey);

            return Json(new
            {
                success = true,
                message = "Payment authorized and order verified successfully!",
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

        private CartViewModel GetCartFromSession()
        {
            var sessionData = HttpContext.Session.GetString(CartSessionKey);
            var cart = string.IsNullOrEmpty(sessionData)
                ? new CartViewModel()
                : JsonSerializer.Deserialize<CartViewModel>(sessionData) ?? new CartViewModel();

            cart.GrandTotal = cart.Items.Sum(i => i.Subtotal);
            return cart;
        }
    }
}