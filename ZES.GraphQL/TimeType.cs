using System;
using HotChocolate.Language;
using HotChocolate.Types;
using NodaTime;
using NodaTime.Text;
using ZES.Interfaces.Clocks;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class TimeType : ScalarType<Time, StringValueNode>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeType"/> class.
        /// </summary>
        public TimeType() 
            : base("Time")
        {
        }

        /// <inheritdoc />
        public override IValueNode ParseResult(object resultValue) => ParseValue(resultValue);

        /// <inheritdoc />
        public override bool TrySerialize(object runtimeValue, out object resultValue)
        {
            resultValue = null;
            if (runtimeValue is not Time time)
                throw new SerializationException($"Cannot serialize {Name}", this);
            
            resultValue = time.ToExtendedIso();
            return true;
        }

        /// <inheritdoc />
        public override bool TryDeserialize(object serialized, out object value)
        {
            if (serialized is null)
            {
                value = null;
                return true;
            }

            if (serialized is string s && TryParseTime(s, out var time))
            {
                value = time;
                return true;
            }

            if (serialized is Time i)
            {
                value = i;
                return true;
            }

            value = null;
            return false;
        }

        /// <inheritdoc />
        protected override bool IsInstanceOfType(StringValueNode literal)
        {
            if (literal == null)
            {
                throw new ArgumentNullException(nameof(literal));
            }

            return TryParseTime(literal.Value, out _);
        }

        /// <inheritdoc />
        protected override Time ParseLiteral(StringValueNode literal)
        {
            if (literal == null)
            {
                throw new ArgumentNullException(nameof(literal));
            }

            if (TryParseTime(literal.Value, out var time))
                return time;

            throw new SerializationException($"Cannot parse {Name} with type {literal.GetType()}", this);
        }

        /// <inheritdoc />
        protected override StringValueNode ParseValue(Time value)
        {
            if (value == default)
            {
                return new StringValueNode(string.Empty);
            }

            return new StringValueNode(value.ToExtendedIso());
        }

        private bool TryParseTime(string value, out Time time)
        {
            time = Time.FromExtendedIso(value);
            return time != default;
        }
    }
}