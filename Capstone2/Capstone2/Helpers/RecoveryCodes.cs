using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Capstone2.Helpers
{
    public static class RecoveryCodes
    {
        private static readonly char[] CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        public static List<string> Generate(int count = 10, int length = 8)
        {
            var codes = new List<string>(count);
            Span<byte> buffer = stackalloc byte[length];

            for (int i = 0; i < count; i++)
            {
                RandomNumberGenerator.Fill(buffer);
                var sb = new StringBuilder(length + 1);
                for (int j = 0; j < length; j++)
                {
                    sb.Append(CodeChars[buffer[j] % CodeChars.Length]);
                }
                if (length >= 8)
                {
                    sb.Insert(4, '-');
                }
                codes.Add(sb.ToString());
            }
            return codes;
        }

        public static (string Hash, string Salt) HashSecret(string secret, int iterations = 100_000)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, iterations, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);
            return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
        }

        public static bool Verify(string secret, string base64Hash, string base64Salt, int iterations = 100_000)
        {
            byte[] salt = Convert.FromBase64String(base64Salt);
            using var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, iterations, HashAlgorithmName.SHA256);
            byte[] computed = pbkdf2.GetBytes(32);
            return CryptographicOperations.FixedTimeEquals(computed, Convert.FromBase64String(base64Hash));
        }
    }
}


