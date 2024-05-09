using System.Diagnostics.CodeAnalysis;
using wan24.Core;
using wan24.RPC.Processing;

namespace wan24.RPC.Sdk
{
    /// <summary>
    /// Base class for a RPC SDK
    /// </summary>
    public abstract class RpcSdkBase : RpcSdkBase<RpcProcessor>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        protected RpcSdkBase() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processor">RPC processor (will be disposed)</param>
        protected RpcSdkBase(in RpcProcessor processor) : base(processor) { }
    }

    /// <summary>
    /// Base class for a RPC SDK
    /// </summary>
    /// <typeparam name="T">RPC processor type</typeparam>
    public abstract class RpcSdkBase<T> : DisposableBase where T : RpcProcessor
    {
        /// <summary>
        /// If to dispose the <see cref="Processor"/> when disposing
        /// </summary>
        protected bool DisposeProcessor = true;

        /// <summary>
        /// Constructor
        /// </summary>
        protected RpcSdkBase() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processor">RPC processor (will be disposed)</param>
        protected RpcSdkBase(in T processor) : base() => Processor = processor;

        /// <summary>
        /// RPC processor (will be disposed)
        /// </summary>
        protected virtual T? Processor { get; set; }

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value (may throw after the remote execution)
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="returnValueType">Return value type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        /// <returns>Return value (should be disposed, if possible)</returns>
        protected virtual Task<object?> CallValueAsync(
            string? api,
            string method,
            Type returnValueType,
            CancellationToken cancellationToken = default,
            params object?[] parameters
            )
        {
            EnsureInitialized();
            return Processor.CallValueAsync(api, method, returnValueType, cancellationToken, parameters);
        }

        /// <summary>
        /// Call a RPC API method at the peer and wait for feedback (may throw after the remote execution)
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        protected virtual Task CallVoidAsync(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
        {
            EnsureInitialized();
            return Processor.CallVoidAsync(api, method, cancellationToken, parameters);
        }

        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <typeparam name="tReturn">Event arguments type</typeparam>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        protected virtual RpcEvent RegisterEvent<tReturn>(in string name, in RpcEvent.EventHandler_Delegate handler) where tReturn : EventArgs
        {
            EnsureInitialized();
            return Processor.RegisterEvent<tReturn>(name, handler);
        }

        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        protected virtual RpcEvent RegisterEvent(in string name, in RpcEvent.EventHandler_Delegate handler)
        {
            EnsureInitialized();
            return Processor.RegisterEvent(name, handler);
        }

        /// <summary>
        /// Raise an event at the peer (if waiting may throw after the remote execution)
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="e">Event arguments (won't be diposed)</param>
        /// <param name="wait">Wait for remote event handlers to finish?</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return Processor.RaiseEventAsync(name, e, wait, cancellationToken);
        }

        /// <summary>
        /// Ensure being initialized
        /// </summary>
        [MemberNotNull(nameof(Processor))]
        protected virtual void EnsureInitialized()
        {
            if (Processor is null)
                throw new InvalidOperationException("Missing RPC processor");
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (DisposeProcessor)
                Processor?.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            if (DisposeProcessor && Processor is not null)
                await Processor.DisposeAsync().DynamicContext();
        }
    }
}
