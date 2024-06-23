using wan24.RPC.Processing.Options;

namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// Thrown on too many RPC cancellations (<see cref="StreamScopeOptions.MaxStreamContentLength"/>)
    /// </summary>
    [Serializable]
    public class TooManyRpcCancellationsException : OutOfMemoryException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public TooManyRpcCancellationsException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public TooManyRpcCancellationsException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public TooManyRpcCancellationsException(string? message, Exception? inner) : base(message, inner) { }
    }
}
