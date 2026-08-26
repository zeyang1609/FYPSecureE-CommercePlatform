using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FYP.Services
{
    public interface IOtpService
    {
        Task<bool> GenerateAndSendOtpAsync(string email, string purpose);
        bool ValidateOtp(string email, string code);
        Task<bool> SendDeviceApprovalEmailAsync(string email, string deviceOs, string browser, string ipAddress, string approvalLink);
    }

    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<OtpService> _logger;

        public OtpService(IMemoryCache cache, IConfiguration config, ILogger<OtpService> logger)
        {
            _cache = cache;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> GenerateAndSendOtpAsync(string email, string purpose)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;

            // 1. Generate Cryptographically Secure 6-digit OTP
            string otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            // 2. Store in MemoryCache for 5 minutes
            string cacheKey = $"OTP_{email.Trim().ToLower()}";
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(1));

            _cache.Set(cacheKey, otpCode, cacheOptions);

            // 3. Dispatch Email or Log to Console
            _ = Task.Run(async () => await SendEmailAsync(email, purpose, otpCode));
            return true;
        }

        public bool ValidateOtp(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code)) return false;

            string cacheKey = $"OTP_{email.Trim().ToLower()}";

            if (_cache.TryGetValue(cacheKey, out string? cachedCode))
            {
                if (cachedCode == code.Trim())
                {
                    // Single-use token: Remove immediately upon successful validation
                    _cache.Remove(cacheKey);
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> SendEmailAsync(string email, string purpose, string otpCode)
        {
            try
            {
                var smtpHost = _config["SmtpSettings:Host"];
                var smtpPort = int.TryParse(_config["SmtpSettings:Port"], out var port) ? port : 587;
                var smtpUser = _config["SmtpSettings:Username"];
                var smtpPass = _config["SmtpSettings:Password"];
                var senderEmail = _config["SmtpSettings:SenderEmail"] ?? "no-reply@secureplatform.com";

                // Development Mode: If no SMTP credentials exist, print OTP directly to Console Output
                if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n==========================================");
                    Console.WriteLine($"[SECURITY OTP GATEWAY] {email}");
                    Console.WriteLine($"[PURPOSE] {purpose}");
                    Console.WriteLine($"[VERIFICATION CODE] -> {otpCode}");
                    Console.WriteLine($"==========================================\n");
                    Console.ResetColor();
                    return true;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "SecurePlatform Security Gate"),
                    Subject = $"Your Verification Code - {purpose}",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; max-width: 500px;'>
                            <h2 style='color: #333;'>Identity Verification Required</h2>
                            <p style='color: #666;'>A request was received for: <strong>{purpose}</strong>.</p>
                            <div style='background: #f8f9fa; padding: 15px; text-align: center; border-radius: 6px; margin: 20px 0;'>
                                <span style='font-size: 28px; font-weight: bold; letter-spacing: 6px; color: #EE4D2D;'>{otpCode}</span>
                            </div>
                            <p style='font-size: 12px; color: #999;'>This code is valid for <strong>1 minute</strong>. If you did not make this request, please ignore this email.</p>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);
                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to dispatch OTP email to {email}.");

                // Fallback debug log
                Console.WriteLine($"[FALLBACK OTP CODE] {email} -> {otpCode}");
                return false;
            }
        }

        public async Task<bool> SendDeviceApprovalEmailAsync(string email, string deviceOs, string browser, string ipAddress, string approvalLink)
        {
            try
            {
                var smtpHost = _config["SmtpSettings:Host"];
                var smtpPort = int.TryParse(_config["SmtpSettings:Port"], out var port) ? port : 587;
                var smtpUser = _config["SmtpSettings:Username"];
                var smtpPass = _config["SmtpSettings:Password"];
                var senderEmail = _config["SmtpSettings:SenderEmail"] ?? "no-reply@secureplatform.com";

                if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser))
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"\n==========================================");
                    Console.WriteLine($"[SECURITY DEVICE APPROVAL] {email}");
                    Console.WriteLine($"[DEVICE] OS: {deviceOs}, Browser: {browser}, IP: {ipAddress}");
                    Console.WriteLine($"[APPROVAL LINK] -> {approvalLink}");
                    Console.WriteLine($"==========================================\n");
                    Console.ResetColor();
                    return true;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "SecurePlatform Security"),
                    Subject = "Action Required: New Device Login Attempt",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; max-width: 500px;'>
                            <h2 style='color: #333;'>Unrecognized Device Login</h2>
                            <p style='color: #666;'>We noticed a login attempt from a new device. Please confirm if this was you.</p>
                            <div style='background: #f8f9fa; padding: 15px; border-radius: 6px; margin: 20px 0;'>
                                <p><strong>OS:</strong> {deviceOs}</p>
                                <p><strong>Browser:</strong> {browser}</p>
                                <p><strong>IP Address:</strong> {ipAddress}</p>
                            </div>
                            <div style='text-align: center; margin: 30px 0;'>
                                <a href='{approvalLink}' style='background-color: #EE4D2D; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px; font-weight: bold; font-size: 16px;'>Approve Device</a>
                            </div>
                            <p style='font-size: 12px; color: #999;'>This link will expire in 10 minutes. If you did not make this request, we recommend changing your password immediately.</p>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(email);
                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to dispatch device approval email to {email}.");
                Console.WriteLine($"[FALLBACK DEVICE APPROVAL LINK] {email} -> {approvalLink}");
                return false;
            }
        }
    }
}