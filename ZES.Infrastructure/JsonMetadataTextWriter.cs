using System.IO;
using Newtonsoft.Json;

namespace ZES.Infrastructure;

/// <inheritdoc />
public class JsonMetadataTextWriter: JsonTextWriter
{
    /// <inheritdoc />
    public JsonMetadataTextWriter(TextWriter textWriter) : base(textWriter)
    {
    }

    /// <inheritdoc />
    public override void WritePropertyName(string name, bool escape)
    {
        if (escape)
            base.WritePropertyName(name);
        else
        {
            SetWriteState(JsonToken.PropertyName, name); 
            WriteRaw(name);
        }
    }
}