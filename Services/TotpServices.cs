using System;
using System.Security.Cryptography;
using System.Text;

namespace FYP.Services
{
    public class TotpService
    {
        private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>
        /// Generates a random 160-bit (20-byte) Base32 encoded secret key.
        /// </summary>
        public string GenerateSecretKey()
        {
            byte[] secretBytes = new byte[20];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(secretBytes);
            }
            return Base32Encode(secretBytes);
        }

        /// <summary>
        /// Generates the standard otpauth:// URI recognized by Google Authenticator, Authy, and Microsoft Authenticator.
        /// </summary>
        public string GenerateQrCodeUri(string userEmail, string secretKey, string issuer = "SecurePlatform")
        {
            string encodedIssuer = Uri.EscapeDataString(issuer);
            string encodedEmail = Uri.EscapeDataString(userEmail);
            return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={secretKey}&issuer={encodedIssuer}&digits=6&period=30";
        }

        /// <summary>
        /// Validates a user-submitted 6-digit TOTP code against the secret key.
        /// Accounts for ±1 time-step (30-second) clock drift tolerance.
        /// </summary>
        public bool VerifyCode(string secretBase32, string userCode, int timeDriftSteps = 1)
        {
            if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(userCode))
                return false;

            userCode = userCode.Trim().Replace(" ", "");
            if (userCode.Length != 6 || !int.TryParse(userCode, out _))
                return false;

            byte[] secretBytes = Base32Decode(secretBase32);
            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long currentTimeStep = currentUnixTime / 30;

            // Check current time step as well as previous/next steps to tolerate clock skew
            for (int i = -timeDriftSteps; i <= timeDriftSteps; i++)
            {
                string expectedCode = ComputeTotpCode(secretBytes, currentTimeStep + i);
                if (expectedCode == userCode)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// RFC 4226 HMAC-SHA1 Dynamic Truncation Calculation
        /// </summary>
        private string ComputeTotpCode(byte[] secretBytes, long timeStep)
        {
            byte[] timeBytes = BitConverter.GetBytes(timeStep);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(timeBytes); // RFC 4226 requires Big-Endian byte order
            }

            using var hmac = new HMACSHA1(secretBytes);
            byte[] hash = hmac.ComputeHash(timeBytes);

            // Dynamic Truncation
            int offset = hash[hash.Length - 1] & 0x0F;
            int binaryCode = ((hash[offset] & 0x7F) << 24)
                           | ((hash[offset + 1] & 0xFF) << 16)
                           | ((hash[offset + 2] & 0xFF) << 8)
                           | (hash[offset + 3] & 0xFF);

            int otp = binaryCode % 1000000;
            return otp.ToString("D6");
        }

        private static string Base32Encode(byte[] data)
        {
            StringBuilder result = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = data[0];
            int next = 1;
            int bitsLeft = 8;

            while (bitsLeft > 0 || next < data.Length)
            {
                if (bitsLeft < 5)
                {
                    if (next < data.Length)
                    {
                        buffer = (buffer << 8) | (data[next++] & 0xFF);
                        bitsLeft += 8;
                    }
                    else
                    {
                        int pad = 5 - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }

                int index = 0x1F & (buffer >> (bitsLeft - 5));
                bitsLeft -= 5;
                result.Append(Base32Chars[index]);
            }

            return result.ToString();
        }

        private static byte[] Base32Decode(string base32)
        {
            base32 = base32.TrimEnd('=').ToUpperInvariant();
            byte[] buffer = new byte[base32.Length * 5 / 8];
            int bufIndex = 0;
            int currentByte = 0;
            int bitsRemaining = 8;

            foreach (char c in base32)
            {
                int val = Base32Chars.IndexOf(c);
                if (val < 0) continue;

                if (bitsRemaining > 5)
                {
                    int mask = val << (bitsRemaining - 5);
                    currentByte |= mask;
                    bitsRemaining -= 5;
                }
                else
                {
                    int mask = val >> (5 - bitsRemaining);
                    currentByte |= mask;
                    buffer[bufIndex++] = (byte)currentByte;
                    currentByte = (val << (8 - (5 - bitsRemaining))) & 0xFF;
                    bitsRemaining = 8 - (5 - bitsRemaining);
                }
            }

            return buffer;
        }
    }
}