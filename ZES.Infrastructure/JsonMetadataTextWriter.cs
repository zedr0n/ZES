using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace ZES.Infrastructure;

/// <inheritdoc />
public class JsonMetadataTextWriter: JsonTextWriter
{
    private readonly TextWriter _textWriter;
    
    /// <inheritdoc />
    public JsonMetadataTextWriter(TextWriter textWriter) : base(textWriter)
    {
        _textWriter = textWriter;
    }

    /// <inheritdoc />
    public override void WritePropertyName(string name, bool escape)
    {
        if (escape)
            base.WritePropertyName(name);
        else
        {
            WritePropertyName(name);
        }
    }

    /// <inheritdoc />
    public override void WritePropertyName(string name)
    {
        SetWriteState(JsonToken.PropertyName, name);
        _textWriter.Write(name);
    }
}