using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Serialization;

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
        /// Create a message ID
        /// </summary>
        /// <returns>Message ID</returns>
        protected virtual long CreateMessageId()
        {
            EnsureUndisposed();
            return Interlocked.Increment(ref MessageId);
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
