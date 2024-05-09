using wan24.Core;

namespace wan24.RPC.Processing.Parameters
{
    /// <summary>
    /// RPC enumeration parameter
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public record class RpcEnumerationParameter<T>() : DisposableRecordBase(), IAsyncEnumerator<T>
    {
        /// <summary>
        /// Enumerator
        /// </summary>
        protected IEnumerator<T>? Enumerator = null;
        /// <summary>
        /// Asynchronous enumerator
        /// </summary>
        protected IAsyncEnumerator<T>? AsyncEnumerator = null;

        /// <summary>
        /// Enumerable
        /// </summary>
        public IEnumerable<T>? Enumerable { get; init; }

        /// <summary>
        /// Asynchronous enumerable
        /// </summary>
        public IAsyncEnumerable<T>? AsyncEnumerable { get; init; }

        /// <summary>
        /// If to dispose the <see cref="Enumerable"/>/<see cref="AsyncEnumerable"/> when disposing
        /// </summary>
        public bool DisposeEnumerable { get; init; }

        /// <summary>
        /// Cancellation
        /// </summary>
        public CancellationToken CancelToken { get; set; }

        /// <summary>
        /// If the enumeration has started
        /// </summary>
        public bool HasEnumerationStarted => Enumerator is not null || AsyncEnumerator is not null;

        /// <inheritdoc/>
        public virtual T Current
        {
            get
            {
                EnsureUndisposed();
                if (!HasEnumerationStarted)
                    throw new InvalidOperationException();
                return Enumerator is not null ? Enumerator.Current : AsyncEnumerator!.Current;
            }
        }

        /// <inheritdoc/>
        public virtual async ValueTask<bool> MoveNextAsync()
        {
            EnsureUndisposed();
            if (!HasEnumerationStarted)
                if(Enumerable is not null)
                {
                    Enumerator = Enumerable.GetEnumerator();
                }
                else
                {
                    AsyncEnumerator = AsyncEnumerable!.GetAsyncEnumerator(CancelToken);
                }
            return Enumerator is not null ? Enumerator.MoveNext() : await AsyncEnumerator!.MoveNextAsync().DynamicContext();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (DisposeEnumerable)
            {
                Enumerable?.TryDispose();
                AsyncEnumerable?.TryDispose();
            }
            Enumerator?.Dispose();
            AsyncEnumerator?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            if (DisposeEnumerable)
            {
                if (Enumerable is not null)
                    await Enumerable.TryDisposeAsync().DynamicContext();
                if (AsyncEnumerable is not null)
                    await AsyncEnumerable.TryDisposeAsync().DynamicContext();
            }
            Enumerator?.Dispose();
            if (AsyncEnumerator is not null)
                await AsyncEnumerator.DisposeAsync().DynamicContext();
        }
    }
}
