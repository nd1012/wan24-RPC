namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// Thrown on too many RPC streams
    /// </summary>
    [Serializable]
    public class TooManyRpcStreamsException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public TooManyRpcStreamsException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public TooManyRpcStreamsException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public TooManyRpcStreamsException(string? message, Exception? inner) : base(message, inner) { }
    }
}
