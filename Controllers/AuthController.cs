using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using FYP.Security;
using FYP.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
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

        public AuthController(ApplicationDbContext context, IOtpService otpService, TotpService totpService)
        {
            _context = context;
            _otpService = otpService;
            _totpService = totpService;
        }

        // ==========================================
        // 1. REGISTRATION
        // ==========================================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Email is already registered.");
                    return View(model);
                }

                string hashedPassword = Argon2idHasher.HashPassword(model.Password);
                string userId = "USR-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

                var newUser = new User
                {
                    UserID = userId,
                    Email = model.Email,
                    PasswordHash = hashedPassword,
                    Role = string.IsNullOrWhiteSpace(model.Role) ? "Buyer" : model.Role,
                    MFA_Enabled = true,
                    DeviceHash = HttpContext.Request.Headers["User-Agent"].ToString()
                };

                var auditLog = new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = userId,
                    Action = $"New user registered with role: {newUser.Role}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                // Dispatch initial welcome OTP
                await _otpService.GenerateAndSendOtpAsync(model.Email, "Account Registration");

                TempData["SuccessMessage"] = "Registration successful! Please check your email for the verification OTP.";
                return RedirectToAction("VerifyOtp", new { email = model.Email });
            }
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
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                if (user == null || !Argon2idHasher.VerifyHash(model.Password, user.PasswordHash))
                {
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
                }

                // Check device fingerprint anomaly
                string currentDevice = HttpContext.Request.Headers["User-Agent"].ToString();
                if (user.DeviceHash != currentDevice)
                {
                    user.DeviceHash = currentDevice;
                    _context.AuditLogs.Add(new AuditLog
                    {
                        LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = user.UserID,
                        Action = "SECURITY WARNING: New device fingerprint detected on login.",
                        IP_Address = currentIp,
                        Timestamp = DateTime.UtcNow
                    });
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
            return View(model);
        }

        // ==========================================
        // 3. MFA 2FA VERIFICATION GATEWAY
        // ==========================================
        [HttpGet]
        public IActionResult VerifyOtp(string email)
        {
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
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // Centralized Router for Role-Based Environment Redirection
        private IActionResult RedirectToUserDashboard(string role, string userId)
        {
            return role switch
            {
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                "Seller" => RedirectToAction("Dashboard", "Seller"),
                _ => RedirectToAction("Index", "Home")
            };
        }
    }
}