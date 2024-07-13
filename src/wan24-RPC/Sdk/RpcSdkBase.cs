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
    public abstract class RpcSdkBase<T> : DisposableBase, IRpcSdk where T : RpcProcessor
    {
        /// <summary>
        /// Cancellation
        /// </summary>
        protected readonly CancellationTokenSource Cancellation = new();
        /// <summary>
        /// If to dispose the <see cref="Processor"/> when disposing
        /// </summary>
        protected bool DisposeProcessor = true;
        /// <summary>
        /// RPC processor (will be disposed)
        /// </summary>
        protected T? _Processor = null;

        /// <summary>
        /// Constructor
        /// </summary>
        protected RpcSdkBase() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processor">RPC processor (will be disposed)</param>
        protected RpcSdkBase(in T processor) : base() =>  Processor = processor;

        /// <summary>
        /// RPC processor (will be disposed)
        /// </summary>
        protected virtual T? Processor
        {
            get => _Processor;
            set
            {
                EnsureUndisposed();
                if (value == _Processor)
                    return;
                if (_Processor is not null)
                    _Processor.OnDisposed -= HandleProcessorDisposing;
                _Processor = value;
                if (value is not null)
                    value.OnDisposing += HandleProcessorDisposing;
            }
        }

        /// <summary>
        /// Close and dispose
        /// </summary>
        /// <param name="code">Close reason code</param>
        /// <param name="info">Close reason information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task CloseAndDisposeAsync(int code = 0, string? info = null, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            EnsureInitialized();
            await Processor.CloseAsync(code, info, cancellationToken).DynamicContext();
            await DisposeAsync().DynamicContext();
        }

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value (may throw after the remote execution)
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="returnValueType">Return value type</param>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        /// <returns>Return value (should be disposed, if possible)</returns>
        protected virtual async Task<object?> CallValueAsync(
            string? api,
            string method,
            Type returnValueType,
            TimeSpan timeout = default,
            CancellationToken cancellationToken = default,
            params object?[] parameters
            )
        {
            EnsureInitialized();
            using CancellationTokenSource? cts = timeout == default ? null : new(timeout);
            List<CancellationToken> tokens = [Cancellation.Token];
            if (!Equals(cancellationToken, default)) tokens.Add(cancellationToken);
            if (cts is not null) tokens.Add(cts.Token);
            using Cancellations cancellation = new([.. tokens]);
            return await Processor.CallValueAsync(api, method, returnValueType, cancellation, parameters).DynamicContext();
        }

        /// <summary>
        /// Call a RPC API method at the peer and wait for feedback (may throw after the remote execution)
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        protected virtual async Task CallVoidAsync(
            string? api,
            string method,
            TimeSpan timeout = default,
            CancellationToken cancellationToken = default,
            params object?[] parameters
            )
        {
            EnsureInitialized();
            using CancellationTokenSource? cts = timeout == default ? null : new(timeout);
            List<CancellationToken> tokens = [Cancellation.Token];
            if (!Equals(cancellationToken, default)) tokens.Add(cancellationToken);
            if (cts is not null) tokens.Add(cts.Token);
            using Cancellations cancellation = new([.. tokens]);
            await Processor.CallVoidAsync(api, method, cancellation, parameters).DynamicContext();
        }

        /// <summary>
        /// Send a ping request
        /// </summary>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task PingAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            using CancellationTokenSource cts = new(timeout);
            List<CancellationToken> tokens = [Cancellation.Token, cts.Token];
            if (!Equals(cancellationToken, default)) tokens.Add(cancellationToken);
            using Cancellations cancellation = new([.. tokens]);
            await Processor.PingAsync(timeout, cancellation).DynamicContext();
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
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task RaiseEventAsync(
            string name,
            EventArgs? e = null,
            bool wait = false,
            TimeSpan timeout = default,
            CancellationToken cancellationToken = default
            )
        {
            EnsureInitialized();
            using CancellationTokenSource? cts = timeout == default ? null : new(timeout);
            List<CancellationToken> tokens = [Cancellation.Token];
            if (!Equals(cancellationToken, default)) tokens.Add(cancellationToken);
            if (cts is not null) tokens.Add(cts.Token);
            using Cancellations cancellation = new([.. tokens]);
            await Processor.RaiseEventAsync(name, e, wait, cancellation).DynamicContext();
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

        /// <summary>
        /// Handle a disposing RPC processor
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Arguments</param>
        protected virtual void HandleProcessorDisposing(IDisposableObject sender, EventArgs e)
        {
            if(sender is RpcProcessor processor)
                processor.OnDisposing -= HandleProcessorDisposing;
            if (!IsDisposing)
                Cancellation.Cancel();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Cancellation.Cancel();
            if (_Processor is RpcProcessor processor)
            {
                processor.OnDisposing -= HandleProcessorDisposing;
                if (DisposeProcessor)
                    processor.Dispose();
            }
            Cancellation.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            await Cancellation.CancelAsync().DynamicContext();
            if (_Processor is RpcProcessor processor)
            {
                processor.OnDisposing -= HandleProcessorDisposing;
                if (DisposeProcessor)
                    await processor.DisposeAsync().DynamicContext();
            }
            Cancellation.Dispose();
        }
    }
}
