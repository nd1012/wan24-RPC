using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Serialization;

/*
 * Message priorities should be used to keep the RPC message handling workflow smooth. Wrong priorities can cause a dead lock in combination with exhausted buffers. 
 * A message priority may be a negative value also.
 */

namespace wan24.RPC.Processing
{
    // Outgoing
    public partial class RpcProcessor
    {
        /// <summary>
        /// Outgoing messages
        /// </summary>
        protected readonly OutgoingQueue OutgoingMessages;
        /// <summary>
        /// Stream writing thread synchronization
        /// </summary>
        protected readonly SemaphoreSync WriteSync = new()
        {
            Name = "RPC write synchronization"
        };
        /// <summary>
        /// Message ID
        /// </summary>
        protected long MessageId = 0;

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="returnValueType">Return value type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        /// <returns>Return value (should be disposed, if possible)</returns>
        public virtual async Task<object?> CallValueAsync(
            string? api, 
            string method, 
            Type returnValueType, 
            CancellationToken cancellationToken = default, 
            params object?[] parameters
            )
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} calling API \"{api}\" method \"{method}\" at the peer ({count} parameters)", ToString(), api, method, parameters.Length);
            return await SendRequestAsync(new RequestMessage()
            {
                PeerRpcVersion = Options.RpcVersion,
                Id = CreateMessageId(),
                Api = api,
                Method = method,
                Parameters = parameters.Length == 0
                    ? null
                    : parameters
            }, returnValueType, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        /// <returns>Return value (should be disposed, if possible)</returns>
        public virtual async Task<T?> CallValueAsync<T>(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
            => (T?)await CallValueAsync(api, method, typeof(T), cancellationToken, parameters).DynamicContext();

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters (won't be diposed)</param>
        public virtual async Task CallVoidAsync(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} calling API \"{api}\" void method \"{method}\" at the peer ({count} parameters)", ToString(), api, method, parameters.Length);
            await SendVoidRequestAsync(new RequestMessage()
            {
                PeerRpcVersion = Options.RpcVersion,
                Id = CreateMessageId(),
                Api = api,
                Method = method,
                Parameters = parameters.Length == 0
                    ? null
                    : parameters,
                WantsReturnValue = false
            }, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Send a ping message and wait for the pong message
        /// </summary>
        /// <param name="timeout">Timeout</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task PingAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} sending ping request", ToString());
            using CancellationTokenSource cts = new(timeout);
            List<CancellationToken> token = [cts.Token];
            if (!cancellationToken.IsEqualTo(default))
                token.Add(cancellationToken);
            using Cancellations cancellation = new([.. token]);
            DateTime now = DateTime.Now;
            await SendVoidRequestAsync(new PingMessage()
            {
                PeerRpcVersion = Options.RpcVersion,
                Id = CreateMessageId()
            }, cancellation).DynamicContext();
            TimeSpan runtime = DateTime.Now - now;
            MessageLoopDuration = runtime;
            Logger?.Log(LogLevel.Debug, "{this} got pong response after {runtime}", ToString(), runtime);
        }

        /// <summary>
        /// Create a message ID
        /// </summary>
        /// <returns>Message ID</returns>
        protected virtual long CreateMessageId()
        {
            EnsureUndisposed();
            return Interlocked.Increment(ref MessageId);
        }

        /// <summary>
        /// Send a request
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task SendVoidRequestAsync(IRpcRequest message, CancellationToken cancellationToken = default)
        {
            Request request = new()
            {
                Processor = this,
                Message = message,
                Cancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                Logger?.Log(LogLevel.Trace, "{this} storing request #{id}", ToString(), request.Id);
                if (!AddPendingRequest(request))
                    throw new InvalidProgramException($"Failed to store request #{request.Id} (double message ID)");
                try
                {
                    await SendMessageAsync(request.Message, Options.Priorities.Rpc, cancellationToken).DynamicContext();
                    await request.ProcessorCompletion.Task.DynamicContext();
                }
                finally
                {
                    RemovePendingRequest(request);
                }
            }
        }

        /// <summary>
        /// Send a request
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        protected virtual async Task<T?> SendRequestNullableAsync<T>(IRpcRequest message, CancellationToken cancellationToken = default)
        {
            Request request = new()
            {
                Processor = this,
                Message = message,
                ExpectedReturnType = typeof(T),
                Cancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                Logger?.Log(LogLevel.Trace, "{this} storing request #{id}", ToString(), request.Id);
                if (!AddPendingRequest(request))
                    throw new InvalidProgramException($"Failed to store request #{request.Id} (double message ID)");
                try
                {
                    await SendMessageAsync(request.Message, Options.Priorities.Rpc, cancellationToken).DynamicContext();
                    return (T?)await request.ProcessorCompletion.Task.DynamicContext();
                }
                finally
                {
                    RemovePendingRequest(request);
                }
            }
        }

        /// <summary>
        /// Send a request
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        [return: NotNull]
        protected virtual async Task<T> SendRequestAsync<T>(IRpcRequest message, CancellationToken cancellationToken = default)
        {
            Request request = new()
            {
                Processor = this,
                Message = message,
                ExpectedReturnType = typeof(T),
                Cancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                Logger?.Log(LogLevel.Trace, "{this} storing request #{id}", ToString(), request.Id);
                if (!AddPendingRequest(request))
                    throw new InvalidProgramException($"Failed to store request #{request.Id} (double message ID)");
                try
                {
                    await SendMessageAsync(request.Message, Options.Priorities.Rpc, cancellationToken).DynamicContext();
                    return (T)(await request.ProcessorCompletion.Task.DynamicContext() ?? throw new InvalidDataException("NULL was responded"));
                }
                finally
                {
                    RemovePendingRequest(request);
                }
            }
        }

        /// <summary>
        /// Send a request
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="returnType">Return value type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        protected virtual async Task<object?> SendRequestNullableAsync(IRpcRequest message, Type returnType, CancellationToken cancellationToken = default)
        {
            Request request = new()
            {
                Processor = this,
                Message = message,
                ExpectedReturnType = returnType,
                Cancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                Logger?.Log(LogLevel.Trace, "{this} storing request #{id}", ToString(), request.Id);
                if (!AddPendingRequest(request))
                    throw new InvalidProgramException($"Failed to store request #{request.Id} (double message ID)");
                try
                {
                    await SendMessageAsync(request.Message, Options.Priorities.Rpc, cancellationToken).DynamicContext();
                    return await request.ProcessorCompletion.Task.DynamicContext();
                }
                finally
                {
                    RemovePendingRequest(request);
                }
            }
        }

        /// <summary>
        /// Send a request
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="returnType">Return value type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Return value</returns>
        protected virtual async Task<object> SendRequestAsync(IRpcRequest message, Type returnType, CancellationToken cancellationToken = default)
        {
            Request request = new()
            {
                Processor = this,
                Message = message,
                ExpectedReturnType = returnType,
                Cancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                Logger?.Log(LogLevel.Trace, "{this} storing request #{id}", ToString(), request.Id);
                if (!AddPendingRequest(request))
                    throw new InvalidProgramException($"Failed to store request #{request.Id} (double message ID)");
                try
                {
                    await SendMessageAsync(request.Message, Options.Priorities.Rpc, cancellationToken).DynamicContext();
                    return await request.ProcessorCompletion.Task.DynamicContext() ?? throw new InvalidDataException("NULL was responded");
                }
                finally
                {
                    RemovePendingRequest(request);
                }
            }
        }

        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="priority">Priority (higher value will be processed faster)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task SendMessageAsync(IRpcMessage message, int priority, CancellationToken cancellationToken = default)
        {
            if (Equals(cancellationToken, default))
                cancellationToken = CancelToken;
            OutgoingQueue.QueuedMessage queuedMessage = new()
            {
                Message = message,
                Priority = priority,
                CancelToken = cancellationToken
            };
            try
            {
                await SendMessageAsync(queuedMessage).DynamicContext();
            }
            finally
            {
                await queuedMessage.TryDisposeAsync().DynamicContext();
            }
        }

        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="queuedMessage">Message (won't be disposed)</param>
        protected virtual async Task SendMessageAsync(OutgoingQueue.QueuedMessage queuedMessage)
        {
            if (queuedMessage.Message is RpcMessageBase rpcMessage && rpcMessage.RequireId && !rpcMessage.Id.HasValue)
                rpcMessage.Id = CreateMessageId();
            Logger?.Log(LogLevel.Trace, "{this} sending message type {type} ({clrType}) as #{id} with priority {priority}", ToString(), queuedMessage.Message.Type, queuedMessage.Message.GetType(), queuedMessage.Message.Id, queuedMessage.Priority);
            try
            {
                await OutgoingMessages.EnqueueAsync(queuedMessage, queuedMessage, queuedMessage.CancelToken).DynamicContext();
                await queuedMessage.Completion.Task.WaitAsync(queuedMessage.CancelToken).DynamicContext();
            }
            catch (Exception ex)
            {
                queuedMessage.Completion.TrySetException(ex);
                throw;
            }
            finally
            {
                queuedMessage.SetDone();
                Logger?.Log(LogLevel.Trace, "{this} processed message type {type} ({clrType}) sending as #{id} with priority {priority} within {runtime}", ToString(), queuedMessage.Message.Type, queuedMessage.Message.GetType(), queuedMessage.Message.Id, queuedMessage.Priority, queuedMessage.Done - queuedMessage.Enqueued);
            }
        }

        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task SendMessageAsync(IRpcMessage message, CancellationToken cancellationToken = default)
        {
            if (Equals(cancellationToken, default))
                cancellationToken = CancelToken;
            using SemaphoreSyncContext ssc = await WriteSync.SyncContextAsync(cancellationToken).DynamicContext();
            if (message is RpcMessageBase rpcMessage && rpcMessage.RequireId && !rpcMessage.Id.HasValue)
                rpcMessage.Id = CreateMessageId();
            Logger?.Log(LogLevel.Trace, "{this} sending message type {type} ({clrType}) as #{id}", ToString(), message.Type, message.GetType(), message.Id);
            LastMessageSent = DateTime.Now;
            using (LimitedLengthStream limited = new(Options.Stream, Options.MaxMessageLength, leaveOpen: true))
                try
                {
                    await limited.WriteRpcMessageAsync(message, CancelToken).DynamicContext();
                }
                catch (OutOfMemoryException)
                {
                    Logger?.Log(LogLevel.Error, "{this} outgoing message type {type} ({clrType}) ID #{id} is too big (disposing due to invalid RPC stream state)", ToString(), message.Type, message.GetType(), message.Id);
                    _ = DisposeAsync().AsTask();
                    throw;
                }
            if (Options.FlushStream)
                await Options.Stream.FlushAsync(CancelToken).DynamicContext();
        }
    }
}
