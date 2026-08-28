using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using FYP.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Stripe;
using Microsoft.AspNetCore.SignalR;
using FYP.Hubs;

namespace FYP.Controllers
{
    [Authorize(Roles = "Buyer")]
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PythonAiClient _aiClient;
        private readonly IConfiguration _configuration;
        private readonly IShippingService _shippingService;
        private readonly IHubContext<OrderHub> _orderHubContext;
        private readonly FYP.Services.IOtpService _otpService;
        private readonly FYP.Services.TotpService _totpService;
        private readonly FYP.Services.IPaymentSecurityService _paymentSecurityService;
        private readonly ICheckoutLockService _checkoutLockService;
        private readonly IPaymentEncryptionService _paymentEncryptionService;

        public CartController(
            ApplicationDbContext context,
            PythonAiClient aiClient,
            IConfiguration configuration,
            IShippingService shippingService,
            IHubContext<OrderHub> orderHubContext,
            FYP.Services.IOtpService otpService,
            FYP.Services.TotpService totpService,
            FYP.Services.IPaymentSecurityService paymentSecurityService,
            ICheckoutLockService checkoutLockService,
            IPaymentEncryptionService paymentEncryptionService)
        {
            _context = context;
            _aiClient = aiClient;
            _configuration = configuration;
            _shippingService = shippingService;
            _orderHubContext = orderHubContext;
            _otpService = otpService;
            _totpService = totpService;
            _paymentSecurityService = paymentSecurityService;
            _checkoutLockService = checkoutLockService;
            _paymentEncryptionService = paymentEncryptionService;
        }

        private string GetUserId()
        {
            return User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        }

        private async Task<Cart> GetOrCreateCartForUserAsync()
        {
            var userId = GetUserId();
            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserID == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserID = userId
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }

        private async Task BroadcastNewOrderToSellersAsync(string orderId)
        {
            var sellerIds = await _context.OrderItems
                .Where(oi => oi.OrderID == orderId)
                .Select(oi => oi.Product.SellerID)
                .Distinct()
                .ToListAsync();

            foreach (var sellerId in sellerIds)
            {
                if (!string.IsNullOrEmpty(sellerId))
                {
                    await _orderHubContext.Clients.Group(sellerId).SendAsync("NewOrderToShip", orderId, $"You have a new order ({orderId}) pending shipment!");
                }
            }
        }

        private async Task<CartViewModel> MapCartToViewModelAsync(Cart cart)
        {
            var viewModel = new CartViewModel
            {
                Items = cart.Items.Select(i => new CartItemViewModel
                {
                    ProductID = i.ProductID,
                    Title = i.Product?.Title ?? "Unknown Product",
                    Price = i.Product?.Price ?? 0,
                    Quantity = i.Quantity,
                    MaxStock = i.Product?.StockLevel ?? 0,
                    IsSelected = i.IsSelected
                }).ToList()
            };

            viewModel.GrandTotal = viewModel.Items.Where(i => i.IsSelected).Sum(i => i.Subtotal);
            viewModel.SelectedItemsCount = viewModel.Items.Where(i => i.IsSelected).Sum(i => i.Quantity);

            var defaultAddress = await _context.Addresses.FirstOrDefaultAsync(a => a.UserID == cart.UserID && a.IsDefault);
            string addressString = defaultAddress != null ? $"{defaultAddress.HouseBuildingStreet}, {defaultAddress.StateArea} {defaultAddress.PostalCode}" : "Kuala Lumpur, 50000";

            var shippingItems = viewModel.Items.Where(i => i.IsSelected).Select(i => (i.ProductID, i.Quantity)).ToList();
            if (shippingItems.Any())
            {
                var (originalFee, finalFee, courier) = await _shippingService.CalculateAndAssignShippingAsync(shippingItems, viewModel.GrandTotal, addressString);
                viewModel.OriginalShippingFee = originalFee;
                viewModel.FinalShippingFee = finalFee;
            }

            return viewModel;
        }

        // GET: /Cart/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = await GetOrCreateCartForUserAsync();
            bool selectionsCleared = false;
            foreach (var item in cart.Items)
            {
                if (item.IsSelected)
                {
                    item.IsSelected = false;
                    selectionsCleared = true;
                }
            }
            if (selectionsCleared)
            {
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            var viewModel = await MapCartToViewModelAsync(cart);
            return View(viewModel);
        }

        // POST: /Cart/AddToCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(string productId, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId);
            if (product == null || product.StockLevel <= 0)
            {
                TempData["ErrorMessage"] = "Product is out of stock or unavailable.";
                return RedirectToAction("Index", "Home");
            }

