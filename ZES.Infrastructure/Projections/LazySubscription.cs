using System;
using System.Reactive.Disposables;

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
}