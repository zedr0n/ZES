using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using Force.Crc32;

#pragma warning disable SYSLIB0011

namespace ZES.Infrastructure
{
    /// <summary>
    /// Hashing methods
    /// </summary>
    public static class Hashing
    {
        private static List<string> _enumHash = new()
        {
            "3253407757",
            "2768625435",
            "1007455905",
            "1259060791",
            "3580832660",
            "2724731650",
            "996231864",
            "1281784366"
        };
        
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
        public static string Crc32(object value)
        {
            if (value == null)
                return string.Empty;

            return Crc32Algorithm.Compute(Byte(value)).ToString();
        }
        
        /// <summary>
        /// Crc32 object hash
        /// </summary>
        /// <param name="bytes">Bytes to hash</param>
        /// <returns>String hash</returns>
        public static string Crc32(byte[] bytes)
        {
            return bytes == null ? string.Empty : Crc32Algorithm.Compute(bytes).ToString();
        }

        /// <summary>
        /// Crc32 object hash
        /// </summary>
        /// <param name="value">String to hash</param>
        /// <returns>String hash</returns>
        public static string Crc32(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var bytes = Encoding.UTF8.GetBytes(value);
            return Crc32Algorithm.Compute(bytes).ToString();
        }

        /// <summary>
        /// Crc32 object hash
        /// </summary>
        /// <param name="value">String to hash</param>
        /// <returns>Double to hash</returns>
        public static string Crc32(double value)
        {
            var bytes = BitConverter.GetBytes(value);
            return Crc32Algorithm.Compute(bytes).ToString();
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

        private static byte[] Byte(IEnumerable<object> state)
        {
            return state.SelectMany(Byte).ToArray();
        }

        private static byte[] Byte(string str) => Encoding.UTF8.GetBytes(str);
        private static byte[] Byte(double value) => BitConverter.GetBytes(value);
        private static byte[] Byte(Enum @enum) => new[] { Convert.ToByte(@enum) };
        
        private static byte[] Byte(object value)
        {
            /*https://stackoverflow.com/questions/1446547/
              how-to-convert-an-object-to-a-byte-array-in-c-sharp*/
            
            var bytes = value switch
            {
                IEnumerable<object> enumerable => Byte(enumerable),
                string str => Byte(str),
                double val => Byte(val),
                Enum @enum => Byte(@enum),
                _ => null
            };

            if (bytes != null)
                return bytes;

            using var ms = new MemoryStream();
            
            var bf = new BinaryFormatter();
            bf.Serialize(ms, value ?? "null");
            return ms.ToArray();
        }
    }
}