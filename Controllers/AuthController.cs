using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using FYP.Security;
using FYP.Services;

namespace FYP.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;

        public AuthController(ApplicationDbContext context, IOtpService otpService)
        {
            _context = context;
            _otpService = otpService;
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
                var existingUser = _context.Users.FirstOrDefault(u => u.Email == model.Email);
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
                var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);
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

                // Enforce MFA Step: Dispatch OTP before logging in
                if (user.MFA_Enabled)
                {
                    await _otpService.GenerateAndSendOtpAsync(user.Email, "Login Multi-Factor Authentication");
                    return RedirectToAction("VerifyOtp", new { email = user.Email });
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
                    new Claim(ClaimTypes.Name, user.Email.Split('@')[0]), // use username part as name
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                return RedirectToAction("Index", "Home");
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
                    var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);
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
                        return RedirectToAction("Index", "Home");
                    }
                    return RedirectToAction("Index", "Home");
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
                var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);
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
                var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);
                if (user != null)
                {
                    // Ensure the new password isn't the same as the current password
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

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToUserDashboard(string role, string userId)
        {
            return role switch
            {
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                "Seller" => RedirectToAction("Dashboard", "Seller", new { sellerId = userId }),
                _ => RedirectToAction("Dashboard", "Buyer", new { buyerId = userId })
            };
        }
    }
}