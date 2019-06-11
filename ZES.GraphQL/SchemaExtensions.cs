using System.Linq;

namespace ZES.GraphQL
{
    /// <summary>
    /// String extensions for schemas
    /// </summary>
    public static class SchemaExtensions
    {
        /// <summary>
        /// Mutations should start with lowercase for GraphQL
        /// </summary>
        /// <param name="s">Field string</param>
        /// <returns>Lowercase starting string</returns>
        public static string ToLowerFirst(this string s)
        {
            if (s.Length < 1)
                return s;
            var lower = s[0].ToString().ToLower();
            s = s.Remove(0, 1);
            return new string(s.Prepend(lower[0]).ToArray());
        }
        
        /*public static IEnumerable<(string type, string field)> IgnoredFields(this Schema s)
        {
            var result = new List<(string type, string field)>();
            foreach (var type in s.Types)
            {
                var clrType = type.ToClrType();
                if (!clrType.GetInterfaces().Contains(typeof(IMessage)))
                    continue;
                if ( clrType.GetProperty(nameof(IMessage.AncestorId)) != null )
                   result.Add((type.Name.Value, nameof(IMessage.AncestorId).ToLowerFirst())); 
                if ( clrType.GetProperty(nameof(IMessage.MessageId)) != null )
                    result.Add((type.Name.Value, nameof(IMessage.MessageId).ToLowerFirst())); 
                if ( clrType.GetProperty(nameof(IMessage.Position)) != null )
                    result.Add((type.Name.Value, nameof(IMessage.Position).ToLowerFirst())); 
                if ( clrType.GetProperty(nameof(IMessage.Timestamp)) != null )
                    result.Add((type.Name.Value, nameof(IMessage.Timestamp).ToLowerFirst())); 
            }

            return result;
        }*/
    }
}