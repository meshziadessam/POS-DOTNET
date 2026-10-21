using System;
using System.Security.Cryptography;
using System.Text;

namespace RestaurantPos
{
    public static class PinSecurity
    {
        public static bool Verify(string pin, string salt, string digest, int iterations)
        {
            try
            {
                if (iterations <= 0) return false;
                var expected = Convert.FromBase64String(digest);
                if (expected.Length != 32) return false;
                using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(pin ?? ""), Convert.FromBase64String(salt), iterations, HashAlgorithmName.SHA256))
                {
                    var actual = kdf.GetBytes(32);
                    int difference = 0;
                    for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
                    return difference == 0;
                }
            }
            catch (FormatException) { return false; }
            catch (ArgumentException) { return false; }
            catch (CryptographicException) { return false; }
        }
    }
}
