﻿using System;

namespace ZES.Interfaces.Stochastic
{
    /// <summary>
    /// Markov value for MDP
    /// </summary>
    public readonly record struct Value(double Mean = 0.0, double Variance = 0.0)
    {
        /*public class CdfFunction
        {
            private readonly List<double> _abscissas;
            private readonly Dictionary<double, double> _values; 

            public IEnumerable<double> Abscissas => _abscissas;

            private const int MIN_ABSCISSA = -50;
            private const int MAX_ABSCISSA = 50;
            private const int STEP = 1;
            private const double SCALE = 1000000;
            private const double EPS = 1;

            public CdfFunction()
            {
                var abscissca = MIN_ABSCISSA;
                while (abscissca < MAX_ABSCISSA && false)
                {
                    _values[abscissca] = 0.0;
                    abscissca += STEP;
                }
            }

            public double this[double x]
            {
                get
                {
                    var prevAbscissa = _abscissas.LastOrDefault(a => a <= x);
                    if (prevAbscissa == default)
                        return _values[_abscissas.First()];
                    return _values[prevAbscissa];
                }
                set
                {
                   if (!_abscissas.Contains(x))
                       throw new InvalidOperationException();
                   _values[x] = value;
                }
            }
        }*/
        
        /// <summary>
        /// Sum operator overload
        /// </summary>
        /// <param name="a">First value</param>
        /// <param name="b">Second value</param>
        /// <returns>Sum of the values</returns>
        public static Value operator +(Value a, Value b)
        {
            var temp = new Value(a.Mean + b.Mean, a.Variance + b.Variance);
            return temp;
        }
        
        /// <summary>
        /// Scale operator overload
        /// </summary>
        /// <param name="mult">Scaling multiplier</param>
        /// <param name="value">Target value</param>
        /// <returns>Scaled value</returns>
        public static Value operator *(double mult, Value value)
        {
            var temp = new Value(value.Mean * mult, value.Variance * mult);
            return temp;
        }

        /// <inheritdoc />
        public override string ToString() => $"{Mean}";
    }
}