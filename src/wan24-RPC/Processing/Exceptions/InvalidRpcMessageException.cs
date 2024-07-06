using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// Thrown on invalid received RPC message
    /// </summary>
    [Serializable]
    public class InvalidRpcMessageException : OutOfMemoryException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public InvalidRpcMessageException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public InvalidRpcMessageException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public InvalidRpcMessageException(string? message, Exception? inner) : base(message, inner) { }

        /// <summary>
        /// Invalid RPC message
        /// </summary>
        public required IRpcMessage RpcMessage { get; init; }
    }
}
