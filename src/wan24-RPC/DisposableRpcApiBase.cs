using wan24.Core;

namespace wan24.RPC
{
    /// <summary>
    /// Base type for a disposable RPC API
    /// </summary>
    public abstract class DisposableRpcApiBase : DisposableBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        protected DisposableRpcApiBase() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="asyncDisposing">Asynchronous disposing?</param>
        /// <param name="allowFinalizer">Don't count running the finalizer as an error?</param>
        protected DisposableRpcApiBase(in bool asyncDisposing, in bool allowFinalizer = false) : base(asyncDisposing, allowFinalizer) { }

        /// <inheritdoc/>
        [NoRpc]
        public override bool Equals(object? obj) => base.Equals(obj);

        /// <inheritdoc/>
        [NoRpc]
        public override int GetHashCode() => base.GetHashCode();

        /// <inheritdoc/>
        [NoRpc]
        public override string? ToString() => base.ToString();

        /// <inheritdoc/>
        [NoRpc]
        new public void Dispose() => base.Dispose();

        /// <inheritdoc/>
        [NoRpc]
        new public ValueTask DisposeAsync() => base.DisposeAsync();
    }
}
