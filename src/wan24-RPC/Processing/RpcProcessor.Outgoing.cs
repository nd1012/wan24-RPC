using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Messages.Interfaces;
using wan24.RPC.Api.Messages.Serialization.Extensions;

namespace wan24.RPC.Processing
{
    // Outgoing
    public partial class RpcProcessor
    {
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
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters</param>
        /// <returns>Return value</returns>
        public virtual async Task<T> CallValueAsync<T>(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
        {
            EnsureUndisposed();
            Options.Logger?.Log(LogLevel.Debug, "{this} calling API \"{api}\" method \"{method}\" at the peer ({count} parameters)", this, api, method, parameters.Length);
            Request request = new()
            {
                Processor = this,
                Message = new RequestMessage()
                {
                    Id = Interlocked.Increment(ref MessageId),
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0 
                        ? null 
                        : parameters
                },
                ProcessorCancellation = CancelToken,
                RequestCancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                //TODO Stream and enumeration parameters
                PendingRequests[request.Message.Id!.Value] = request;
                try
                {
                    await Requests.EnqueueAsync(request, cancellationToken).DynamicContext();
                    return (T)(await request.RequestCompletion.Task.DynamicContext() ?? throw new InvalidDataException("The RPC method returned NULL"));
                }
                finally
                {
                    PendingRequests.TryRemove(request.Message.Id!.Value, out _);
                }
            }
        }

        /// <summary>
        /// Call a RPC API method at the peer and wait for the response
        /// </summary>
        /// <typeparam name="T">Return value type</typeparam>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters</param>
        /// <returns>Return value</returns>
        public virtual async Task<T?> CallNullableAsync<T>(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
        {
            EnsureUndisposed();
            Options.Logger?.Log(LogLevel.Debug, "{this} calling API \"{api}\" nullable method \"{method}\" at the peer ({count} parameters)", this, api, method, parameters.Length);
            Request request = new()
            {
                Processor = this,
                Message = new RequestMessage()
                {
                    Id = Interlocked.Increment(ref MessageId),
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0
                        ? null
                        : parameters
                },
                ProcessorCancellation = CancelToken,
                RequestCancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                //TODO Stream and enumeration parameters
                PendingRequests[request.Message.Id!.Value] = request;
                try
                {
                    await Requests.EnqueueAsync(request, cancellationToken).DynamicContext();
                    return (T?)await request.RequestCompletion.Task.DynamicContext();
                }
                finally
                {
                    PendingRequests.TryRemove(request.Message.Id!.Value, out _);
                }
            }
        }

        /// <summary>
        /// Call a RPC API method at the peer and wait for the return value
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">Method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters</param>
        /// <returns>Return value</returns>
        public virtual async Task CallVoidAsync(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
        {
            EnsureUndisposed();
            Options.Logger?.Log(LogLevel.Debug, "{this} calling API \"{api}\" void method \"{method}\" at the peer ({count} parameters)", this, api, method, parameters.Length);
            Request request = new()
            {
                Processor = this,
                Message = new RequestMessage()
                {
                    Id = Interlocked.Increment(ref MessageId),
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0
                        ? null
                        : parameters,
                    WantsReturnValue = false
                },
                ProcessorCancellation = CancelToken,
                RequestCancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
                //TODO Stream and enumeration parameters
                PendingRequests[request.Message.Id!.Value] = request;
                try
                {
                    await Requests.EnqueueAsync(request, cancellationToken).DynamicContext();
                    await request.RequestCompletion.Task.DynamicContext();
                }
                finally
                {
                    PendingRequests.TryRemove(request.Message.Id!.Value, out _);
                }
            }
        }

        /// <summary>
        /// Send a RPC message to the peer
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected virtual async Task SendMessageAsync(IRpcMessage message, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
                cancellationToken = CancelToken;
            using SemaphoreSyncContext ssc = await WriteSync.SyncContextAsync(cancellationToken).DynamicContext();
            if (message is RpcMessageBase rpcMessage && rpcMessage.RequireId && !rpcMessage.Id.HasValue)
                rpcMessage.Id = Interlocked.Increment(ref MessageId);
            Options.Logger?.Log(LogLevel.Trace, "{this} sending message type {type} ({clrType}) as #{id}", this, message.Type, message.GetType(), message.Id);
            using (LimitedLengthStream limited = new(Options.Stream, Options.MaxMessageLength, leaveOpen: true))
                try
                {
                    await limited.WriteRpcMessageAsync(message, CancelToken).DynamicContext();
                }
                catch(OutOfMemoryException)
                {
                    Options.Logger?.Log(LogLevel.Error, "{this} outgoing message type {type} ({clrType}) ID #{id} is too long (disposing due to invalid RPC stream state)", this, message.Type, message.GetType(), message.Id);
                    _ = DisposeAsync().AsTask();
                    throw;
                }
            if (Options.FlushStream)
                await Options.Stream.FlushAsync(CancelToken).DynamicContext();
        }
    }
}
