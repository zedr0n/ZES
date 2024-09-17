using System;
using System.Collections;
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
    /// Provides various hashing methods.
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
        /// Computes the SHA-256 hash value of a string.
        /// </summary>
        /// <param name="value">The string to hash.</param>
        /// <returns>The SHA-256 hash value as a string.</returns>
        public static string Sha256(string value)
        {
            return Sha256(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// Computes the SHA256 hash of a byte array.
        /// </summary>
        /// <param name="bytes">The byte array to hash.</param>
        /// <returns>The computed SHA256 hash as a string.</returns>
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
        /// Computes the CRC32 hash value of an object.
        /// </summary>
        /// <param name="value">The object to hash.</param>
        /// <returns>The CRC32 hash value as a string.</returns>
        public static string Crc32(object value)
        {
            if (value == null)
                return string.Empty;

            return Crc32Algorithm.Compute(Byte(value)).ToString();
        }

        /// <summary>
        /// Calculates the CRC32 hash value of the provided byte array.
        /// </summary>
        /// <param name="bytes">The byte array to be hashed.</param>
        /// <returns>The CRC32 hash value as a string.</returns>
        public static string Crc32(byte[] bytes)
        {
            return bytes == null ? string.Empty : Crc32Algorithm.Compute(bytes).ToString();
        }

        /// <summary>
        /// Calculates the CRC32 checksum for the given string value.
        /// </summary>
        /// <param name="value">The string value to calculate the CRC32 checksum for.</param>
        /// <returns>The CRC32 checksum as a string.</returns>
        public static string Crc32(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var bytes = Encoding.UTF8.GetBytes(value);
            return Crc32Algorithm.Compute(bytes).ToString();
        }

        /// <summary>
        /// Calculates the Crc32 hash of a double value.
        /// </summary>
        /// <param name="value">The double value to hash.</param>
        /// <returns>The Crc32 hash as a string.</returns>
        public static string Crc32(double value)
        {
            var bytes = BitConverter.GetBytes(value);
            return Crc32Algorithm.Compute(bytes).ToString();
        }

        /// <summary>
        /// Computes the SHA256 hash value for the specified object.
        /// </summary>
        /// <param name="value">The object to hash.</param>
        /// <returns>The SHA256 hash value as a string.</returns>
        public static string Sha256(object value)
        {
            if (value == null)
                return string.Empty;

            var bytes = Byte(value);
            return Sha256(bytes);
        }

        /// <summary>
        /// Converts each element in the given state collection to a byte array and concatenates them into a single byte array.
        /// </summary>
        /// <param name="state">The collection of objects to convert to byte arrays.</param>
        /// <returns>A byte array that contains byte representations of the elements in the state collection.</returns>
        private static byte[] Byte(IEnumerable<object> state)
        {
            return state.SelectMany(Byte).ToArray();
        }

        /// <summary>
        /// Converts the specified string to an array of bytes using UTF-8 encoding.
        /// </summary>
        /// <param name="str">The string to convert to bytes.</param>
        /// <returns>An array of bytes representing the specified string.</returns>
        private static byte[] Byte(string str) => Encoding.UTF8.GetBytes(str);

        /// <summary>
        /// Returns the bytes representation of the specified double value.
        /// </summary>
        /// <param name="value">The double value to convert.</param>
        /// <returns>The byte array representing the specified double value.</returns>
        private static byte[] Byte(double value) => BitConverter.GetBytes(value);
        
        /// <summary>
        /// Returns the bytes representation of the specified integer value.
        /// </summary>
        /// <param name="value">The integer value to convert.</param>
        /// <returns>The byte array representing the specified integer value.</returns>
        private static byte[] Byte(int value) => BitConverter.GetBytes(value);
        
        /// <summary>
        /// Converts an Enum value to a byte array representation.
        /// </summary>
        /// <param name="enum">The Enum value to be converted.</param>
        /// <returns>A byte array representation of the Enum value.</returns>
        private static byte[] Byte(Enum @enum) => new[] { Convert.ToByte(@enum) };

        /// <summary>
        /// Converts an object into a byte array.
        /// </summary>
        /// <param name="value">The object to convert.</param>
        /// <returns>The byte array representation of the object.</returns>
        private static byte[] Byte(object value)
        {
            /*https://stackoverflow.com/questions/1446547/
              how-to-convert-an-object-to-a-byte-array-in-c-sharp*/
            
            var bytes = value switch
            {
                IEnumerable enumerable and not string => Byte(enumerable.Cast<object>()),
                string str => Byte(str),
                double val => Byte(val),
                Enum @enum => Byte(@enum),
                _ => null
            };

            if (bytes != null)
                return bytes;

            throw new InvalidOperationException("Serialization not supported for this type");
            
            // DEPRECATED
            /*using var ms = new MemoryStream();
            
            var bf = new BinaryFormatter();
            bf.Serialize(ms, value ?? "null");
            return ms.ToArray();*/
        }
    }
}