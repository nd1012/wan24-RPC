namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// Thrown on too many incoming RPC messages while keep alive is being used (<see cref="RpcProcessorOptions.IncomingMessageQueue"/>)
    /// </summary>
    [Serializable]
    public class TooManyRpcMessagesException : OutOfMemoryException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public TooManyRpcMessagesException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public TooManyRpcMessagesException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public TooManyRpcMessagesException(string? message, Exception? inner) : base(message, inner) { }
    }
}
