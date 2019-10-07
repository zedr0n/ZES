using System;
using System.Reactive.Disposables;
using System.Threading;

namespace ZES.Infrastructure.Projections
{
    /// <summary>
    /// Lazy-initialized subscription
    /// </summary>
    public class LazySubscription : IDisposable
    {
        private readonly Func<IDisposable> _factory;
        private Lazy<IDisposable> _subscription;

        /// <summary>
        /// Initializes a new instance of the <see cref="LazySubscription"/> class.
        /// </summary>
        public LazySubscription()
        {
            _factory = () => Disposable.Empty;
            _subscription = new Lazy<IDisposable>(_factory);
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="LazySubscription"/> class.
        /// </summary>
        /// <param name="factory">Subscription initialisation</param>
        public LazySubscription(Func<IDisposable> factory)
        {
            _factory = factory;
            _subscription = new Lazy<IDisposable>(_factory);
        }

        /// <summary>Gets a value indicating whether a value has been created for this <see cref="T:System.Lazy`1"></see> instance.</summary>
        /// <returns>true if a value has been created for this <see cref="T:System.Lazy`1"></see> instance; otherwise, false.</returns>
        public bool IsValueCreated => _subscription.IsValueCreated;

        /// <summary>
        /// Start the subscription
        /// </summary>
        /// <returns>Subscription instance</returns>
        public IDisposable Start()
        {
            Dispose();
            return _subscription.Value;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_subscription.IsValueCreated) 
                return;
            
            _subscription.Value.Dispose();
            _subscription = new Lazy<IDisposable>(_factory);
        }
    }

    public class RepeatableCancellationTokenSource
    {
        private readonly Func<CancellationTokenSource> _factory;
        private CancellationTokenSource _ctl;

        public RepeatableCancellationTokenSource()
        {
            _factory = () => new CancellationTokenSource();
            _ctl = _factory();
        }

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