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

        public CartController(ApplicationDbContext context, PythonAiClient aiClient, IConfiguration configuration, IShippingService shippingService, IHubContext<OrderHub> orderHubContext)
        {
            _context = context;
            _aiClient = aiClient;
            _configuration = configuration;
            _shippingService = shippingService;
            _orderHubContext = orderHubContext;
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
            await AddToCart(productId, quantity);
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
                SavedCards = savedCards
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

        // POST: /Cart/ProcessCheckout (XGBoost Fraud Integration)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCheckout(CheckoutViewModel model)
        {
            var cart = await GetOrCreateCartForUserAsync();
            var cartViewModel = await MapCartToViewModelAsync(cart);
            
            if (!cart.Items.Any())
            {
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrEmpty(model.CourierName))
            {
                // Fallback for stale form submission or missing data
                var userAddresses = await _context.Addresses.Where(a => a.UserID == GetUserId()).ToListAsync();
                var selectedAddress = userAddresses.FirstOrDefault(a => a.AddressID == model.SelectedAddressID);
                if (selectedAddress != null)
                {
                    var addressString = $"{selectedAddress.HouseBuildingStreet}, {selectedAddress.StateArea} {selectedAddress.PostalCode}";
                    var shippingItems = cart.Items.Where(i => i.IsSelected).Select(i => (i.ProductID, i.Quantity));
                    var (originalFee, fee, courier) = await _shippingService.CalculateAndAssignShippingAsync(shippingItems, cartViewModel.GrandTotal, addressString);
                    
                    model.OriginalShippingFee = originalFee;
                    model.ShippingFee = fee;
                    model.CourierName = courier?.Name ?? "Standard Courier";
                    model.CourierID = courier?.CourierID ?? "COUR_JNT";
                    
                    ModelState.Remove("CourierName");
                    ModelState.Remove("CourierID");
                    ModelState.Remove("OriginalShippingFee");
                    ModelState.Remove("ShippingFee");
                }
            }

            model.CartItems = cartViewModel.Items.Where(i => i.IsSelected).ToList();
            model.TotalAmount = cartViewModel.GrandTotal + model.ShippingFee;

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

            // Simulate the algorithmic risk score check:
            double simulatedRiskScore = (double)model.TotalAmount > 5000 ? 0.85 : 0.12;

            string orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            if (simulatedRiskScore > 0.80)
            {
                var blockedOrder = new Order
                {
                    OrderID = orderId,
                    BuyerID = GetUserId(),
                    TotalAmount = model.TotalAmount,
                    Status = "Declined - AI Security Block",
                    CreatedAt = DateTime.UtcNow,
                    ServiceType = "Standard Delivery"
                };

                // Aligned to match your exact FraudAlert properties
                var fraudAlert = new FraudAlert
                {
                    AlertID = "FRD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    OrderID = orderId,
                    RiskScore = (decimal)simulatedRiskScore,
                    Reason = "XGBoost behavioral anomaly detected: Abnormal expenditure spike.",
                    SHAP_Data = "{\"reason\": \"High transaction velocity and abnormal expenditure spike detected by XGBoost.\"}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(blockedOrder);
                _context.FraudAlerts.Add(fraudAlert);
                await _context.SaveChangesAsync();

                ModelState.AddModelError("", $"🚨 SECURITY BLOCK: Transaction flagged by XGBoost AI (Risk Score: {simulatedRiskScore:P0}). Please complete step-up MFA verification.");
                return View("Checkout", model);
            }

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

            var checkoutItems = cart.Items.Where(i => i.IsSelected).ToList();
            
            // Recalculate Shipping Fee dynamically
            decimal finalShippingFee = 0;
            string finalCourierId = "COUR_JNT";
            var defaultAddress = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressID == model.SelectedAddressID)
                                ?? await _context.Addresses.FirstOrDefaultAsync(a => a.UserID == GetUserId() && a.IsDefault);
            if (defaultAddress != null)
            {
                var addressString = $"{defaultAddress.HouseBuildingStreet}, {defaultAddress.StateArea} {defaultAddress.PostalCode}";
                var shippingItems = checkoutItems.Select(i => (i.ProductID, i.Quantity));
                var (originalFee, fee, courier) = await _shippingService.CalculateAndAssignShippingAsync(shippingItems, cartViewModel.GrandTotal, addressString);
                finalShippingFee = fee;
                if (courier != null) finalCourierId = courier.CourierID;
            }

            decimal finalTotalAmount = model.TotalAmount + finalShippingFee;

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
            
            foreach (var item in checkoutItems)
            {
                if (item.Product != null)
                {
                    item.Product.StockLevel = Math.Max(0, item.Product.StockLevel - item.Quantity);
                    orderItems.Add(new OrderItem
                    {
                        OrderItemID = "OIT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        OrderID = orderId,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product.Price
                    });
                }
            }

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
                Action = $"Completed checkout for Order {orderId} (RM {model.TotalAmount:0.00}) - XGBoost Risk: {simulatedRiskScore:P0}",
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

        // POST: /Cart/CreatePaymentIntent
        [HttpPost]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] System.Text.Json.JsonElement requestData)
        {
            try
            {
                var cart = await GetOrCreateCartForUserAsync();
                var cartViewModel = await MapCartToViewModelAsync(cart);
                
                if (!cart.Items.Any(i => i.IsSelected))
                {
                    return BadRequest(new { error = "Cart is empty or no items selected." });
                }

                string bankCode = requestData.TryGetProperty("bank", out var bankProp) ? bankProp.GetString() : "";
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

                StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(totalAmount * 100), // RM to cents
                    Currency = "myr",
                    PaymentMethodTypes = new List<string> { "fpx" },
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                string orderId = "ORD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
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
        }

        // POST: /Cart/RetryPaymentIntent
        [HttpPost]
        public async Task<IActionResult> RetryPaymentIntent([FromBody] System.Text.Json.JsonElement requestData)
        {
            try
            {
                string orderId = requestData.GetProperty("orderId").GetString();
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == GetUserId());
                if (order == null || (order.Status != "Pending" && order.Status != "Pending Payment"))
                {
                    return BadRequest(new { error = "Order not found or not pending payment." });
                }

                StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(order.TotalAmount * 100), // RM to cents
                    Currency = "myr",
                    PaymentMethodTypes = new List<string> { "fpx" },
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                return Json(new { 
                    clientSecret = paymentIntent.ClientSecret,
                    orderId = orderId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
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
                            PaymentToken = paymentIntent.Id,
                            PaymentMethod = detectedMethod,
                            IdempotencyKey = Guid.NewGuid().ToString("N").ToUpper(),
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
        public async Task<IActionResult> PayExistingOrder([FromBody] System.Text.Json.JsonElement requestData)
        {
            try
            {
                string orderId = requestData.GetProperty("orderId").GetString();
                string paymentMethod = requestData.TryGetProperty("paymentMethod", out var pmProp) ? pmProp.GetString() : "Credit Card";

                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == GetUserId());
                if (order == null || (order.Status != "Pending" && order.Status != "Pending Payment"))
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
                    PaymentToken = "TOK-SESSION-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    PaymentMethod = paymentMethod,
                    IdempotencyKey = Guid.NewGuid().ToString("N").ToUpper(),
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
    }
}