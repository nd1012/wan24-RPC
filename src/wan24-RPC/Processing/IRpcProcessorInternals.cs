using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// Interface for a RPC processor which exports its internals
    /// </summary>
    public interface IRpcProcessorInternals
    {
        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="priority">Priority (higher value will be processed faster)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendMessageAsync(IRpcMessage message, int priority, CancellationToken cancellationToken = default);
        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendMessageAsync(IRpcMessage message, CancellationToken cancellationToken = default);
        /// <summary>
        /// Create a RPC stream parameter
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="disposeStream">Dispose the stream after use?</param>
        /// <returns>Parameter</returns>
        RpcStreamParameter CreateStreamParameter(in Stream stream, in bool disposeStream = false);
        /// <summary>
        /// Create an outgoing stream
        /// </summary>
        /// <param name="streamParameter">Parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream value to send to the peer</returns>
        Task<RpcStreamValue> CreateOutgoingStreamAsync(RpcStreamParameter streamParameter, CancellationToken cancellationToken = default);
    }
}
