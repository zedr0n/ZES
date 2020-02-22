using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ZES.Infrastructure
{
    /// <summary>
    /// Hashing methods
    /// </summary>
    public static class Hashing
    {
        /// <summary>
        /// Sha256 string hash
        /// </summary>
        /// <param name="value">String to hash</param>
        /// <returns>String hash</returns>
        public static string Sha256(string value)
        {
            return Sha256(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// Sha256 string hash
        /// </summary>
        /// <param name="bytes">Object bytes to hash</param>
        /// <returns>String hash</returns>
        public static string Sha256(byte[] bytes)
        {
            var sb = new StringBuilder();

            using (var hash = SHA256.Create())
            {
                var result = hash.ComputeHash(bytes);
                
                foreach (var b in result)
                    sb.Append(b.ToString("x2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Sha256 object hash
        /// </summary>
        /// <param name="value">Object to hash</param>
        /// <returns>String hash</returns>
        public static string Sha256(object value)
        {
            if (value == null)
                return string.Empty;

            var bytes = Byte(value);
            return Sha256(bytes);
        }
        
        private static byte[] Byte(object value)
        {
            /*https://stackoverflow.com/questions/1446547/
              how-to-convert-an-object-to-a-byte-array-in-c-sharp*/
            using (var ms = new MemoryStream())
            {
                var bf = new BinaryFormatter();
                bf.Serialize(ms, value ?? "null");
                return ms.ToArray();
            }
        }
    }
}