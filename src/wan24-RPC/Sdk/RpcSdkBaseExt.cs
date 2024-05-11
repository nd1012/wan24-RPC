using wan24.Core;
using wan24.RPC.Processing;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Sdk
{
    /// <summary>
    /// Base class for a RPC SDK which uses a RPC processor that exports its internals using <see cref="IRpcProcessorInternals"/>
    /// </summary>
    /// <typeparam name="T">RPC processor type</typeparam>
    public abstract class RpcSdkBaseExt<T> : RpcSdkBase<T> where T : RpcProcessor, IRpcProcessorInternals
    {
        /// <summary>
        /// Constructor
        /// </summary>
        protected RpcSdkBaseExt() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="processor">RPC processor (will be disposed)</param>
        protected RpcSdkBaseExt(in T processor) : base(processor) { }

        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="priority">Priority (higher value will be processed faster)</param>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task SendMessageAsync(IRpcMessage message, int priority, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            using CancellationTokenSource? cts = timeout == default ? null : new(timeout);
            List<CancellationToken> tokens = [Cancellation.Token];
            if (!Equals(cancellationToken, default)) tokens.Add(cancellationToken);
            if (cts is not null) tokens.Add(cts.Token);
            using Cancellations cancellation = new([.. tokens]);
            await Processor.SendMessageAsync(message, priority, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task SendMessageAsync(IRpcMessage message, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            using CancellationTokenSource? cts = timeout == default ? null : new(timeout);
            List<CancellationToken> tokens = [Cancellation.Token];
            if (!Equals(cancellationToken, default)) tokens.Add(cancellationToken);
            if (cts is not null) tokens.Add(cts.Token);
            using Cancellations cancellation = new([.. tokens]);
            await Processor.SendMessageAsync(message, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Create a RPC stream parameter
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="disposeStream">Dispose the stream after use?</param>
        /// <returns>Parameter</returns>
        protected virtual RpcStreamParameter CreateStreamParameter(in Stream stream, in bool disposeStream = false)
        {
            EnsureInitialized();
            return Processor.CreateStreamParameter(stream, disposeStream);
        }

        /// <summary>
        /// Create an outgoing stream
        /// </summary>
        /// <param name="streamParameter">Parameter</param>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream value to send to the peer</returns>
        protected virtual async Task<RpcStreamValue> CreateOutgoingStreamAsync(
            RpcStreamParameter streamParameter, 
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
            return await Processor.CreateOutgoingStreamAsync(streamParameter, cancellationToken).DynamicContext();
        }
    }
}
