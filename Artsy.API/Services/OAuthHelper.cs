using System.Security.Cryptography;
using System.Text;

namespace Artsy.API.Services
{
    public static class OAuthHelper
    {
        public static string GenerateState()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string GenerateCodeVerifier()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Base64UrlEncode(bytes);
        }

        public static string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
