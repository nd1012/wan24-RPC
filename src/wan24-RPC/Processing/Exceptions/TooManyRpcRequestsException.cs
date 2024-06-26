﻿namespace wan24.RPC.Processing.Exceptions
{
    /// <summary>
    /// Thrown on too many RPC calls (<see cref="RpcProcessorOptions.CallQueue"/>)
    /// </summary>
    [Serializable]
    public class TooManyRpcRequestsException : OutOfMemoryException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public TooManyRpcRequestsException() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public TooManyRpcRequestsException(string? message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="inner">Inner exception</param>
        public TooManyRpcRequestsException(string? message, Exception? inner) : base(message, inner) { }
    }
}
