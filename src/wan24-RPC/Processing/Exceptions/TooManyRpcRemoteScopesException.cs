namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// Thrown on too many RPC remote scopes (<see cref="RpcProcessorOptions.IncomingMessageQueue"/>)
    /// </summary>
    [Serializable]
    public class TooManyRpcRemoteScopesException : OutOfMemoryException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public TooManyRpcRemoteScopesException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public TooManyRpcRemoteScopesException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public TooManyRpcRemoteScopesException(string? message, Exception? inner) : base(message, inner) { }
    }
}
