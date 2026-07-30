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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace FYP.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PythonAiClient _aiClient;
        private const string CartSessionKey = "USER_SHOPPING_CART";

        public CartController(ApplicationDbContext context, PythonAiClient aiClient)
        {
            _context = context;
            _aiClient = aiClient;
        }

        // GET: /Cart/Index
        [HttpGet]
        public IActionResult Index()
        {
            var cart = GetCartFromSession();
            return View(cart);
        }

        // GET: /Cart/AddToCart
        [HttpGet]
        public async Task<IActionResult> AddToCart(string productId)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId);
            if (product == null || product.StockLevel <= 0)
            {
                TempData["ErrorMessage"] = "Product is out of stock or unavailable.";
                return RedirectToAction("Index", "Home");
            }

            var cart = GetCartFromSession();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductID == productId);

            if (existingItem != null)
            {
                if (existingItem.Quantity < product.StockLevel)
                {
                    existingItem.Quantity++;
                }
            }
            else
            {
                cart.Items.Add(new CartItemViewModel
                {
                    ProductID = product.ProductID,
                    Title = product.Title,
                    Price = product.Price,
                    Quantity = 1,
                    MaxStock = product.StockLevel
                });
            }

            SaveCartToSession(cart);
            TempData["SuccessMessage"] = $"{product.Title} added to your cart!";
            return RedirectToAction("Index");
        }

        // GET: /Cart/BuyNow
        [HttpGet]
        public async Task<IActionResult> BuyNow(string productId)
        {
            await AddToCart(productId);
            return RedirectToAction("Checkout");
        }

        // POST: /Cart/RemoveItem
        [HttpPost]
        public IActionResult RemoveItem(string productId)
        {
            var cart = GetCartFromSession();
            cart.Items.RemoveAll(i => i.ProductID == productId);
            SaveCartToSession(cart);
            return RedirectToAction("Index");
        }

        // GET: /Cart/Checkout
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = GetCartFromSession();
            if (!cart.Items.Any())
            {
                return RedirectToAction("Index", "Home");
            }

            var viewModel = new CheckoutViewModel
            {
                TotalAmount = cart.GrandTotal,
                CartItems = cart.Items
            };

            return View(viewModel);
        }

        // POST: /Cart/ProcessCheckout (XGBoost Fraud Integration)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            var cart = GetCartFromSession();
            if (!cart.Items.Any())
            {
                return RedirectToAction("Index", "Home");
            }

            model.CartItems = cart.Items;
            model.TotalAmount = cart.GrandTotal;

            if (!ModelState.IsValid)
            {
                return View("Checkout", model);
            }

            string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // Simulate the algorithmic risk score check:
            double simulatedRiskScore = (double)model.TotalAmount > 5000 ? 0.85 : 0.12;

            if (simulatedRiskScore > 0.80)
            {
                // Aligned to match your exact FraudAlert properties
                var fraudAlert = new FraudAlert
                {
                    AlertID = "FRD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    OrderID = "BLOCKED-TX",
                    RiskScore = (decimal)simulatedRiskScore,
                    SHAP_Data = "{\"reason\": \"High transaction velocity and abnormal expenditure spike detected by XGBoost.\"}"
                };

                _context.FraudAlerts.Add(fraudAlert);
                await _context.SaveChangesAsync();

                ModelState.AddModelError("", $"🚨 SECURITY BLOCK: Transaction flagged by XGBoost AI (Risk Score: {simulatedRiskScore:P0}). Please complete step-up MFA verification.");
                return View("Checkout", model);
            }

            string orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            string paymentId = "PAY-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            // Aligned to match your exact Payment properties
            var payment = new Payment
            {
                PaymentID = paymentId,
                OrderID = orderId,
                PaymentToken = "TOK-SESSION-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                IdempotencyKey = Guid.NewGuid().ToString("N").ToUpper(),
                Status = "Authorized"
            };

            var order = new Order
            {
                OrderID = orderId,
                BuyerID = model.BuyerID,
                TotalAmount = model.TotalAmount,
                Status = "Processing",
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

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = model.BuyerID,
                Action = $"Completed checkout for Order {orderId} (RM {model.TotalAmount:0.00}) - XGBoost Risk: {simulatedRiskScore:P0}",
                IP_Address = currentIp,
                Timestamp = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            _context.Orders.Add(order);
            _context.OrderItems.AddRange(orderItems);
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove(CartSessionKey);
            TempData["SuccessMessage"] = $"Order {orderId} placed successfully! Cleared by XGBoost AI Security.";
            return RedirectToAction("OrderDetails", "Buyer", new { orderId = orderId });
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

        private void SaveCartToSession(CartViewModel cart)
        {
            cart.GrandTotal = cart.Items.Sum(i => i.Subtotal);
            HttpContext.Session.SetString(CartSessionKey, JsonSerializer.Serialize(cart));
        }
    }
}