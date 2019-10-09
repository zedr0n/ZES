using System;
using System.Threading;

namespace ZES.Infrastructure.Projections
{
    /// <summary>
    /// Cancellation token source refreshed on disposal
    /// </summary>
    public class RepeatableCancellationTokenSource
    {
        private readonly Func<CancellationTokenSource> _factory;
        private CancellationTokenSource _ctl;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepeatableCancellationTokenSource"/> class.
        /// </summary>
        public RepeatableCancellationTokenSource()
        {
            _factory = () => new CancellationTokenSource();
            _ctl = _factory();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RepeatableCancellationTokenSource"/> class.
        /// </summary>
        /// <param name="factory">Cancellation token source initialisation</param>
        public RepeatableCancellationTokenSource(Func<CancellationTokenSource> factory)
        {
            _factory = factory;
            _ctl = _factory();
        }

        /// <summary>Gets the <see cref="T:System.Threading.CancellationToken"></see> associated with this <see cref="T:System.Threading.CancellationTokenSource"></see>.</summary>
        /// <returns>The <see cref="T:System.Threading.CancellationToken"></see> associated with this <see cref="T:System.Threading.CancellationTokenSource"></see>.</returns>
        /// <exception cref="T:System.ObjectDisposedException">The token source has been disposed.</exception>
        public CancellationToken Token => _ctl.Token;

        /// <summary>Gets a value indicating whether cancellation has been requested for this <see cref="T:System.Threading.CancellationTokenSource"></see>.</summary>
        /// <returns>true if cancellation has been requested for this <see cref="T:System.Threading.CancellationTokenSource"></see>; otherwise, false.</returns>
        public bool IsCancellationRequested => _ctl.IsCancellationRequested;
        
        /// <summary>
        /// Dispose of the cancellation token source instance creating a new one
        /// </summary>
        public void Dispose()
        {
            _ctl = _factory();
        }

        /// <summary>Communicates a request for cancellation.</summary>
        /// <exception cref="T:System.ObjectDisposedException">This <see cref="T:System.Threading.CancellationTokenSource"></see> has been disposed.</exception>
        /// <exception cref="T:System.AggregateException">An aggregate exception containing all the exceptions thrown by the registered callbacks on the associated <see cref="T:System.Threading.CancellationToken"></see>.</exception>
        public void Cancel() => _ctl.Cancel();
    }
}