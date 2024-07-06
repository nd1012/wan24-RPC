namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// Thrown on too many RPC scopes (<see cref="RpcProcessorOptions.IncomingMessageQueue"/>)
    /// </summary>
    [Serializable]
    public class TooManyRpcScopesException : OutOfMemoryException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public TooManyRpcScopesException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public TooManyRpcScopesException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public TooManyRpcScopesException(string? message, Exception? inner) : base(message, inner) { }
    }
}
