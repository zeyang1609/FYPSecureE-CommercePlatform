using System;
using System.Security.Cryptography;
using System.Text;

namespace FYP.Security
{
    public class AesGcmEncryption
    {
        private readonly byte[] _key;

        public AesGcmEncryption(string base64Key)
        {
            _key = Convert.FromBase64String(base64Key);
        }

        public string Encrypt(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            // 1. Generate a unique 12-byte Initialization Vector (IV) for this specific transaction[cite: 1]
            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);

            byte[] cipherBytes = new byte[plainBytes.Length];

            // 2. Prepare the 16-byte authentication tag
            byte[] tag = new byte[16];

            using (var aesGcm = new AesGcm(_key, tagSizeInBytes: 16))
            {
                // 3. Encrypt the data and generate the tamper-evident tag[cite: 1]
                aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
            }

            // 4. Combine the components into a single string separated by colons
            return Convert.ToBase64String(nonce) + ":" +
                   Convert.ToBase64String(tag) + ":" +
                   Convert.ToBase64String(cipherBytes);
        }


        public string Decrypt(string encryptedPayload)
        {
            string[] parts = encryptedPayload.Split(':');
            if (parts.Length != 3)
                throw new FormatException("Invalid encrypted payload format.");

            // Extract the components
            byte[] nonce = Convert.FromBase64String(parts[0]);
            byte[] tag = Convert.FromBase64String(parts[1]);
            byte[] cipherBytes = Convert.FromBase64String(parts[2]);

            byte[] plainBytes = new byte[cipherBytes.Length];

            using (var aesGcm = new AesGcm(_key, tagSizeInBytes: 16))
            {
                // 5. Decrypt the payload. If the tag doesn't match the cipherBytes perfectly, this crashes intentionally[cite: 1].
                aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
            }

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}