            var cart = await GetOrCreateCartForUserAsync();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductID == productId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity <= product.StockLevel)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    existingItem.Quantity = product.StockLevel;
                }
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductID = product.ProductID,
                    Quantity = Math.Min(quantity, product.StockLevel)
                });
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{product.Title} added to your cart!";
            return RedirectToAction("Index");
        }

        // GET: /Cart/GetCartPreviewHtml
        [HttpGet]
        public IActionResult GetCartPreviewHtml()
        {
            return ViewComponent("CartPreview");
        }

        // POST: /Cart/AddToCartAjax
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCartAjax(string productId, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId);
            if (product == null || product.StockLevel <= 0)
            {
                return Json(new { success = false, message = "Product is out of stock or unavailable." });
            }

            var cart = await GetOrCreateCartForUserAsync();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductID == productId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity <= product.StockLevel)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    existingItem.Quantity = product.StockLevel;
                }
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductID = product.ProductID,
                    Quantity = Math.Min(quantity, product.StockLevel)
                });
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Item has been added to your shopping cart" });
        }

        // POST: /Cart/UpdateQuantityAjax
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantityAjax(string productId, int newQuantity)
        {
            if (newQuantity < 1) newQuantity = 1;

            var cart = await GetOrCreateCartForUserAsync();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductID == productId);

            if (existingItem != null)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId);
                if (product != null)
                {
                    existingItem.Quantity = Math.Min(newQuantity, product.StockLevel);
                    cart.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    var cartViewModel = await MapCartToViewModelAsync(cart);
                    var updatedItem = cartViewModel.Items.First(i => i.ProductID == productId);

                    return Json(new { 
                        success = true, 
                        newQuantity = existingItem.Quantity,
                        itemSubtotal = updatedItem.Subtotal,
                        grandTotal = cartViewModel.GrandTotal,
                        selectedCount = cartViewModel.SelectedItemsCount,
                        originalShipping = cartViewModel.OriginalShippingFee,
                        finalShipping = cartViewModel.FinalShippingFee
                    });
                }
            }

            return Json(new { success = false });
        }

        // POST: /Cart/UpdateItemSelectionAjax
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateItemSelectionAjax(string productId, bool isSelected)
        {
            var cart = await GetOrCreateCartForUserAsync();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductID == productId);
            if (existingItem != null)
            {
                existingItem.IsSelected = isSelected;
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                
                var cartViewModel = await MapCartToViewModelAsync(cart);
                return Json(new { success = true, grandTotal = cartViewModel.GrandTotal, selectedCount = cartViewModel.SelectedItemsCount, originalShipping = cartViewModel.OriginalShippingFee, finalShipping = cartViewModel.FinalShippingFee });
            }
            return Json(new { success = false });
        }

        // POST: /Cart/SelectAllAjax
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectAllAjax(bool isSelected)
        {
            var cart = await GetOrCreateCartForUserAsync();
            foreach (var item in cart.Items)
            {
                item.IsSelected = isSelected;
            }
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            var cartViewModel = await MapCartToViewModelAsync(cart);
            return Json(new { success = true, grandTotal = cartViewModel.GrandTotal, selectedCount = cartViewModel.SelectedItemsCount, originalShipping = cartViewModel.OriginalShippingFee, finalShipping = cartViewModel.FinalShippingFee });
        }

        // POST: /Cart/BuyNow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyNow(string productId, int quantity = 1)
        {
            if (quantity < 1) quantity = 1;

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId);
            if (product == null || product.StockLevel <= 0)
            {
                TempData["ErrorMessage"] = "Product is out of stock or unavailable.";
                return RedirectToAction("Index", "Home");
            }

            var cart = await GetOrCreateCartForUserAsync();
            var existingItem = cart.Items.FirstOrDefault(i => i.ProductID == productId);

            if (existingItem != null)
            {
                existingItem.Quantity = Math.Min(quantity, product.StockLevel);
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductID = product.ProductID,
                    Quantity = Math.Min(quantity, product.StockLevel)
                });
            }

            // Deselect all other items, select only this one
            foreach (var item in cart.Items)
            {
                item.IsSelected = (item.ProductID == productId);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction("Checkout");
        }

        // POST: /Cart/RemoveItem
        [HttpPost]
        public async Task<IActionResult> RemoveItem(string productId)
        {
            var cart = await GetOrCreateCartForUserAsync();
            var itemToRemove = cart.Items.FirstOrDefault(i => i.ProductID == productId);
            if (itemToRemove != null)
            {
                _context.CartItems.Remove(itemToRemove);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            
            return RedirectToAction("Index");
        }

        // GET: /Cart/Checkout
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = await GetOrCreateCartForUserAsync();
            if (!cart.Items.Any(i => i.IsSelected))
            {
                TempData["ErrorMessage"] = "Please select at least one item to checkout.";
                return RedirectToAction("Index");
            }

            var checkoutItems = cart.Items.Where(i => i.IsSelected).ToList();
            var cartViewModel = await MapCartToViewModelAsync(cart);
            
            var userAddresses = await _context.Addresses.Where(a => a.UserID == GetUserId()).ToListAsync();
            var defaultAddress = userAddresses.FirstOrDefault(a => a.IsDefault) ?? userAddresses.FirstOrDefault();
            var savedCards = await _context.SavedBankCards.Where(c => c.UserID == GetUserId()).ToListAsync();

            var viewModel = new CheckoutViewModel
            {
                BuyerID = GetUserId(),
                TotalAmount = cartViewModel.GrandTotal,
                CartItems = cartViewModel.Items.Where(i => i.IsSelected).ToList(),
                AvailableAddresses = userAddresses,
                SelectedAddressID = defaultAddress?.AddressID ?? 0,
                SavedCards = savedCards,
                SecurityToken = _paymentSecurityService.GeneratePaymentToken(GetUserId())
            };

            if (defaultAddress != null)
            {
                var addressString = $"{defaultAddress.HouseBuildingStreet}, {defaultAddress.StateArea} {defaultAddress.PostalCode}";
                var shippingItems = checkoutItems.Select(i => (i.ProductID, i.Quantity));
                var (originalFee, fee, courier) = await _shippingService.CalculateAndAssignShippingAsync(shippingItems, cartViewModel.GrandTotal, addressString);
                
                viewModel.OriginalShippingFee = originalFee;
                viewModel.ShippingFee = fee;
                viewModel.CourierName = courier?.Name ?? "Standard Courier";
                viewModel.CourierID = courier?.CourierID ?? "COUR_JNT";
                viewModel.TotalAmount += fee;
            }

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult GeneratePaymentToken()
        {
            var token = _paymentSecurityService.GeneratePaymentToken(GetUserId());
            return Json(new { token = token });
        }

        // POST: /Cart/ProcessCheckout (XGBoost Fraud Integration)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            var userId = GetUserId();
            if (!await _checkoutLockService.AcquireLockAsync(userId, TimeSpan.Zero))
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = userId,
                    Action = "DUPLICATE TRANSACTION BLOCKED: Concurrent checkout request rejected by per-user lock (ProcessCheckout)",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = "A checkout is already processing. Please wait.";
                return RedirectToAction("Checkout");
            }

            try
            {
                if (!_paymentSecurityService.ValidatePaymentToken(model.SecurityToken ?? "", GetUserId(), out string validatedNonce))
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = userId,
                        Action = "REPLAY ATTACK BLOCKED: Reused or expired security token rejected during form checkout (ProcessCheckout)",
                        IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    TempData["ErrorMessage"] = "Session expired or invalid. Please try checking out again.";
                    return RedirectToAction("Checkout");
                }

            var cart = await GetOrCreateCartForUserAsync();
            var cartViewModel = await MapCartToViewModelAsync(cart);
            
            if (!cart.Items.Any())
            {
                return RedirectToAction("Index", "Home");
            }

            // SERVER-SIDE CALCULATION: Recalculate Shipping Fee dynamically to prevent tampering
            decimal finalShippingFee = 0;
            string finalCourierId = "COUR_JNT";
            var userAddressesForShipping = await _context.Addresses.Where(a => a.UserID == GetUserId()).ToListAsync();
            var checkoutAddress = userAddressesForShipping.FirstOrDefault(a => a.AddressID == model.SelectedAddressID)
                                ?? userAddressesForShipping.FirstOrDefault(a => a.IsDefault);
                                
            if (checkoutAddress != null)
            {
                var addressString = $"{checkoutAddress.HouseBuildingStreet}, {checkoutAddress.StateArea} {checkoutAddress.PostalCode}";
                var shippingItemsForCalc = cart.Items.Where(i => i.IsSelected).Select(i => (i.ProductID, i.Quantity));
                var (originalFee, fee, courier) = await _shippingService.CalculateAndAssignShippingAsync(shippingItemsForCalc, cartViewModel.GrandTotal, addressString);
                finalShippingFee = fee;
                if (courier != null) finalCourierId = courier.CourierID;
                
                model.OriginalShippingFee = originalFee;
                model.ShippingFee = fee;
                model.CourierName = courier?.Name ?? "Standard Courier";
                model.CourierID = finalCourierId;
                
                ModelState.Remove("CourierName");
                ModelState.Remove("CourierID");
                ModelState.Remove("OriginalShippingFee");
                ModelState.Remove("ShippingFee");
            }

            model.CartItems = cartViewModel.Items.Where(i => i.IsSelected).ToList();
            model.TotalAmount = cartViewModel.GrandTotal + finalShippingFee; // Secure total amount

            if (model.PaymentMethod == "Credit Card")
            {
                if (string.IsNullOrEmpty(model.SelectedSavedCardID))
                {
                    // No saved card selected, validate that new card fields are provided
                    if (string.IsNullOrWhiteSpace(model.RawCardNumber) || 
                        string.IsNullOrWhiteSpace(model.ExpiryDate) || 
                        string.IsNullOrWhiteSpace(model.CVV))
                    {
                        ModelState.AddModelError("SelectedSavedCardID", "Please select a saved card or enter new card details to proceed with Credit/Debit Card payment.");
                    }
                }
                else
                {
                    // Saved card selected, remove validation for new card fields
                    ModelState.Remove("RawCardNumber");
                    ModelState.Remove("CardNumber");
                    ModelState.Remove("ExpiryDate");
                    ModelState.Remove("CVV");
                }
            }
            else
            {
                // Non-credit card payment method, ignore card fields
                ModelState.Remove("RawCardNumber");
                ModelState.Remove("CardNumber");
                ModelState.Remove("ExpiryDate");
                ModelState.Remove("CVV");
            }

            if (!ModelState.IsValid)
            {
                // Reload necessary data for the view
                model.AvailableAddresses = await _context.Addresses.Where(a => a.UserID == GetUserId()).ToListAsync();
                model.SavedCards = await _context.SavedBankCards.Where(c => c.UserID == GetUserId()).ToListAsync();
                return View("Checkout", model);
            }

            string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            // Calculate real-time velocity metrics for the AI
            var tenMinsAgo = DateTime.UtcNow.AddMinutes(-10);
            int transactionsLast10Mins = await _context.Orders
                .CountAsync(o => o.BuyerID == userId && o.CreatedAt >= tenMinsAgo);

            int deviceIpFlags = await _context.AuditLogs
                .CountAsync(a => a.IP_Address == currentIp && a.Action.Contains("Block"));

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            double timeSinceAccountCreationSeconds = buyer != null 
                ? (DateTime.UtcNow - buyer.CreatedAt).TotalSeconds 
                : 86400;

            // Package transaction features for the Python XGBoost microservice
            var transactionPayload = new
            {
                transactionAmount = model.TotalAmount,
                accountAgeDays = (int)(timeSinceAccountCreationSeconds / 86400),
                failedLoginAttempts = 0,
                distanceFromShippingAddress = 15,
                transactions_last_10_mins = transactionsLast10Mins,
                time_since_account_creation_seconds = timeSinceAccountCreationSeconds,
                device_ip_flags = deviceIpFlags
            };

            // Send transaction to Python microservice for real-time AI evaluation
            FraudEvaluationResult aiVerdict = await _aiClient.EvaluateTransactionRiskAsync(transactionPayload);

            if (TempData["SkipRiskCheck"] != null)
            {
                aiVerdict = new FraudEvaluationResult { RiskScore = 0.0m, IsBlocked = false, ShapData = "{}" };
            }

            string orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            if (aiVerdict.RiskScore > 0.50m && aiVerdict.RiskScore <= 0.80m && !aiVerdict.IsBlocked)
            {
                // Medium Risk: Enforce MFA
                var user = await _context.Users.FindAsync(userId);
                
                // Save model to TempData
                TempData["PendingCheckout"] = System.Text.Json.JsonSerializer.Serialize(model);
                
                if (user != null && !string.IsNullOrEmpty(user.TotpSecret))
                {
                    return RedirectToAction("VerifyCheckoutTotp");
                }
                else
                {
                    if (user != null) {
                        await _otpService.GenerateAndSendOtpAsync(user.Email, "Checkout Verification");
                    }
                    TempData["ResetOtpTimer"] = true;
                    return RedirectToAction("VerifyCheckoutOtp");
                }
            }

            if (aiVerdict.RiskScore > 0.80m || aiVerdict.IsBlocked)
            {
                var blockedOrder = new Order
                {
                    OrderID = orderId,
                    BuyerID = GetUserId(),
                    TotalAmount = model.TotalAmount,
                    Status = FYP.Models.Entities.TransactionStatus.RequiredCheck,
                    CreatedAt = DateTime.UtcNow,
                    ServiceType = "Standard Delivery"
                };

                // Create OrderItems for the blocked order so the admin can review the cart contents
                var blockedCheckoutItems = cart.Items.Where(i => i.IsSelected).ToList();
                var blockedOrderItems = new List<OrderItem>();
                foreach (var item in blockedCheckoutItems)
                {
                    if (item.Product != null)
                    {
                        blockedOrderItems.Add(new OrderItem
                        {
                            OrderItemID = "OIT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            OrderID = orderId,
                            ProductID = item.ProductID,
                            Quantity = item.Quantity,
                            UnitPrice = item.Product.Price
                        });
                    }
                }
                
                // Remove items from the cart
                if (blockedCheckoutItems.Any())
                {
                    _context.CartItems.RemoveRange(blockedCheckoutItems);
                }

                // Aligned to match your exact FraudAlert properties
                var fraudAlert = new FraudAlert
                {
                    AlertID = "FRD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    OrderID = orderId,
                    RiskScore = aiVerdict.RiskScore,
                    Reason = "XGBoost behavioral anomaly detected: High transaction velocity or anomaly pattern.",
                    SHAP_Data = aiVerdict.ShapData ?? "{}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(blockedOrder);
                _context.OrderItems.AddRange(blockedOrderItems);
                _context.FraudAlerts.Add(fraudAlert);

                var admins = _context.Users.Where(u => u.Role == "Admin").ToList();
                foreach (var admin in admins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = admin.UserID,
                        Type = "Security Alert",
                        Content = $"High-Risk Checkout Blocked! Score: {aiVerdict.RiskScore:P1}. Order: {orderId}",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
                
                _context.Notifications.Add(new Notification
                {
                    NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = userId,
                    Type = "Security Alert",
                    Content = "Security Alert: A suspicious transaction attempt was blocked on your account."
                });

                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = $"SECURITY BLOCK: Transaction flagged by XGBoost AI (Risk Score: {aiVerdict.RiskScore:P0}). Your transaction is under review.";
                return RedirectToAction("Index", "Cart");
            }

            string paymentId = "PAY-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            // Aligned to match your exact Payment properties
            var payment = new Payment
            {
                PaymentID = paymentId,
                OrderID = orderId,
                PaymentToken = _paymentEncryptionService.Encrypt("TOK-SESSION-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()),
                IdempotencyKey = _paymentEncryptionService.Encrypt(Guid.NewGuid().ToString("N").ToUpper()),
                PaymentMethod = model.PaymentMethod ?? "Credit Card",
                Status = "Authorized"
            };

            var checkoutItems = cart.Items.Where(i => i.IsSelected).ToList();
            
            // finalShippingFee and finalCourierId were safely calculated above
            decimal finalTotalAmount = model.TotalAmount; // model.TotalAmount is already safely calculated
            payment.Amount = finalTotalAmount;

            var order = new Order
            {
                OrderID = orderId,
                BuyerID = GetUserId(),
                TotalAmount = finalTotalAmount,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow,
                ServiceType = "Standard Delivery" // Will be overridden or ignored in favor of Delivery module
            };

            var orderItems = new List<OrderItem>();
            var notifications = new List<Notification>();
            
            foreach (var item in checkoutItems)
            {
                if (item.Product != null)
                {
                    int oldStock = item.Product.StockLevel;
                    item.Product.StockLevel = Math.Max(0, item.Product.StockLevel - item.Quantity);
                    orderItems.Add(new OrderItem
                    {
                        OrderItemID = "OIT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        OrderID = orderId,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product.Price
                    });

                    // Low Stock Alert for wishlisted buyers
                    if (oldStock > 5 && item.Product.StockLevel <= 5 && item.Product.StockLevel > 0)
                    {
                        var wishlistedBuyerIds = await _context.Wishlists
                            .Where(w => w.ProductID == item.ProductID)
                            .Select(w => w.BuyerID)
                            .Distinct()
                            .ToListAsync();

                        foreach (var wBuyerId in wishlistedBuyerIds)
                        {
                            notifications.Add(new Notification
                            {
                                NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                                UserID = wBuyerId,
                                Type = "Low Stock Alert",
                                Content = $"Hurry! '{item.Product.Title}' in your wishlist is running low on stock (only {item.Product.StockLevel} left)."
                            });
                        }
                    }

                    // Low Stock Alert / Out of Stock Alert for Seller
                    if (item.Product.StockLevel == 0)
                    {
                        notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = item.Product.SellerID,
                            Type = "Inventory Alert",
                            Content = $"Out of Stock Alert! Product '{item.Product.Title}' is now completely out of stock."
                        });
                    }
                    else if (item.Product.StockLevel <= 5)
                    {
                        notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = item.Product.SellerID,
                            Type = "Inventory Alert",
                            Content = $"Low Stock Warning! Product '{item.Product.Title}' has only {item.Product.StockLevel} left."
                        });
                    }
                }
            }

            // New Order Notifications for Sellers
            var sellerIds = checkoutItems.Where(i => i.Product != null).Select(i => i.Product.SellerID).Distinct();
            foreach (var sellerId in sellerIds)
            {
                notifications.Add(new Notification
                {
                    NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = sellerId,
                    Type = "New Order",
                    Content = $"You have received a new order (Order ID: {orderId})."
                });
            }
            
            // Order Confirmation & Payment Success for Buyer
            notifications.Add(new Notification
            {
                NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = userId,
                Type = "Order Confirmation",
                Content = $"Your order {orderId} has been confirmed and payment of RM {order.TotalAmount:0.00} was successful."
            });

            _context.Notifications.AddRange(notifications);
            var delivery = new Delivery
            {
                DeliveryID = "DEL-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                OrderID = orderId,
                CourierID = finalCourierId,
                ShippingFee = finalShippingFee,
                Status = "Pending"
            };

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = GetUserId(),
                Action = $"Completed checkout for Order {orderId} (RM {model.TotalAmount:0.00}) - XGBoost Risk: {aiVerdict.RiskScore:P0}",
                IP_Address = currentIp,
                Timestamp = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            _context.Orders.Add(order);
            _context.OrderItems.AddRange(orderItems);
            _context.Deliveries.Add(delivery);
            _context.AuditLogs.Add(auditLog);
            
            // Clear only checked out items from database
            _context.CartItems.RemoveRange(checkoutItems);
            
            await _context.SaveChangesAsync();
            await BroadcastNewOrderToSellersAsync(orderId);

            TempData["SuccessMessage"] = $"Order {orderId} placed successfully! Cleared by XGBoost AI Security.";
            return RedirectToAction("OrderDetails", "Buyer", new { orderId = orderId });
            }
            finally
            {
                _checkoutLockService.ReleaseLock(userId);
            }
        }

        // POST: /Cart/CreatePaymentIntent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] System.Text.Json.JsonElement requestData)
        {
            var userId = GetUserId();
            if (!await _checkoutLockService.AcquireLockAsync(userId, TimeSpan.Zero))
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = userId,
                    Action = "DUPLICATE TRANSACTION BLOCKED: Concurrent Stripe payment request rejected by per-user lock (CreatePaymentIntent)",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return BadRequest(new { error = "A checkout is already processing. Please wait." });
            }

            try
            {
                string securityToken = requestData.TryGetProperty("securityToken", out var tokenProp) ? tokenProp.GetString() ?? "" : "";
                if (!_paymentSecurityService.ValidatePaymentToken(securityToken, GetUserId(), out string validatedNonce))
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = userId,
                        Action = "REPLAY ATTACK BLOCKED: Reused or expired security token rejected during Stripe payment (CreatePaymentIntent)",
                        IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    return BadRequest(new { error = "Session expired or invalid. Please refresh the page and try again." });
                }

                var cart = await GetOrCreateCartForUserAsync();
                var cartViewModel = await MapCartToViewModelAsync(cart);
                
                if (!cart.Items.Any(i => i.IsSelected))
                {
                    return BadRequest(new { error = "Cart is empty or no items selected." });
                }

                string bankCode = requestData.TryGetProperty("bank", out var bankProp) ? bankProp.GetString() ?? "" : "";
                int addressId = 0;
                if (requestData.TryGetProperty("addressId", out var addrProp))
                {
                    if (addrProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        int.TryParse(addrProp.GetString(), out addressId);
                    }
                    else if (addrProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        addressId = addrProp.GetInt32();
                    }
                }

                var checkoutItems = cart.Items.Where(i => i.IsSelected).ToList();
                decimal totalAmount = cartViewModel.Items.Where(i => i.IsSelected).Sum(i => i.Subtotal);

                decimal finalShippingFee = 0;
                string finalCourierId = "COUR_JNT";
                var defaultAddress = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressID == addressId)
                                    ?? await _context.Addresses.FirstOrDefaultAsync(a => a.UserID == GetUserId() && a.IsDefault);
                if (defaultAddress != null)
                {
                    var addressString = $"{defaultAddress.HouseBuildingStreet}, {defaultAddress.StateArea} {defaultAddress.PostalCode}";
                    var shippingItems = checkoutItems.Select(i => (i.ProductID, i.Quantity));
                    var (originalFee, fee, courier) = await _shippingService.CalculateAndAssignShippingAsync(shippingItems, cartViewModel.GrandTotal, addressString);
                    finalShippingFee = fee;
                    if (courier != null) finalCourierId = courier.CourierID;
                }

                totalAmount += finalShippingFee;

                // --- Inject XGBoost Fraud Evaluation ---
                string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var tenMinsAgo = DateTime.UtcNow.AddMinutes(-10);
                int transactionsLast10Mins = await _context.Orders.CountAsync(o => o.BuyerID == userId && o.CreatedAt >= tenMinsAgo);
                int deviceIpFlags = await _context.AuditLogs.CountAsync(a => a.IP_Address == currentIp && a.Action.Contains("Block"));
                var buyer = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
                double timeSinceAccountCreationSeconds = buyer != null ? (DateTime.UtcNow - buyer.CreatedAt).TotalSeconds : 86400;

                var transactionPayload = new
                {
                    transactionAmount = totalAmount,
                    accountAgeDays = (int)(timeSinceAccountCreationSeconds / 86400),
                    failedLoginAttempts = 0,
                    distanceFromShippingAddress = 15,
                    transactions_last_10_mins = transactionsLast10Mins,
                    time_since_account_creation_seconds = timeSinceAccountCreationSeconds,
                    device_ip_flags = deviceIpFlags
                };

                FraudEvaluationResult aiVerdict = await _aiClient.EvaluateTransactionRiskAsync(transactionPayload);

                if (TempData["SkipRiskCheck"] != null)
                {
                    aiVerdict = new FraudEvaluationResult { RiskScore = 0.0m, IsBlocked = false, ShapData = "{}" };
                }

                if (TempData["SkipRiskCheck"] == null && aiVerdict.RiskScore > 0.50m && aiVerdict.RiskScore <= 0.80m && !aiVerdict.IsBlocked)
                {
                    var user = await _context.Users.FindAsync(userId);
                    var pendingFpx = new
                    {
                        PaymentType = "FPX",
                        Bank = bankCode,
                        AddressId = addressId,
                        SecurityToken = validatedNonce
                    };
                    TempData["PendingCheckout"] = System.Text.Json.JsonSerializer.Serialize(pendingFpx);
                    TempData.Keep("PendingCheckout");

                    string redirectUrl = (user != null && !string.IsNullOrEmpty(user.TotpSecret))
                        ? Url.Action("VerifyCheckoutTotp", "Cart")
                        : Url.Action("VerifyCheckoutOtp", "Cart");

                    if (user != null && string.IsNullOrEmpty(user.TotpSecret))
                    {
                        await _otpService.GenerateAndSendOtpAsync(user.Email, "Checkout Verification");
                        TempData["ResetOtpTimer"] = true;
                    }

                    return Ok(new { 
                        requiresMfa = true, 
                        redirectUrl = redirectUrl 
                    });
                }

                string orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

                if (aiVerdict.RiskScore > 0.80m || aiVerdict.IsBlocked)
                {
                    var blockedOrder = new Order
                    {
                        OrderID = orderId,
                        BuyerID = GetUserId(),
                        TotalAmount = totalAmount,
                        Status = FYP.Models.Entities.TransactionStatus.RequiredCheck,
                        CreatedAt = DateTime.UtcNow,
                        ServiceType = "Standard Delivery"
                    };

                    var blockedOrderItems = checkoutItems.Select(item => new OrderItem
                    {
                        OrderItemID = "OIT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        OrderID = orderId,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product.Price
                    }).ToList();
                    
                    if (checkoutItems.Any()) _context.CartItems.RemoveRange(checkoutItems);

                    var fraudAlert = new FraudAlert
                    {
                        AlertID = "FRD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        OrderID = orderId,
                        RiskScore = aiVerdict.RiskScore,
                        Reason = "XGBoost behavioral anomaly detected (via FPX): High transaction velocity or anomaly pattern.",
                        SHAP_Data = aiVerdict.ShapData ?? "{}",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Orders.Add(blockedOrder);
                    _context.OrderItems.AddRange(blockedOrderItems);
                    _context.FraudAlerts.Add(fraudAlert);

                    var admins = _context.Users.Where(u => u.Role == "Admin").ToList();
                    foreach (var admin in admins)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = admin.UserID,
                            Type = "Security Alert",
                            Content = $"High-Risk Checkout Blocked (FPX)! Score: {aiVerdict.RiskScore:P1}. Order: {orderId}",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        });
                    }
                    _context.Notifications.Add(new Notification
                    {
                        NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = userId,
                        Type = "Security Alert",
                        Content = "Security Alert: A suspicious transaction attempt was blocked on your account."
                    });

                    await _context.SaveChangesAsync();
                    return BadRequest(new { error = $"SECURITY BLOCK: Transaction flagged by XGBoost AI (Risk Score: {aiVerdict.RiskScore:P0}). Your transaction is under review." });
                }
                // --- End XGBoost Fraud Evaluation ---

                StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(totalAmount * 100), // RM to cents
                    Currency = "myr",
                    PaymentMethodTypes = new List<string> { "fpx" },
                };

                var requestOptions = new RequestOptions { IdempotencyKey = validatedNonce };
                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options, requestOptions);

                string serviceType = string.IsNullOrEmpty(bankCode) ? "FPX Online Banking" : $"FPX|{bankCode}";

                var order = new Order
                {
                    OrderID = orderId,
                    BuyerID = GetUserId(),
                    TotalAmount = totalAmount,
                    Status = "Pending Payment",
                    CreatedAt = DateTime.UtcNow,
                    ServiceType = serviceType 
                };
                
                var orderItems = checkoutItems.Select(item => new OrderItem
                {
                    OrderItemID = "OIT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    OrderID = orderId,
                    ProductID = item.ProductID,
                    Quantity = item.Quantity,
                    UnitPrice = item.Product.Price
                }).ToList();

                var delivery = new Delivery
                {
                    DeliveryID = "DEL-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    OrderID = orderId,
                    CourierID = finalCourierId,
                    ShippingFee = finalShippingFee,
                    Status = "Pending"
                };

                _context.Orders.Add(order);
                _context.OrderItems.AddRange(orderItems);
                _context.Deliveries.Add(delivery);

                foreach (var item in checkoutItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.StockLevel = Math.Max(0, item.Product.StockLevel - item.Quantity);
                    }
                }
                _context.CartItems.RemoveRange(checkoutItems);

                await _context.SaveChangesAsync();

                return Json(new { 
                    clientSecret = paymentIntent.ClientSecret,
                    orderId = orderId
                });
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return BadRequest(new { error = "DB Error: " + msg });
            }
            finally
            {
                _checkoutLockService.ReleaseLock(userId);
            }
        }

        // POST: /Cart/RetryPaymentIntent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryPaymentIntent([FromBody] System.Text.Json.JsonElement requestData)
        {
            var userId = GetUserId();
            if (!await _checkoutLockService.AcquireLockAsync(userId, TimeSpan.Zero))
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = userId,
                    Action = "DUPLICATE TRANSACTION BLOCKED: Concurrent retry payment request rejected by per-user lock (RetryPaymentIntent)",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return BadRequest(new { error = "A checkout is already processing. Please wait." });
            }

            try
            {
                string securityToken = requestData.TryGetProperty("securityToken", out var tokenProp) ? tokenProp.GetString() ?? "" : "";
                if (!_paymentSecurityService.ValidatePaymentToken(securityToken, GetUserId(), out string validatedNonce))
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = userId,
                        Action = "REPLAY ATTACK BLOCKED: Reused or expired security token rejected during retry payment (RetryPaymentIntent)",
                        IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    return BadRequest(new { error = "Session expired or invalid. Please refresh the page and try again." });
                }

                string orderId = requestData.GetProperty("orderId").GetString();
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == GetUserId());
                if (order == null || (order.Status != "Pending" && order.Status != "Pending Payment" && order.Status != "Approved"))
                {
                    return BadRequest(new { error = "Order not found or not pending payment." });
                }

                // --- Inject XGBoost Fraud Evaluation ---
                string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var tenMinsAgo = DateTime.UtcNow.AddMinutes(-10);
                int transactionsLast10Mins = await _context.Orders.CountAsync(o => o.BuyerID == userId && o.CreatedAt >= tenMinsAgo);
                int deviceIpFlags = await _context.AuditLogs.CountAsync(a => a.IP_Address == currentIp && a.Action.Contains("Block"));
                var buyer = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
                double timeSinceAccountCreationSeconds = buyer != null ? (DateTime.UtcNow - buyer.CreatedAt).TotalSeconds : 86400;

                var transactionPayload = new
                {
                    transactionAmount = order.TotalAmount,
                    accountAgeDays = (int)(timeSinceAccountCreationSeconds / 86400),
                    failedLoginAttempts = 0,
                    distanceFromShippingAddress = 15,
                    transactions_last_10_mins = transactionsLast10Mins,
                    time_since_account_creation_seconds = timeSinceAccountCreationSeconds,
                    device_ip_flags = deviceIpFlags
                };

                FraudEvaluationResult aiVerdict;
                if (order.Status == "Approved")
                {
                    aiVerdict = new FraudEvaluationResult { RiskScore = 0.0m, IsBlocked = false, ShapData = "{}" };
                }
                else
                {
                    aiVerdict = await _aiClient.EvaluateTransactionRiskAsync(transactionPayload);
                }

                if (TempData["SkipRiskCheck"] != null)
                {
                    aiVerdict = new FraudEvaluationResult { RiskScore = 0.0m, IsBlocked = false, ShapData = "{}" };
                }

                if (TempData["SkipRiskCheck"] == null && aiVerdict.RiskScore > 0.50m && aiVerdict.RiskScore <= 0.80m && !aiVerdict.IsBlocked)
                {
                    var user = await _context.Users.FindAsync(userId);
                    var pendingFpx = new
                    {
                        PaymentType = "FPX_RETRY",
                        OrderId = orderId,
                        Bank = order.ServiceType != null && order.ServiceType.StartsWith("FPX|") ? order.ServiceType.Replace("FPX|", "") : ""
                    };
                    TempData["PendingCheckout"] = System.Text.Json.JsonSerializer.Serialize(pendingFpx);
                    TempData.Keep("PendingCheckout");

                    string redirectUrl = (user != null && !string.IsNullOrEmpty(user.TotpSecret))
                        ? Url.Action("VerifyCheckoutTotp", "Cart")
                        : Url.Action("VerifyCheckoutOtp", "Cart");

                    if (user != null && string.IsNullOrEmpty(user.TotpSecret))
                    {
                        await _otpService.GenerateAndSendOtpAsync(user.Email, "Checkout Verification");
                        TempData["ResetOtpTimer"] = true;
                    }

                    return Ok(new { 
                        requiresMfa = true, 
                        redirectUrl = redirectUrl 
                    });
                }

                if (aiVerdict.RiskScore > 0.80m || aiVerdict.IsBlocked)
                {
                    order.Status = FYP.Models.Entities.TransactionStatus.RequiredCheck;

                    var fraudAlert = new FraudAlert
                    {
                        AlertID = "FRD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        OrderID = orderId,
                        RiskScore = aiVerdict.RiskScore,
                        Reason = "XGBoost behavioral anomaly detected (via FPX Retry): High transaction velocity or anomaly pattern.",
                        SHAP_Data = aiVerdict.ShapData ?? "{}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.FraudAlerts.Add(fraudAlert);

                    var admins = _context.Users.Where(u => u.Role == "Admin").ToList();
                    foreach (var admin in admins)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = admin.UserID,
                            Type = "Security Alert",
                            Content = $"High-Risk Checkout Blocked (FPX Retry)! Score: {aiVerdict.RiskScore:P1}. Order: {orderId}",
                            CreatedAt = DateTime.UtcNow,
                            IsRead = false
                        });
                    }
                    _context.Notifications.Add(new Notification
                    {
                        NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = userId,
                        Type = "Security Alert",
                        Content = "Security Alert: A suspicious transaction attempt was blocked on your account."
                    });

                    await _context.SaveChangesAsync();
                    return BadRequest(new { error = $"SECURITY BLOCK: Transaction flagged by XGBoost AI (Risk Score: {aiVerdict.RiskScore:P0}). Your transaction is under review." });
                }
                // --- End XGBoost Fraud Evaluation ---

                StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(order.TotalAmount * 100), // RM to cents
                    Currency = "myr",
                    PaymentMethodTypes = new List<string> { "fpx" },
                };

                var requestOptions = new RequestOptions { IdempotencyKey = validatedNonce };
                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options, requestOptions);

                return Json(new { 
                    clientSecret = paymentIntent.ClientSecret,
                    orderId = orderId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            finally
            {
                _checkoutLockService.ReleaseLock(userId);
            }
        }

        // GET: /Cart/PaymentComplete
        [HttpGet]
        public async Task<IActionResult> PaymentComplete(string payment_intent, string payment_intent_client_secret, string redirect_status, string orderId)
        {
            if (redirect_status != "succeeded")
            {
                TempData["ErrorMessage"] = "Payment failed or was cancelled.";
                return RedirectToAction("Index", "Home");
            }

            try 
            {
                StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(payment_intent);

                if (paymentIntent.Status == "succeeded")
                {
                    var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == GetUserId());
                    if (order != null && order.Status == "Pending Payment")
                    {
                        order.Status = "Processing";

                        string paymentId = "PAY-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
                        string detectedMethod = "Credit Card";
                        if (paymentIntent.PaymentMethodTypes != null && paymentIntent.PaymentMethodTypes.Contains("fpx"))
                        {
                            detectedMethod = "FPX Online Banking";
                        }
                        
                        var payment = new Payment
                        {
                            PaymentID = paymentId,
                            OrderID = orderId,
                            Amount = order.TotalAmount,
                            PaymentToken = _paymentEncryptionService.Encrypt(paymentIntent.Id),
                            PaymentMethod = detectedMethod,
                            IdempotencyKey = _paymentEncryptionService.Encrypt(Guid.NewGuid().ToString("N").ToUpper()),
                            Status = "Authorized"
                        };
                        _context.Payments.Add(payment);

                        string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                        var auditLog = new AuditLog
                        {
                            LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = GetUserId(),
                            Action = $"Completed FPX checkout for Order {orderId} (RM {order.TotalAmount:0.00})",
                            IP_Address = currentIp,
                            Timestamp = DateTime.UtcNow
                        };
                        _context.AuditLogs.Add(auditLog);

                        await _context.SaveChangesAsync();
                        await BroadcastNewOrderToSellersAsync(orderId);

                        TempData["SuccessMessage"] = $"Order {orderId} placed successfully via FPX!";
                        return RedirectToAction("OrderDetails", "Buyer", new { orderId = orderId });
                    }
                }
            } 
            catch (Exception) 
            {
                TempData["ErrorMessage"] = "Error verifying payment.";
                return RedirectToAction("Checkout");
            }

            TempData["ErrorMessage"] = "Payment could not be verified or order already processed.";
            return RedirectToAction("Checkout");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayExistingOrder([FromBody] System.Text.Json.JsonElement requestData)
        {
            try
            {
                string securityToken = requestData.TryGetProperty("securityToken", out var tokenProp) ? tokenProp.GetString() ?? "" : "";
                if (!_paymentSecurityService.ValidatePaymentToken(securityToken, GetUserId(), out string validatedNonce))
                {
                    return BadRequest(new { error = "Session expired or invalid. Please refresh the page and try again." });
                }

                string orderId = requestData.GetProperty("orderId").GetString();
                string paymentMethod = requestData.TryGetProperty("paymentMethod", out var pmProp) ? pmProp.GetString() ?? "Credit Card" : "Credit Card";

                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == GetUserId());
                if (order == null || (order.Status != "Pending" && order.Status != "Pending Payment" && order.Status != "Approved"))
                {
                    return BadRequest(new { error = "Order not found or not pending payment." });
                }

                order.Status = "Processing";
                order.ServiceType = paymentMethod;

                string paymentId = "PAY-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
                var payment = new Payment
                {
                    PaymentID = paymentId,
                    OrderID = orderId,
                    Amount = order.TotalAmount,
                    PaymentToken = _paymentEncryptionService.Encrypt("TOK-SESSION-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()),
                    PaymentMethod = paymentMethod,
                    IdempotencyKey = _paymentEncryptionService.Encrypt(Guid.NewGuid().ToString("N").ToUpper()),
                    Status = "Authorized"
                };

                string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var auditLog = new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = GetUserId(),
                    Action = $"Completed checkout via {paymentMethod} for existing Order {orderId} (RM {order.TotalAmount:0.00})",
                    IP_Address = currentIp,
                    Timestamp = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
                await BroadcastNewOrderToSellersAsync(orderId);

                TempData["SuccessMessage"] = $"Order {orderId} paid successfully via {paymentMethod}!";
                return Json(new { success = true, orderId = orderId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetShippingOptions([FromBody] System.Text.Json.JsonElement data)
        {
            try
            {
                var cart = await GetOrCreateCartForUserAsync();
                if (!cart.Items.Any(i => i.IsSelected))
                {
                    return BadRequest(new { error = "No items selected." });
                }

                int addressId = 0;
                if (data.TryGetProperty("addressId", out System.Text.Json.JsonElement addrProp) && addrProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    addressId = addrProp.GetInt32();
                }

                var address = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressID == addressId);
                string addressString = address != null ? $"{address.HouseBuildingStreet}, {address.StateArea} {address.PostalCode}" : "Pending Address";

                var selectedItems = cart.Items.Where(i => i.IsSelected).Select(i => (i.ProductID, i.Quantity)).ToList();
                var cartViewModel = await MapCartToViewModelAsync(cart);
                
                var options = await _shippingService.GetShippingOptionsAsync(selectedItems, cartViewModel.GrandTotal, addressString);

                return Json(new { success = true, options = options });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    

        [HttpGet]
        public IActionResult VerifyCheckoutOtp()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCheckoutOtp(string otp)
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            if (_otpService.ValidateOtp(user.Email, otp))
            {
                var pendingJson = TempData["PendingCheckout"] as string;
                if (!string.IsNullOrEmpty(pendingJson))
                {
                    // MFA passed, bypass the risk check this time
                    TempData["SkipRiskCheck"] = true;
                    TempData.Keep("PendingCheckout");
                    return RedirectToAction("FinalizeCheckoutMfaPassed"); 
                }
                return RedirectToAction("Index");
            }

            ModelState.AddModelError("", "Invalid OTP.");
            return View();
        }

        [HttpGet]
        public IActionResult VerifyCheckoutTotp()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyCheckoutTotp(string code)
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            if (_totpService.VerifyCode(user.TotpSecret, code))
            {
                var pendingJson = TempData["PendingCheckout"] as string;
                if (!string.IsNullOrEmpty(pendingJson))
                {
                    TempData["SkipRiskCheck"] = true;
                    TempData.Keep("PendingCheckout");
                    return RedirectToAction("FinalizeCheckoutMfaPassed");
                }
                return RedirectToAction("Index");
            }

            ModelState.AddModelError("", "Invalid Code.");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendCheckoutOtp()
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized(new { success = false, message = "User not found." });

            await _otpService.GenerateAndSendOtpAsync(user.Email, "Checkout Verification");
            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> FinalizeCheckoutMfaPassed()
        {
            var pendingJson = TempData["PendingCheckout"] as string;
            if (string.IsNullOrEmpty(pendingJson)) return RedirectToAction("Index");

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(pendingJson);
                if (doc.RootElement.TryGetProperty("PaymentType", out var ptProp))
                {
                    string pType = ptProp.GetString() ?? "";
                    if (pType == "FPX")
                    {
                        string bank = doc.RootElement.TryGetProperty("Bank", out var bProp) ? bProp.GetString() ?? "" : "";
                        int addressId = doc.RootElement.TryGetProperty("AddressId", out var aProp) ? aProp.GetInt32() : 0;

                        // Re-run FPX creation with SkipRiskCheck active
                        TempData["SkipRiskCheck"] = true;
                        var (clientSecret, orderId, error) = await ExecuteFpxCheckoutAsync(bank, addressId);
                        if (!string.IsNullOrEmpty(error))
                        {
                            TempData["ErrorMessage"] = error;
                            return RedirectToAction("Checkout");
                        }

                        return View("FpxRedirect", new FpxRedirectViewModel
                        {
                            PublishableKey = _configuration["PaymentGateway:PublishableKey"] ?? "",
                            ClientSecret = clientSecret,
                            OrderId = orderId,
                            Bank = bank
                        });
                    }
                    else if (pType == "FPX_RETRY")
                    {
                        string orderId = doc.RootElement.TryGetProperty("OrderId", out var oProp) ? oProp.GetString() ?? "" : "";
                        string bank = doc.RootElement.TryGetProperty("Bank", out var bProp) ? bProp.GetString() ?? "" : "";

                        TempData["SkipRiskCheck"] = true;
                        var (clientSecret, error) = await ExecuteFpxRetryAsync(orderId);
                        if (!string.IsNullOrEmpty(error))
                        {
                            TempData["ErrorMessage"] = error;
                            return RedirectToAction("Orders", "Buyer");
                        }

                        return View("FpxRedirect", new FpxRedirectViewModel
                        {
                            PublishableKey = _configuration["PaymentGateway:PublishableKey"] ?? "",
                            ClientSecret = clientSecret,
                            OrderId = orderId,
                            Bank = bank
                        });
                    }
                }
            }
            catch
            {
                // Fallback to standard CheckoutViewModel deserialization
            }

            var model = System.Text.Json.JsonSerializer.Deserialize<CheckoutViewModel>(pendingJson);
            
            // Re-run ProcessCheckout with SkipRiskCheck active
            TempData["SkipRiskCheck"] = true;
            return await ProcessCheckout(model);
        }

        private async Task<(string clientSecret, string orderId, string error)> ExecuteFpxCheckoutAsync(string bankCode, int addressId)
        {
            var userId = GetUserId();
            var cart = await GetOrCreateCartForUserAsync();
            var cartViewModel = await MapCartToViewModelAsync(cart);
            
            if (!cart.Items.Any(i => i.IsSelected))
            {
                return ("", "", "Cart is empty or no items selected.");
            }

            var checkoutItems = cart.Items.Where(i => i.IsSelected).ToList();
            decimal totalAmount = cartViewModel.Items.Where(i => i.IsSelected).Sum(i => i.Subtotal);

            decimal finalShippingFee = 0;
            string finalCourierId = "COUR_JNT";
            var defaultAddress = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressID == addressId)
                                ?? await _context.Addresses.FirstOrDefaultAsync(a => a.UserID == userId && a.IsDefault);
            if (defaultAddress != null)
            {
                var addressString = $"{defaultAddress.HouseBuildingStreet}, {defaultAddress.StateArea} {defaultAddress.PostalCode}";
                var shippingItems = checkoutItems.Select(i => (i.ProductID, i.Quantity));
                var (originalFee, fee, courier) = await _shippingService.CalculateAndAssignShippingAsync(shippingItems, cartViewModel.GrandTotal, addressString);
                finalShippingFee = fee;
                if (courier != null) finalCourierId = courier.CourierID;
            }

            totalAmount += finalShippingFee;

            StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(totalAmount * 100), // RM to cents
                Currency = "myr",
                PaymentMethodTypes = new List<string> { "fpx" },
            };

            var requestOptions = new RequestOptions { IdempotencyKey = Guid.NewGuid().ToString("N").ToUpper() };
            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options, requestOptions);

            string orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            string serviceType = string.IsNullOrEmpty(bankCode) ? "FPX Online Banking" : $"FPX|{bankCode}";

            var order = new Order
            {
                OrderID = orderId,
                BuyerID = userId,
                TotalAmount = totalAmount,
                Status = "Pending Payment",
                CreatedAt = DateTime.UtcNow,
                ServiceType = serviceType 
            };
            
            var orderItems = checkoutItems.Select(item => new OrderItem
            {
                OrderItemID = "OIT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                OrderID = orderId,
                ProductID = item.ProductID,
                Quantity = item.Quantity,
                UnitPrice = item.Product.Price
            }).ToList();

            var delivery = new Delivery
            {
                DeliveryID = "DEL-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                OrderID = orderId,
                CourierID = finalCourierId,
                ShippingFee = finalShippingFee,
                Status = "Pending"
            };

            _context.Orders.Add(order);
            _context.OrderItems.AddRange(orderItems);
            _context.Deliveries.Add(delivery);

            foreach (var item in checkoutItems)
            {
                if (item.Product != null)
                {
                    item.Product.StockLevel = Math.Max(0, item.Product.StockLevel - item.Quantity);
                }
            }
            _context.CartItems.RemoveRange(checkoutItems);

            await _context.SaveChangesAsync();

            return (paymentIntent.ClientSecret, orderId, null);
        }

        private async Task<(string clientSecret, string error)> ExecuteFpxRetryAsync(string orderId)
        {
            var userId = GetUserId();
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == userId);
            if (order == null || (order.Status != "Pending" && order.Status != "Pending Payment" && order.Status != "Approved"))
            {
                return ("", "Order not found or not pending payment.");
            }

            StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(order.TotalAmount * 100),
                Currency = "myr",
                PaymentMethodTypes = new List<string> { "fpx" },
            };

            var requestOptions = new RequestOptions { IdempotencyKey = Guid.NewGuid().ToString("N").ToUpper() };
            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options, requestOptions);

            return (paymentIntent.ClientSecret, null);
        }

    }
}
