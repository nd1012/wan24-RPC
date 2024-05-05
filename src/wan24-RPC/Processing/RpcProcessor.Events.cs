using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Api.Messages;

namespace wan24.RPC.Processing
{
    // Remote event
    public partial class RpcProcessor
    {
        /// <summary>
        /// Remote events (key is the event name; values will be disposed)
        /// </summary>
        protected readonly ConcurrentDictionary<string, RpcEvent> _RemoteEvents = [];

        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <typeparam name="T">Event arguments type</typeparam>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        public virtual RpcEvent RegisterEvent<T>(in string name, in RpcEvent.EventHandler_Delegate handler) where T : EventArgs
        {
            EnsureUndisposed();
            RpcEvent e = CreateEvent<T>(name, handler);
            if (!_RemoteEvents.TryAdd(name, e))
            {
                e.TryDispose();
                throw new InvalidOperationException($"Event \"{name}\" handler registered already");
            }
            return e;
        }

        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        public virtual RpcEvent RegisterEvent(in string name, in RpcEvent.EventHandler_Delegate handler)
        {
            EnsureUndisposed();
            RpcEvent e = CreateEvent(name, handler);
            if (!_RemoteEvents.TryAdd(name, e))
            {
                e.TryDispose();
                throw new InvalidOperationException($"Event \"{name}\" handler registered already");
            }
            return e;
        }

        /// <summary>
        /// Raise an event at the peer
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="e">Event arguments</param>
        /// <param name="wait">Wait for remote event handlers to finish?</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task RaiseEventAsync(string name, EventArgs? e = null, bool wait = false, CancellationToken cancellationToken = default)
        {
            EnsureUndisposed();
            Options.Logger?.Log(LogLevel.Debug, "{this} raising event \"{name}\" with arguments type {type} at the peer (and wait: {wait})", this, name, e?.GetType().ToString() ?? "NULL", wait);
            if (wait)
            {
                Request request = new()
                {
                    Processor = this,
                    Message = new EventMessage()
                    {
                        Id = Interlocked.Increment(ref MessageId),
                        Name = name,
                        Arguments = e,
                        Waiting = true
                    },
                    ProcessorCancellation = CancelToken,
                    RequestCancellation = cancellationToken
                };
                await using (request.DynamicContext())
                {
                    Options.Logger?.Log(LogLevel.Trace, "{this} storing event \"{name}\" request as #{id}", this, name, request.Message.Id);
                    if (!PendingRequests.TryAdd(request.Message.Id!.Value, request))
                        throw new InvalidProgramException($"Failed to store event message #{request.Message.Id} (double message ID)");
                    try
                    {
                        await SendMessageAsync(request.Message, cancellationToken).DynamicContext();
                        await request.ProcessorCompletion.Task.DynamicContext();
                    }
                    finally
                    {
                        PendingRequests.TryRemove(request.Message.Id!.Value, out _);
                    }
                }
            }
            else
            {
                await SendMessageAsync(new EventMessage()
                {
                    Name = name,
                    Arguments = e
                }, cancellationToken).DynamicContext();
            }
        }

        /// <summary>
        /// Create an event
        /// </summary>
        /// <typeparam name="T">Event arguments type</typeparam>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        protected virtual RpcEvent CreateEvent<T>(in string name, in RpcEvent.EventHandler_Delegate handler) where T : EventArgs
            => new()
            {
                Processor = this,
                Name = name,
                Arguments = typeof(T),
                Handler = handler
            };

        /// <summary>
        /// Create an event
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        protected virtual RpcEvent CreateEvent(in string name, in RpcEvent.EventHandler_Delegate handler)
            => new()
            {
                Processor = this,
                Name = name,
                Handler = handler
            };

        /// <summary>
        /// Handle a RPC event (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleEventAsync(EventMessage message)
        {
            Options.Logger?.Log(LogLevel.Debug, "{this} handling event \"{name}\" with arguments type {type}", this, message.Name, message.Arguments?.GetType().ToString() ?? "NULL");
            try
            {
                if(!_RemoteEvents.TryGetValue(message.Name, out RpcEvent? handler))
                {
                    Options.Logger?.Log(LogLevel.Debug, "{this} no event \"{name}\" handler - ignoring", this, message.Name);
                    return;
                }
                await handler.RaiseEventAsync(message, CancelToken).DynamicContext();
                Options.Logger?.Log(LogLevel.Trace, "{this} handled event \"{name}\" with arguments type {type}", this, message.Name, message.Arguments?.GetType().ToString() ?? "NULL");
                if (message.Waiting)
                {
                    Options.Logger?.Log(LogLevel.Trace, "{this} sending event \"{name}\" response", this, message.Name);
                    await SendMessageAsync(new ResponseMessage()
                    {
                        Id = message.Id
                    }, CancelToken).DynamicContext();
                }
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch(Exception ex)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} handling event \"{name}\" with arguments type {type} failed exceptional: {ex}", this, message.Name, message.Arguments?.GetType().ToString() ?? "NULL", ex);
                if (message.Waiting && EnsureUndisposed(throwException: false))
                    try
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} sending event \"{name}\" error response", this, message.Name);
                        await SendMessageAsync(new ErrorResponseMessage()
                        {
                            Id = message.Id,
                            Error = ex
                        }, CancelToken).DynamicContext();
                    }
                    catch (Exception ex2)
                    {
                        Options.Logger?.Log(LogLevel.Warning, "{this} sending event \"{name}\" error response failed: {ex2}", this, message.Name, ex2);
                    }
            }
            finally
            {
                await message.DisposeArgumentsAsync().DynamicContext();
            }
        }
    }
}
