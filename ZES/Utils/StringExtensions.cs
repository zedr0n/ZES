namespace ZES.Utils
{
    /// <summary>
    /// String extensions
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Convert first character to lower case
        /// </summary>
        /// <param name="str">Input string</param>
        /// <returns>String with lower first character</returns>
        public static string FirstCharacterToLower(this string str)
        {
            if (string.IsNullOrEmpty(str) || char.IsLower(str, 0))
                return str;

            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}