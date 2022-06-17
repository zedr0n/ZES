using System;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using ZES.Interfaces.Clocks;

namespace ZES.Infrastructure.Clocks
{
    /// <summary>
    /// Time JSON.NET convertor extensions
    /// </summary>
    public static class TimeExtensions
    {
        /// <summary>
        /// Configures Json.NET with everything required to properly serialize and deserialize Time types.
        /// </summary>
        /// <param name="settings">The existing settings to add Time converters to.</param>
        /// <returns>The original <paramref name="settings"/> value, for further chaining.</returns>
        public static JsonSerializerSettings ConfigureForTime(this JsonSerializerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            
            if (!Time.UseLogicalTime)
                settings.Converters.Add(new InstantTimeConverter());
            else
                settings.Converters.Add(new LogicalTimeConverter());
 
            // Disable automatic conversion of anything that looks like a date and time to BCL types.
            settings.DateParseHandling = DateParseHandling.None;

            // return to allow fluent chaining if desired
            return settings;
        }
        
        /// <summary>
        /// Configures Json.NET with everything required to properly serialize and deserialize Time types.
        /// </summary>
        /// <param name="serializer">The existing serializer to add Time converters to.</param>
        /// <returns>The original <paramref name="serializer"/> value, for further chaining.</returns>
        public static JsonSerializer ConfigureForTime(this JsonSerializer serializer)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }
            
            if (!Time.UseLogicalTime)
                serializer.Converters.Add(new InstantTimeConverter());
            else
                serializer.Converters.Add(new LogicalTimeConverter());

            // Disable automatic conversion of anything that looks like a date and time to BCL types.
            serializer.DateParseHandling = DateParseHandling.None;

            // return to allow fluent chaining if desired
            return serializer;
        }
    }

    /// <inheritdoc />
    public class InstantTimeConverter : JsonConverter
    {
        private readonly JsonConverter _instantConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="InstantTimeConverter"/> class.
        /// </summary>
        public InstantTimeConverter()
        {
            _instantConverter = NodaConverters.InstantConverter;
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var instantTime = value as InstantTime;
            _instantConverter.WriteJson(writer, instantTime.instant, serializer);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var instant = (Instant)_instantConverter.ReadJson(reader, typeof(Instant), existingValue, serializer);
            return new InstantTime(instant);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType) => objectType == typeof(Time) || objectType == typeof(InstantTime);
    }
    
    /// <inheritdoc />
    public class LogicalTimeConverter : JsonConverter
    {
        private readonly JsonConverter _instantConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogicalTimeConverter"/> class.
        /// </summary>
        public LogicalTimeConverter()
        {
            _instantConverter = NodaConverters.InstantConverter;
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var logicalTime = value as LogicalTime;
            var instant = Instant.FromUnixTimeTicks(logicalTime.l);
            _instantConverter.WriteJson(writer, instant, serializer);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var instant = (Instant)_instantConverter.ReadJson(reader, typeof(Instant), existingValue, serializer);
            return new LogicalTime(instant.ToUnixTimeTicks(), 0);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType) => objectType == typeof(Time) || objectType == typeof(LogicalTime);
    }
}