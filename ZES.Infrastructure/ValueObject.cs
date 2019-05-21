using System.Collections.Generic;
using System.Linq;

namespace ZES.Infrastructure
{
    /// <summary>
    /// DDD Value object base class
    /// </summary>
    public abstract class ValueObject
    {
        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            var other = (ValueObject)obj;
            var thisValues = GetAtomicValues().GetEnumerator();
            var otherValues = other.GetAtomicValues().GetEnumerator();
            while (thisValues.MoveNext() && otherValues.MoveNext())
            {
                if (ReferenceEquals(thisValues.Current, null) ^
                    ReferenceEquals(otherValues.Current, null))
                {
                    return false;
                }

                if (thisValues.Current != null &&
                    !thisValues.Current.Equals(otherValues.Current))
                {
                    return false;
                }
            }
            
            var b = !thisValues.MoveNext() && !otherValues.MoveNext();
            thisValues.Dispose();
            otherValues.Dispose();
            return b;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return GetAtomicValues()
                .Select(x => x != null ? x.GetHashCode() : 0)
                .Aggregate((x, y) => x ^ y);
        }
        
        /// <summary>
        /// Provides equal comparison for two value objects
        /// </summary>
        /// <param name="left">Compare object 1</param>
        /// <param name="right">Compare object 2</param>
        /// <returns>True is objects are equal</returns>
        protected static bool EqualOperator(ValueObject left, ValueObject right)
        {
            if (ReferenceEquals(left, null) ^ ReferenceEquals(right, null))
            {
                return false;
            }
            
            return ReferenceEquals(left, null) || left.Equals(right);
        }

        /// <summary>
        /// Provides equal comparison for two value objects
        /// </summary>
        /// <param name="left">Compare object 1</param>
        /// <param name="right">Compare object 2</param>
        /// <returns>True is objects are equal</returns>
        protected static bool NotEqualOperator(ValueObject left, ValueObject right)
        {
            return !EqualOperator(left, right);
        }

        /// <summary>
        /// Atomic attributes of the value object
        /// </summary>
        /// <returns>Atomic enumerator</returns>
        protected abstract IEnumerable<object> GetAtomicValues();
    }
}