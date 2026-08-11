using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace FYP.Services
{
    public interface IPaymentSecurityService
    {
        string GeneratePaymentToken(string userId);
        bool ValidatePaymentToken(string token, string userId, out string nonce);
    }

    public class PaymentSecurityService : IPaymentSecurityService
    {
        private static readonly ConcurrentDictionary<string, byte> _activeNonces = new();
        private readonly IMemoryCache _cache;
        private readonly string _secretKey;

        public PaymentSecurityService(IMemoryCache cache, IConfiguration config)
        {
            _cache = cache;
            _secretKey = config["EncryptionSettings:ChatEncryptionKey"] ?? "DefaultFallbackSecretKey123!";
        }

        public string GeneratePaymentToken(string userId)
        {
            // 1. Generate Nonce
            var nonceBytes = new byte[16];
            RandomNumberGenerator.Fill(nonceBytes);
            var nonce = Convert.ToBase64String(nonceBytes);

            // 2. Timestamp
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 3. Cryptographic Binding: Sign nonce + timestamp + userId
            var payload = $"{nonce}:{timestamp}:{userId}";
            var signature = GenerateSignature(payload);

            return $"{nonce}.{timestamp}.{signature}";
        }

        public bool ValidatePaymentToken(string token, string userId, out string nonce)
        {
            nonce = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
                return false;

            var parts = token.Split('.');
            if (parts.Length != 3)
                return false;

            var extractedNonce = parts[0];
            var timestampStr = parts[1];
            var signature = parts[2];

            // 1. Validate Timestamp (Time-To-Live = 5 minutes)
            if (!long.TryParse(timestampStr, out long timestamp))
                return false;

            var timeSent = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            var timeNow = DateTimeOffset.UtcNow;
            if ((timeNow - timeSent).TotalMinutes > 5 || (timeNow - timeSent).TotalMinutes < -1)
            {
                return false; // Token expired or timestamp in future
            }

            // 2. Validate Cryptographic Binding Signature
            var expectedPayload = $"{extractedNonce}:{timestampStr}:{userId}";
            var expectedSignature = GenerateSignature(expectedPayload);
            if (signature != expectedSignature)
                return false;

            // 3. Prevent Replay Attacks using atomic micro-lock
            if (!_activeNonces.TryAdd(extractedNonce, 1))
            {
                return false; // Someone else is currently validating this nonce!
            }

            try
            {
                // Cache key is the nonce. If it exists, it's a replay attack.
                var cacheKey = $"PaymentNonce_{extractedNonce}";
                if (_cache.TryGetValue(cacheKey, out _))
                {
                    return false; // Nonce already used! Replay attack detected.
                }

                // Store nonce in cache with 5 minute expiration (matching token TTL)
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(5));
            }
            finally
            {
                _activeNonces.TryRemove(extractedNonce, out _);
            }

            nonce = extractedNonce;
            return true;
        }

        private string GenerateSignature(string payload)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(hash);
        }
    }
}
