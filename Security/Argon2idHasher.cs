using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace FYP.Security
{
    public class Argon2idHasher
    {
        /// <summary>
        /// Hashes a plaintext password using Argon2id and a unique salt.
        /// </summary>
        public static string HashPassword(string password)
        {
            // Generate a 16-byte cryptographically secure random salt
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = 1, // Project requirement
                Iterations = 1,          // Project requirement
                MemorySize = 47104       // 46 MiB converted to KB (46 * 1024)
            };

            byte[] hash = argon2.GetBytes(16);

            // Combine the salt and the hash into one byte array for database storage
            byte[] hashBytes = new byte[salt.Length + hash.Length];
            Array.Copy(salt, 0, hashBytes, 0, salt.Length);
            Array.Copy(hash, 0, hashBytes, salt.Length, hash.Length);

            // Return as a clean Base64 string
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Verifies a plaintext password against the stored database hash.
        /// </summary>
        public static bool VerifyHash(string password, string storedHash)
        {
            byte[] hashBytes = Convert.FromBase64String(storedHash);

            // Extract the 16-byte salt from the beginning of the stored string
            byte[] salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, salt.Length);

            // Extract the actual password hash
            byte[] expectedHash = new byte[hashBytes.Length - salt.Length];
            Array.Copy(hashBytes, salt.Length, expectedHash, 0, expectedHash.Length);

            // Re-hash the inputted password using the exact same extracted salt and parameters
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = 1,
                Iterations = 1,
                MemorySize = 47104
            };

            byte[] actualHash = argon2.GetBytes(16);

            // Mathematically compare the two hashes
            return actualHash.SequenceEqual(expectedHash);
        }
    }
}