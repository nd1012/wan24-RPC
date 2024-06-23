using wan24.RPC.Processing.Messages.Scopes;

namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// Thrown if a message for an unknown scope was received
    /// </summary>
    public class RpcScopeNotFoundException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public RpcScopeNotFoundException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public RpcScopeNotFoundException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public RpcScopeNotFoundException(string? message, Exception? inner) : base(message, inner) { }

        /// <summary>
        /// Scope message
        /// </summary>
        public required IRpcScopeMessage ScopeMessage { get; init; }
    }
}
