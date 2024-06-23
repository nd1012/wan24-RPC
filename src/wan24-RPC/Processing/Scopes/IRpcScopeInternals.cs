using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// Interface for a RPC scope which exports its internals
    /// </summary>
    public interface IRpcScopeInternals
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
    }
}
