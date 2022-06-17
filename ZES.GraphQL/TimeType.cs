using System;
using HotChocolate.Language;
using HotChocolate.Types;
using NodaTime;
using NodaTime.Text;
using ZES.Interfaces.Clocks;

namespace ZES.GraphQL
{
    /// <inheritdoc />
    public class TimeType : ScalarType
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeType"/> class.
        /// </summary>
        public TimeType() 
            : base("Time")
        {
        }

        /// <inheritdoc />
        public override Type ClrType => typeof(Time);

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
                && TryParseTime(stringLiteral.Value, out _))
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
                && TryParseTime(stringLiteral.Value, out var time))
            {
                return time;
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

            if (value is Time time)
            {
                return new StringValueNode(time.ToExtendedIso());
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

            if (value is Time time)
            {
                return time;
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

        private bool TryParseTime(string value, out Time time)
        {
            time = Time.FromExtendedIso(value);
            return time != default;
        }
    }
}