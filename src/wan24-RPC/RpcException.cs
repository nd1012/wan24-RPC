namespace wan24.RPC
{
    /// <summary>
    /// RPC remote exception (<see cref="Exception.InnerException"/> should serve the remote exception type)
    /// </summary>
    [Serializable]
    public class RpcException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public RpcException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public RpcException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public RpcException(string? message, Exception? inner) : base(message, inner) { }

        /// <summary>
        /// Remote exception type name
        /// </summary>
        public required string RemoteExceptionType { get; init; }
    }
}
