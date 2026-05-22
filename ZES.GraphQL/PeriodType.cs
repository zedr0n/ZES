using System;
using HotChocolate.Language;
using HotChocolate.Types;
using NodaTime;
using NodaTime.Text;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class PeriodType : ScalarType<Period, StringValueNode>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InstantType"/> class.
        /// </summary>
        public PeriodType() 
            : base("Period")
        {
        }

        /// <inheritdoc />
        public override IValueNode ParseResult(object value)
        {
            if (value == default)
            {
                return new NullValueNode(null);
            }

            if (value is Period period)
                return ParseValue(period);

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
                case string s when TryParsePeriod(s, out var period):
                    value = period;
                    return true;
                case Period i:
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

            return TryParsePeriod(literal.Value, out _);
        }

        /// <inheritdoc />
        protected override Period ParseLiteral(StringValueNode literal)
        {
            if (literal == null)
            {
                throw new ArgumentNullException(nameof(literal));
            }

            if (TryParsePeriod(literal.Value, out var period))
            {
                return period;
            }

            throw new SerializationException($"Cannot parse {Name} with type {literal.GetType()}", this);
        }

        /// <inheritdoc />
        protected override StringValueNode ParseValue(Period period)
        {
            return new StringValueNode(PeriodPattern.Roundtrip.Format(period));
        }

        private bool TryParsePeriod(string value, out Period period)
        {
            period = default;
            var result = PeriodPattern.Roundtrip.Parse(value);
            if (result.Success)
                period = result.Value;
            return result.Success;
        }
    }
}