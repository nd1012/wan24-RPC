using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
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
            Request request = new()
            {
                Processor = this,
                Message = new RequestMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Id = Interlocked.Increment(ref MessageId),
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0
                        ? null
                        : parameters
                },
                Cancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                if (!AddPendingRequest(request))
                    throw new InvalidProgramException($"Failed to store pending request #{request.Message.Id} (double ID)");
                try
                {
                    await Requests.EnqueueAsync(request, cancellationToken).DynamicContext();
                    object? res = await request.RequestCompletion.Task.DynamicContext() ?? throw new InvalidDataException("The RPC method returned NULL");
                    if (res is not null && !returnValueType.IsAssignableFrom(res.GetType()))
                    {
                        await res.TryDisposeAsync().DynamicContext();
                        throw new InvalidDataException($"Expected return value type {returnValueType}, got {res.GetType()} instead");
                    }
                    return res;
                }
                finally
                {
                    RemovePendingRequest(request);
                }
            }
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
            Request request = new()
            {
                Processor = this,
                Message = new RequestMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Id = Interlocked.Increment(ref MessageId),
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0
                        ? null
                        : parameters,
                    WantsReturnValue = false
                },
                Cancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                if (!AddPendingRequest(request))
                    throw new InvalidProgramException($"Failed to store pending request #{request.Message.Id} (double ID)");
                try
                {
                    await Requests.EnqueueAsync(request, cancellationToken).DynamicContext();
                    await request.RequestCompletion.Task.DynamicContext();
                }
                finally
                {
                    RemovePendingRequest(request);
                }
            }
        }

        /// <summary>
        /// Send a ping message and wait for the pong message
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task PingAsync(CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            Logger?.Log(LogLevel.Debug, "{this} sending ping request", ToString());
            Request request = new()
            {
                Processor = this,
                Message = new PingMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Id = Interlocked.Increment(ref MessageId)
                },
                Cancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                if (!AddPendingRequest(request))
                    throw new InvalidProgramException($"Failed to store ping request #{request.Message.Id} (double ID)");
                try
                {
                    await SendMessageAsync(request.Message, cancellationToken).DynamicContext();
                    await request.ProcessorCompletion.Task.DynamicContext();
                    Logger?.Log(LogLevel.Debug, "{this} got pong response after {runtime}", ToString(), request.Runtime);
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
                rpcMessage.Id = Interlocked.Increment(ref MessageId);
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
                rpcMessage.Id = Interlocked.Increment(ref MessageId);
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
