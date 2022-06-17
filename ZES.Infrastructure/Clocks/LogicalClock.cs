using System;
using ZES.Interfaces.Clocks;

namespace ZES.Infrastructure.Clocks
{
    /// <summary>
    /// HLC clock (https://cse.buffalo.edu/tech-reports/2014-04.pdf)
    /// </summary>
    public class LogicalClock : ILogicalClock
    {
        private readonly object _lock = new object();
        private readonly IPhysicalClock _physicalClock;
        
        private long _l = 0;
        private long _c = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogicalClock"/> class.
        /// </summary>
        /// <param name="physicalClock">Physical clock instance</param>
        public LogicalClock(IPhysicalClock physicalClock)
        {
            _physicalClock = physicalClock;
        }

        /// <inheritdoc />
        public Time GetCurrentInstant()
        {
            Sync();
            return new LogicalTime(_l, _c);
        }

        /// <inheritdoc />
        public void Sync()
        {
            lock (_lock)
            {
                var l = _l;
                var pt = _physicalClock.GetCurrentInstant().ToUnixTimeTicks();
                _l = pt >= l ? pt : l; // max (l, pt)
                if (_l == l)
                    _c++;
            }
        }

        /// <inheritdoc />
        public void Receive(Time received)
        {
            LogicalTime m; 
            switch (received)
            {
                case null:
                    return;
                case InstantTime instantTime:
                    m = new LogicalTime (instantTime.instant.ToUnixTimeTicks(), 0 );
                    break;
                case LogicalTime logicalTime:
                    m = logicalTime;
                    break;
                default:
                    throw new InvalidCastException("Time should be of type Logical or Instant");
            }
            
            lock (_lock)
            {
                var l = _l;
                Sync();              // l = max (l, pt )
                _l = l >= m.l ? l : m.l;    // l = max ( max(l,pt), m.l) = max(l,m.l,pt) 
                if (l == _l && l == m.l)
                    _c = _c >= m.c ? _c : m.c + 1;
                else if (l == _l)
                    _c++;
                else if (l == m.l)
                    _c = m.c + 1;
                else
                    _c = 0;
            }
        }
    }
}