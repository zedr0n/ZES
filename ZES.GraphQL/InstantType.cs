using System;
using HotChocolate.Language;
using HotChocolate.Types;
using NodaTime;
using NodaTime.Text;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class InstantType : ScalarType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InstantType"/> class.
        /// </summary>
        public InstantType() 
            : base("Instant")
        {
        }

        /// <inheritdoc />
        public override Type ClrType => typeof(Instant);

        /// <inheritdoc />
        public override bool IsInstanceOfType(IValueNode literal)
        {
            if (literal == null)
            {
                throw new ArgumentNullException(nameof(literal));
            }

            if (literal is NullValueNode)
            {
                return true;
            }

            if (literal is StringValueNode stringLiteral
                && TryParseInstant(stringLiteral.Value, out _))
            {
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public override object ParseLiteral(IValueNode literal)
        {
            if (literal == null)
            {
                throw new ArgumentNullException(nameof(literal));
            }

            if (literal is NullValueNode)
            {
                return null;
            }

            if (literal is StringValueNode stringLiteral
                && TryParseInstant(stringLiteral.Value, out var instant))
            {
                return instant;
            }

            throw new ScalarSerializationException(
                $"Cannot parse {Name} with type {literal.GetType()}");
        }

        /// <inheritdoc />
        public override IValueNode ParseValue(object value)
        {
            if (value == default)
            {
                return new NullValueNode(null);
            }

            if (value is Instant instant)
            {
                return new StringValueNode(InstantPattern.ExtendedIso.Format(instant));
            }

            throw new ScalarSerializationException(
                $"Cannot parse {Name} with type {value.GetType()}");
        }

        /// <inheritdoc />
        public override object Serialize(object value)
        {
            if (value == default)
            {
                return null;
            }

            if (value is Instant instant)
            {
                return instant;
            }

            throw new ScalarSerializationException($"Cannot serialize {Name}");
        }

        /// <inheritdoc />
        public override bool TryDeserialize(object serialized, out object value)
        {
            if (serialized is null)
            {
                value = null;
                return true;
            }

            if (serialized is string s && TryParseInstant(s, out var instant))
            {
                value = instant;
                return true;
            }

            if (serialized is Instant i)
            {
                value = i;
                return true;
            }

            value = null;
            return false;
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