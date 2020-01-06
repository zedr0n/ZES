using System;
using System.Security.Cryptography;
using System.Text;

namespace ZES.Infrastructure
{
    public static class Hashing
    {
        public static string Sha256(string value)
        {
            var sb = new StringBuilder();

            using (var hash = SHA256.Create())            
            {
                var enc = Encoding.UTF8;
                var result = hash.ComputeHash(enc.GetBytes(value));

                foreach (var b in result)
                    sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }    
    }
}