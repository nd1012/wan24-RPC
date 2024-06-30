﻿using System.Diagnostics.CodeAnalysis;
using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// Interface for a RPC scope which exports its internals
    /// </summary>
    public interface IRpcScopeInternals
    {
        /// <summary>
        /// Create a message ID
        /// </summary>
        /// <returns>Message ID</returns>
        long CreateMessageId();
        /// <summary>
        /// Send a request
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SendVoidRequestAsync(IRpcRequest message, CancellationToken cancellationToken = default);
        /// <summary>
        /// Send a request
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        Task<T?> SendRequestNullableAsync<T>(IRpcRequest message, CancellationToken cancellationToken = default);
        /// <summary>
        /// Send a request
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        [return: NotNull]
        Task<T> SendRequestAsync<T>(IRpcRequest message, CancellationToken cancellationToken = default);
        /// <summary>
        /// Send a request
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="returnType">Return value type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        Task<object?> SendRequestNullableAsync(IRpcRequest message, Type returnType, CancellationToken cancellationToken = default);
        /// <summary>
        /// Send a request
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="returnType">Return value type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        Task<object> SendRequestAsync(IRpcRequest message, Type returnType, CancellationToken cancellationToken = default);
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
