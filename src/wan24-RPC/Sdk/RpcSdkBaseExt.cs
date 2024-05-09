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
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual Task SendMessageAsync(IRpcMessage message, int priority, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return Processor.SendMessageAsync(message, priority, cancellationToken);
        }

        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual Task SendMessageAsync(IRpcMessage message, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return Processor.SendMessageAsync(message, cancellationToken);
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
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream value to send to the peer</returns>
        protected virtual Task<RpcStreamValue> CreateOutgoingStreamAsync(RpcStreamParameter streamParameter, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return Processor.CreateOutgoingStreamAsync(streamParameter, cancellationToken);
        }
    }
}
