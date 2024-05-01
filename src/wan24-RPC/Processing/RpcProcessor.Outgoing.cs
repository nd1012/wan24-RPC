using wan24.Core;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Messages.Serialization.Extensions;

namespace wan24.RPC.Processing
{
    // Outgoing
    public partial class RpcProcessor
    {
        /// <summary>
        /// Stream writing thread synchronization
        /// </summary>
        protected readonly SemaphoreSync WriteSync = new();
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
        public virtual async Task<T> CallAsync<T>(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
        {
            EnsureUndisposed();
            Request request = new()
            {
                Message = new RequestMessage()
                {
                    Id = Interlocked.Increment(ref MessageId),
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0 
                        ? null 
                        : parameters,
                    WantsResponse = true
                },
                ProcessorCancellation = CancelToken,
                RequestCancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
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
            Request request = new()
            {
                Message = new RequestMessage()
                {
                    Id = Interlocked.Increment(ref MessageId),
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0
                        ? null
                        : parameters,
                    WantsResponse = true
                },
                ProcessorCancellation = CancelToken,
                RequestCancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
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
            Request request = new()
            {
                Message = new RequestMessage()
                {
                    Id = Interlocked.Increment(ref MessageId),
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0
                        ? null
                        : parameters,
                    WantsResponse = true,
                    WantsReturnValue = false
                },
                ProcessorCancellation = CancelToken,
                RequestCancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
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
        /// Call a RPC API method at the peer and wait for the request being sent
        /// </summary>
        /// <param name="api">API name</param>
        /// <param name="method">API method name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="parameters">Parameters</param>
        public virtual async Task CallAsync(string? api, string method, CancellationToken cancellationToken = default, params object?[] parameters)
        {
            EnsureUndisposed();
            Request request = new()
            {
                Message = new RequestMessage()
                {
                    Api = api,
                    Method = method,
                    Parameters = parameters.Length == 0
                        ? null
                        : parameters,
                    WantsResponse = false,
                    WantsReturnValue = false
                },
                ProcessorCancellation = CancelToken,
                RequestCancellation = cancellationToken
            };
            await using (request.DynamicContext())
            {
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
        protected virtual async Task SendMessageAsync(RpcMessageBase message, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default)
                cancellationToken = CancelToken;
            using SemaphoreSyncContext ssc = await WriteSync.SyncContextAsync(cancellationToken).DynamicContext();
            if (message.RequireId && !message.Id.HasValue)
                message.Id = Interlocked.Increment(ref MessageId);
            await Options.Stream.WriteRpcMessageAsync(message, CancelToken).DynamicContext();
        }
    }
}
