using System;
using HotChocolate.Language;
using HotChocolate.Types;
using NodaTime;
using NodaTime.Text;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class InstantType : ScalarType<Instant, StringValueNode>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InstantType"/> class.
        /// </summary>
        public InstantType() 
            : base("Instant")
        {
        }

        /// <inheritdoc />
        public override IValueNode ParseResult(object value)
        {
            if (value == default)
            {
                return new NullValueNode(null);
            }

            if (value is Instant instant)
                return ParseValue(instant);

            throw new SerializationException($"Cannot parse {Name} with type {value.GetType()}", this);
        }

        /// <inheritdoc />
        public override bool TrySerialize(object runtimeValue, out object resultValue)
        {
            resultValue = null;
            if (runtimeValue is not Instant instant) 
                throw new SerializationException($"Cannot serialize {Name}", this);
            
            resultValue = instant;
            return true;
        }

        /// <inheritdoc />
        public override bool TryDeserialize(object serialized, out object value)
        {
            switch (serialized)
            {
                case null:
                    value = null;
                    return true;
                case string s when TryParseInstant(s, out var instant):
                    value = instant;
                    return true;
                case Instant i:
                    value = i;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        /// <inheritdoc />
        protected override bool IsInstanceOfType(StringValueNode literal)
        {
            if (literal == null)
            {
                throw new ArgumentNullException(nameof(literal));
            }

            return TryParseInstant(literal.Value, out _);
        }

        /// <inheritdoc />
        protected override Instant ParseLiteral(StringValueNode literal)
        {
            if (literal == null)
            {
                throw new ArgumentNullException(nameof(literal));
            }

            if (TryParseInstant(literal.Value, out var instant))
            {
                return instant;
            }

            throw new SerializationException($"Cannot parse {Name} with type {literal.GetType()}", this);
        }

        /// <inheritdoc />
        protected override StringValueNode ParseValue(Instant instant)
        {
            return new StringValueNode(InstantPattern.ExtendedIso.Format(instant));
        }

        private bool TryParseInstant(string value, out Instant instant)
        {
            instant = default;
            var result = InstantPattern.ExtendedIso.Parse(value);
            if (result.Success)
                instant = result.Value;
            return result.Success;
        }
    }
}