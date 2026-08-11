using FYP.Security;
using Microsoft.Extensions.Configuration;

namespace FYP.Services
{
    public interface IPaymentEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string encryptedText);
        string DecryptSafe(string value);
    }

    public class PaymentEncryptionService : IPaymentEncryptionService
    {
        private readonly AesGcmEncryption _aes;

        public PaymentEncryptionService(IConfiguration config)
        {
            var key = config["EncryptionSettings:ChatEncryptionKey"]
                      ?? throw new InvalidOperationException("EncryptionSettings:ChatEncryptionKey is missing from configuration.");
            _aes = new AesGcmEncryption(key);
        }

        /// <summary>
        /// Encrypts plain text using AES-256-GCM. Returns format: nonce:tag:ciphertext (all Base64).
        /// </summary>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            return _aes.Encrypt(plainText);
        }

        /// <summary>
        /// Decrypts AES-256-GCM ciphertext back to plain text.
        /// Throws if the data has been tampered with (authentication tag mismatch).
        /// </summary>
        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            return _aes.Decrypt(encryptedText);
        }

        /// <summary>
        /// Attempts to decrypt. If decryption fails (e.g., the value is old plain text
        /// that was stored before encryption was enabled), returns the original value unchanged.
        /// This ensures backward compatibility with existing database records.
        /// </summary>
        public string DecryptSafe(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            // Quick format check: AES-GCM encrypted values always contain exactly 2 colons
            // separating nonce:tag:ciphertext. Plain text like "pi_xxx" or "TOK-SESSION-xxx" won't match.
            if (!value.Contains(':') || value.Split(':').Length != 3)
                return value; // Not encrypted, return as-is

            try
            {
                return _aes.Decrypt(value);
            }
            catch
            {
                // Decryption failed — this is likely old plain-text data.
                // Return the original value so existing workflows are not broken.
                return value;
            }
        }
    }
}
