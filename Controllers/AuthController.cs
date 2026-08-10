using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using FYP.Security;
using FYP.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;
        private readonly TotpService _totpService;
        private readonly IMemoryCache _cache;

        public AuthController(ApplicationDbContext context, IOtpService otpService, TotpService totpService, IMemoryCache cache)
        {
            _context = context;
            _otpService = otpService;
            _totpService = totpService;
            _cache = cache;
        }

        // ==========================================
        // 1. REGISTRATION
        // ==========================================
        [HttpGet]
        public IActionResult Register(string role = "Buyer")
        {
            if (role == "Seller") return View("~/Views/Seller/Register.cshtml");
            if (role == "Courier") return View("~/Views/Courier/Register.cshtml");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model.Role == "Courier" && string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "Phone Number is required for couriers.");
            }

            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email is already registered.");
                    if (model.Role == "Courier") return View("~/Views/Courier/Register.cshtml", model);
                    if (model.Role == "Seller") return View("~/Views/Seller/Register.cshtml", model); // If it exists
                    return View(model);
                }

                string hashedPassword = Argon2idHasher.HashPassword(model.Password);
                string userId = "USR-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

                var newUser = new User
                {
                    UserID = userId,
                    Name = model.Name,
                    PhoneNumber = model.PhoneNumber,
                    Email = model.Email,
                    PasswordHash = hashedPassword,
                    Role = string.IsNullOrWhiteSpace(model.Role) ? "Buyer" : model.Role,
                    StoreName = model.StoreName,
                    SSMNumber = model.SSMNumber,
                    MFA_Enabled = true,
                    DeviceHash = HttpContext.Request.Headers["User-Agent"].ToString()
                };

                string pendingUserJson = System.Text.Json.JsonSerializer.Serialize(newUser);
                TempData["PendingUser_" + model.Email] = pendingUserJson;

                // Dispatch initial welcome OTP
                await _otpService.GenerateAndSendOtpAsync(model.Email, "Account Registration");

                TempData["SuccessMessage"] = "Registration successful! Please check your email for the verification OTP.";
                return RedirectToAction("VerifyOtp", new { email = model.Email });
            }
            if (model.Role == "Courier") return View("~/Views/Courier/Register.cshtml", model);
            if (model.Role == "Seller") return View("~/Views/Seller/Register.cshtml", model);
            return View(model);
        }

        // ==========================================
        // 2. LOGIN & MFA GATEWAY
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                string deviceIdentifier = $"{model.Email}:{currentIp}";
                var lockoutRecord = await _context.DeviceLockouts.FirstOrDefaultAsync(dl => dl.DeviceIdentifier == deviceIdentifier);

                if (lockoutRecord != null && lockoutRecord.LockoutEnd.HasValue && lockoutRecord.LockoutEnd > DateTime.UtcNow)
                {
                    int minutesLeft = (int)Math.Ceiling((lockoutRecord.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
                    ModelState.AddModelError("", $"Account locked due to too many failed attempts. Please try again in {minutesLeft} minutes.");
                    if (model.Role == "Courier") return View("~/Views/Courier/Login.cshtml", model);
                    if (model.Role == "Seller") return View("~/Views/Seller/Login.cshtml", model);
                    if (model.Role == "Admin") return View("~/Views/Admin/Login.cshtml", model);
                    return View(model);
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

                string expectedRole = string.IsNullOrEmpty(model.Role) ? "Buyer" : model.Role;
                bool isRoleMismatch = user != null && !string.Equals(user.Role, expectedRole, StringComparison.OrdinalIgnoreCase);

                if (user == null || !Argon2idHasher.VerifyHash(model.Password, user.PasswordHash) || isRoleMismatch)
                {
                    if (lockoutRecord == null)
                    {
                        lockoutRecord = new FYP.Models.Entities.DeviceLockout { DeviceIdentifier = deviceIdentifier, FailedAttempts = 1 };
                        _context.DeviceLockouts.Add(lockoutRecord);
                    }
                    else
                    {
                        lockoutRecord.FailedAttempts++;
                        if (lockoutRecord.FailedAttempts >= 5)
                        {
                            lockoutRecord.LockoutEnd = DateTime.UtcNow.AddMinutes(10);
                        }
                    }
                    await _context.SaveChangesAsync();

                    if (lockoutRecord.FailedAttempts >= 5)
                    {
                        ModelState.AddModelError("", "Account locked due to too many failed attempts. Please try again in 10 minutes.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Invalid email or password");
                    }
                    if (model.Role == "Courier") return View("~/Views/Courier/Login.cshtml", model);
                    if (model.Role == "Seller") return View("~/Views/Seller/Login.cshtml", model);
                    if (model.Role == "Admin") return View("~/Views/Admin/Login.cshtml", model);
                    return View(model);
                }
                
                if (user.IsDisabled)
                {
                    ModelState.AddModelError("", "Your account has been disabled by an administrator.");
                    if (model.Role == "Courier") return View("~/Views/Courier/Login.cshtml", model);
                    if (model.Role == "Seller") return View("~/Views/Seller/Login.cshtml", model);
                    if (model.Role == "Admin") return View("~/Views/Admin/Login.cshtml", model);
                    return View(model);
                }



                if (lockoutRecord != null)
                {
                    _context.DeviceLockouts.Remove(lockoutRecord);
                    await _context.SaveChangesAsync();
                }

                // Check device fingerprint anomaly
                string currentDevice = HttpContext.Request.Headers["User-Agent"].ToString();
                
                // For backward compatibility, check if the single DeviceHash matches, or if it's in UserDevices
                bool deviceRecognized = (user.DeviceHash == currentDevice) || await _context.UserDevices.AnyAsync(ud => ud.UserID == user.UserID && ud.DeviceHash == currentDevice);

                // BYPASS: Disable device fingerprinting for the Seed Admin account
                if (user.Email == "demo_admin@secureplatform.com" || user.Email == "demo_seller@secureplatform.com")
                {
                    deviceRecognized = true;
                }

                if (!deviceRecognized)
                {
                    // Device not recognized, start approval flow
                    string approvalToken = Guid.NewGuid().ToString("N");
                    var pendingLogin = new PendingDeviceApproval 
                    {
                        UserID = user.UserID,
                        DeviceHash = currentDevice,
                        IPAddress = currentIp,
                        UserAgent = currentDevice
                    };
                    
                    var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                    _cache.Set($"DeviceApproval_{approvalToken}", pendingLogin, cacheOptions);

                    string approvalLink = Url.Action("ApproveDevice", "Auth", new { token = approvalToken }, Request.Scheme);
                    
                    // Simple parsing for email display
                    string os = currentDevice.Contains("Windows") ? "Windows" : currentDevice.Contains("Mac OS") ? "Mac OS" : currentDevice.Contains("Linux") ? "Linux" : "Unknown OS";
                    string browser = currentDevice.Contains("Chrome") ? "Chrome" : currentDevice.Contains("Firefox") ? "Firefox" : currentDevice.Contains("Safari") ? "Safari" : "Unknown Browser";

                    await _otpService.SendDeviceApprovalEmailAsync(user.Email, os, browser, currentIp, approvalLink);

                    _context.AuditLogs.Add(new AuditLog
                    {
                        LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = user.UserID,
                        Action = "SECURITY WARNING: New device fingerprint detected. Approval email sent.",
                        IP_Address = currentIp,
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    return RedirectToAction("WaitingForDeviceApproval", new { token = approvalToken });
                }
                else 
                {
                    // Update LastUsedAt if it was in UserDevices
                    var existingDevice = await _context.UserDevices.FirstOrDefaultAsync(ud => ud.UserID == user.UserID && ud.DeviceHash == currentDevice);
                    if (existingDevice != null)
                    {
                        existingDevice.LastUsedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                // Enforce MFA Step: Route to Authenticator App or Email OTP
                if (user.MFA_Enabled)
                {
                    if (!string.IsNullOrEmpty(user.TotpSecret))
                    {
                        // Route to zero-trust TOTP App verification
                        return RedirectToAction("VerifyTotpLogin", new { userId = user.UserID });
                    }
                    else
                    {
                        // Fallback to traditional Email OTP
                        await _otpService.GenerateAndSendOtpAsync(user.Email, "Login Multi-Factor Authentication");
                        return RedirectToAction("VerifyOtp", new { email = user.Email });
                    }
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = user.UserID,
                    Action = "User logged in successfully.",
                    IP_Address = currentIp,
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID),
                    new Claim(ClaimTypes.Name, user.Email.Split('@')[0]),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                // Dynamic Redirect: Centralized routing engine
                return RedirectToUserDashboard(user.Role, user.UserID);
            }
            if (model.Role == "Courier") return View("~/Views/Courier/Login.cshtml", model);
            if (model.Role == "Seller") return View("~/Views/Seller/Login.cshtml", model);
            return View(model);
        }

        // ==========================================
        // 2.5. DEVICE APPROVAL
        // ==========================================
        [HttpGet]
        public IActionResult WaitingForDeviceApproval(string token)
        {
            if (string.IsNullOrEmpty(token) || !_cache.TryGetValue($"DeviceApproval_{token}", out PendingDeviceApproval pendingLogin))
            {
                return RedirectToAction("Login");
            }
            ViewBag.Token = token;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ApproveDevice(string token)
        {
            if (string.IsNullOrEmpty(token) || !_cache.TryGetValue($"DeviceApproval_{token}", out PendingDeviceApproval pendingLogin))
            {
                return Content("Invalid or expired link. Please try logging in again.");
            }

            // Save the device as trusted
            var user = await _context.Users.FindAsync(pendingLogin.UserID);
            if (user != null)
            {
                // Optionally update the single string for backward compatibility
                if (string.IsNullOrEmpty(user.DeviceHash) || user.DeviceHash == "SEED")
                {
                    user.DeviceHash = pendingLogin.DeviceHash;
                }

                // Simple parsing for OS/Browser
                string os = pendingLogin.UserAgent.Contains("Windows") ? "Windows" : pendingLogin.UserAgent.Contains("Mac OS") ? "Mac OS" : pendingLogin.UserAgent.Contains("Linux") ? "Linux" : "Unknown OS";
                string browser = pendingLogin.UserAgent.Contains("Chrome") ? "Chrome" : pendingLogin.UserAgent.Contains("Firefox") ? "Firefox" : pendingLogin.UserAgent.Contains("Safari") ? "Safari" : "Unknown Browser";

                _context.UserDevices.Add(new UserDevice
                {
                    UserID = user.UserID,
                    DeviceHash = pendingLogin.DeviceHash,
                    OS = os,
                    Browser = browser,
                    IPAddress = pendingLogin.IPAddress,
                    AddedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                });
                
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = user.UserID,
                    Action = $"User approved new device: {os} on {browser}",
                    IP_Address = pendingLogin.IPAddress,
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            // Mark as approved so the polling endpoint can log them in
            pendingLogin.IsApproved = true;
            _cache.Set($"DeviceApproval_{token}", pendingLogin, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(10)));

            return View("DeviceApproved");
        }

        [HttpGet]
        public async Task<IActionResult> CheckDeviceApprovalStatus(string token)
        {
            if (string.IsNullOrEmpty(token) || !_cache.TryGetValue($"DeviceApproval_{token}", out PendingDeviceApproval pendingLogin))
            {
                return Json(new { status = "expired" });
            }

            if (pendingLogin.IsApproved)
            {
                // Authenticate and log in the user
                var user = await _context.Users.FindAsync(pendingLogin.UserID);
                if (user == null) return Json(new { status = "error" });

                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = user.UserID,
                    Action = "User logged in successfully (After Device Approval).",
                    IP_Address = pendingLogin.IPAddress,
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID),
                    new Claim(ClaimTypes.Name, user.Email.Split('@')[0]),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                string redirectUrl = user.Role switch
                {
                    "Buyer" => Url.Action("Dashboard", "Buyer"),
                    "Seller" => Url.Action("Dashboard", "Seller"),
                    "Courier" => Url.Action("Dashboard", "Courier"),
                    "Admin" => Url.Action("Dashboard", "Admin"),
                    _ => Url.Action("Index", "Home")
                };

                // Clear the cache
                _cache.Remove($"DeviceApproval_{token}");

                return Json(new { status = "approved", redirectUrl });
            }

            return Json(new { status = "pending" });
        }

        // ==========================================
        // 3. MFA 2FA VERIFICATION GATEWAY
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> VerifyOtp(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null && user.Role == "Courier")
            {
                return View("~/Views/Courier/VerifyOtp.cshtml", new VerifyOtpViewModel { Email = email });
            }
            
            var pendingJson = TempData.Peek("PendingUser_" + email) as string;
            if (!string.IsNullOrEmpty(pendingJson))
            {
                var pendingUser = System.Text.Json.JsonSerializer.Deserialize<User>(pendingJson);
                if (pendingUser?.Role == "Courier")
                {
                    return View("~/Views/Courier/VerifyOtp.cshtml", new VerifyOtpViewModel { Email = email });
                }
            }

            return View(new VerifyOtpViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool isValid = _otpService.ValidateOtp(model.Email, model.OtpCode);
                if (isValid)
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                    if (user == null)
                    {
                        var pendingJson = TempData["PendingUser_" + model.Email] as string;
                        if (!string.IsNullOrEmpty(pendingJson))
                        {
                            user = System.Text.Json.JsonSerializer.Deserialize<User>(pendingJson);
                            if (user != null)
                            {
                                _context.Users.Add(user);
                                _context.AuditLogs.Add(new AuditLog
                                {
                                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                                    UserID = user.UserID,
                                    Action = $"New user registered with role: {user.Role}",
                                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                                    Timestamp = DateTime.UtcNow
                                });
                                await _context.SaveChangesAsync();
                            }
                        }
                    }

                    if (user != null)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, user.UserID),
                            new Claim(ClaimTypes.Name, user.Email.Split('@')[0]),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim(ClaimTypes.Role, user.Role)
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                        TempData["SuccessMessage"] = "Identity verified! Welcome to SecurePlatform.";

                        // Dynamic Redirect: Centralized routing engine
                        return RedirectToUserDashboard(user.Role, user.UserID);
                    }
                    return RedirectToAction("Login");
                }

                ModelState.AddModelError("OtpCode", "Invalid or expired verification code.");
            }

            var fallbackUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (fallbackUser != null && fallbackUser.Role == "Courier")
            {
                return View("~/Views/Courier/VerifyOtp.cshtml", model);
            }

            var fallbackJson = TempData.Peek("PendingUser_" + model.Email) as string;
            if (!string.IsNullOrEmpty(fallbackJson))
            {
                var pendingUser = System.Text.Json.JsonSerializer.Deserialize<User>(fallbackJson);
                if (pendingUser?.Role == "Courier")
                {
                    return View("~/Views/Courier/VerifyOtp.cshtml", model);
                }
            }

            return View(model);
        }

        // ==========================================
        // 4. PASSWORD RECOVERY FLOW
        // ==========================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user != null)
                {
                    await _otpService.GenerateAndSendOtpAsync(model.Email, "Password Reset");
                    return RedirectToAction("ResetPasswordOtp", new { email = model.Email });
                }
                ModelState.AddModelError("", "Email address not found.");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordOtp(string email)
        {
            return View(new ResetPasswordOtpViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPasswordOtp(ResetPasswordOtpViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool isValid = _otpService.ValidateOtp(model.Email, model.OtpCode);
                if (isValid)
                {
                    return RedirectToAction("SetNewPassword", new { email = model.Email });
                }
                ModelState.AddModelError("OtpCode", "Invalid or expired reset code.");
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult SetNewPassword(string email)
        {
            return View(new SetNewPasswordViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetNewPassword(SetNewPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user != null)
                {
                    if (Argon2idHasher.VerifyHash(model.NewPassword, user.PasswordHash))
                    {
                        ModelState.AddModelError("NewPassword", "New password cannot be the same as your current password.");
                        return View(model);
                    }

                    user.PasswordHash = Argon2idHasher.HashPassword(model.NewPassword);

                    _context.AuditLogs.Add(new AuditLog
                    {
                        LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = user.UserID,
                        Action = "User successfully reset their password.",
                        IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Timestamp = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Your password has been reset successfully. Please log in.";
                    return RedirectToAction("Login");
                }
                ModelState.AddModelError("", "User account not found.");
            }
            return View(model);
        }

        // ==========================================
        // 5. TOTP AUTHENTICATOR APP GATEWAY
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> SetupTotp(string? userId = null)
        {
            string targetUserId = userId ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(targetUserId))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == targetUserId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (string.IsNullOrEmpty(user.TotpSecret))
            {
                user.TotpSecret = _totpService.GenerateSecretKey();
                await _context.SaveChangesAsync();
            }

            string otpauthUri = _totpService.GenerateQrCodeUri(user.Email, user.TotpSecret);

            ViewBag.SecretKey = user.TotpSecret;
            ViewBag.OtpauthUri = otpauthUri;
            ViewBag.UserEmail = user.Email;
            ViewBag.UserId = user.UserID;
            ViewBag.Role = user.Role;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTotp(string userId, string verificationCode)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null || string.IsNullOrEmpty(user.TotpSecret))
            {
                TempData["ErrorMessage"] = "Invalid user session or secret key missing.";
                return RedirectToAction(nameof(SetupTotp), new { userId = userId });
            }

            bool isValid = _totpService.VerifyCode(user.TotpSecret, verificationCode);

            if (!isValid)
            {
                TempData["ErrorMessage"] = "🚨 INVALID CODE: Time-based OTP mismatch. Please wait for the code to refresh on your app and try again.";
                return RedirectToAction(nameof(SetupTotp), new { userId = userId });
            }

            user.MFA_Enabled = true;

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = user.UserID,
                Action = "Enrolled physical TOTP Authenticator device (RFC 6238 HMAC-SHA1)",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✓ Hardware/Mobile Authenticator enrolled successfully! Multi-Factor Authentication is now active.";

            return RedirectToUserDashboard(user.Role, user.UserID);
        }

        [HttpGet]
        public IActionResult VerifyTotpLogin(string userId)
        {
            ViewBag.UserId = userId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyTotpLogin(string userId, string authenticatorCode)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null || string.IsNullOrEmpty(user.TotpSecret))
            {
                return RedirectToAction("Login");
            }

            bool isValid = _totpService.VerifyCode(user.TotpSecret, authenticatorCode);
            if (isValid)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserID),
                    new Claim(ClaimTypes.Name, user.Email.Split('@')[0]),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                TempData["SuccessMessage"] = "Cryptographic identity verified! Welcome back.";
                return RedirectToUserDashboard(user.Role, user.UserID);
            }

            ModelState.AddModelError("", "Invalid or expired Authenticator code.");
            ViewBag.UserId = userId;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Logout(string returnUrl = null)
        {
            TempData.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        // Centralized Router for Role-Based Environment Redirection
        private IActionResult RedirectToUserDashboard(string role, string userId)
        {
            return role switch
            {
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                "Seller" => RedirectToAction("Dashboard", "Seller"),
                "Courier" => RedirectToAction("Dashboard", "Courier"),
                _ => RedirectToAction("Index", "Home")
            };
        }
    }
}