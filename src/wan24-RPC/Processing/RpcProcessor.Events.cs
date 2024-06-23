using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Processing.Messages;

/*
 * An event can be sent to the peer, or an event from the peer can be handled from a registered event handler. The RPC processor will send a response to the peer, if the 
 * peer is waiting for event handlers to finish.
 */

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
        /// <param name="name">Event name</param>
        /// <param name="arguments">Event arguments type (must be an <see cref="EventArgs"/>)</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        public virtual RpcEvent RegisterEvent(in string name, in Type arguments, in RpcEvent.EventHandler_Delegate handler)
        {
            EnsureUndisposed();
            if (!typeof(EventArgs).IsAssignableFrom(arguments))
                throw new ArgumentException("Invalid event arguments type", nameof(arguments));
            RpcEvent e = CreateEvent(name, arguments, handler);
            if (!AddRemoteEvent(e))
            {
                e.TryDispose();
                throw new InvalidOperationException($"Event \"{name}\" handler registered already");
            }
            return e;
        }

        /// <summary>
        /// Register a remote event handler
        /// </summary>
        /// <typeparam name="T">Event arguments type</typeparam>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        public RpcEvent RegisterEvent<T>(in string name, in RpcEvent.EventHandler_Delegate handler) where T : EventArgs
            => RegisterEvent(name, typeof(T), handler);

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
            if (!AddRemoteEvent(e))
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
            Logger?.Log(LogLevel.Debug, "{this} raising event \"{name}\" with arguments type {type} at the peer (and wait: {wait})", ToString(), name, e?.GetType().ToString() ?? "NULL", wait);
            if (wait)
            {
                Request request = new()
                {
                    Processor = this,
                    Message = new EventMessage()
                    {
                        PeerRpcVersion = Options.RpcVersion,
                        Id = Interlocked.Increment(ref MessageId),
                        Name = name,
                        Arguments = e,
                        Waiting = true
                    },
                    Cancellation = cancellationToken
                };
                await using (request.DynamicContext())
                {
                    Logger?.Log(LogLevel.Trace, "{this} storing event \"{name}\" request as #{id}", ToString(), name, request.Id);
                    if (!AddPendingRequest(request))
                        throw new InvalidProgramException($"Failed to store event message #{request.Id} (double message ID)");
                    try
                    {
                        await SendMessageAsync(request.Message, Options.Priorities.Event, cancellationToken).DynamicContext();
                        await request.ProcessorCompletion.Task.DynamicContext();
                    }
                    finally
                    {
                        RemovePendingRequest(request);
                    }
                }
            }
            else
            {
                await SendMessageAsync(new EventMessage()
                {
                    PeerRpcVersion = Options.RpcVersion,
                    Name = name,
                    Arguments = e
                }, Options.Priorities.Event, cancellationToken).DynamicContext();
            }
        }

        /// <summary>
        /// Add a remote event
        /// </summary>
        /// <param name="e">Event</param>
        protected virtual bool AddRemoteEvent(in RpcEvent e)
            => _RemoteEvents.TryAdd(e.Name, e);

        /// <summary>
        /// Get a remote event
        /// </summary>
        /// <param name="name">Name</param>
        /// <returns>Event</returns>
        protected virtual RpcEvent? GetRemoteEvent(in string name)
            => _RemoteEvents.TryGetValue(name, out RpcEvent? res) ? res : null;

        /// <summary>
        /// Remove a remote event
        /// </summary>
        /// <param name="e">Event</param>
        /// <returns>If removed</returns>
        protected virtual bool RemoveRemoteEvent(in RpcEvent e)
            => _RemoteEvents.TryRemove(e.Name, out _);

        /// <summary>
        /// Remove a remote event
        /// </summary>
        /// <param name="name">Name</param>
        /// <returns>Event</returns>
        protected virtual RpcEvent? RemoveRemoteEvent(in string name)
            => _RemoteEvents.TryRemove(name, out RpcEvent? res) ? res : null;
        /// <summary>
        /// Create an event
        /// </summary>
        /// <param name="name">Event name</param>
        /// <param name="arguments">Event arguments type</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        protected virtual RpcEvent CreateEvent(in string name, in Type arguments, in RpcEvent.EventHandler_Delegate handler)
            => new()
            {
                Processor = this,
                Name = name,
                Arguments = arguments,
                Handler = handler
            };

        /// <summary>
        /// Create an event
        /// </summary>
        /// <typeparam name="T">Event arguments type</typeparam>
        /// <param name="name">Event name</param>
        /// <param name="handler">Event handler</param>
        /// <returns>Event</returns>
        protected RpcEvent CreateEvent<T>(in string name, in RpcEvent.EventHandler_Delegate handler) where T : EventArgs
            => CreateEvent(name, typeof(T), handler);

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
            Logger?.Log(LogLevel.Debug, "{this} handling event \"{name}\" with arguments type {type}", ToString(), message.Name, message.Arguments?.GetType().ToString() ?? "NULL");
            RpcEvent? handler;
            try
            {
                handler = GetRemoteEvent(message.Name);
                if (handler is null)
                {
                    Logger?.Log(LogLevel.Debug, "{this} no event \"{name}\" handler - ignoring", ToString(), message.Name);
                    return;
                }
                if (handler.Arguments is not null)
                    await message.DeserializeArgumentsAsync(handler.Arguments, CancelToken).DynamicContext();
                await handler.RaiseEventAsync(message, CancelToken).DynamicContext();
                Logger?.Log(LogLevel.Trace, "{this} handled event \"{name}\" with arguments type {type}", ToString(), message.Name, message.Arguments?.GetType().ToString() ?? "NULL");
                if (message.Waiting)
                {
                    Logger?.Log(LogLevel.Trace, "{this} sending event \"{name}\" response", ToString(), message.Name);
                    await SendMessageAsync(new ResponseMessage()
                    {
                        PeerRpcVersion = Options.RpcVersion,
                        Id = message.Id
                    }, Options.Priorities.Event, CancelToken).DynamicContext();
                }
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} handling event \"{name}\" with arguments type {type} failed exceptional: {ex}", ToString(), message.Name, message.Arguments?.GetType().ToString() ?? "NULL", ex);
                if (message.Waiting && !IsDisposing)
                    try
                    {
                        Logger?.Log(LogLevel.Trace, "{this} sending event \"{name}\" error response", ToString(), message.Name);
                        await SendMessageAsync(new ErrorResponseMessage()
                        {
                            PeerRpcVersion = Options.RpcVersion,
                            Id = message.Id,
                            Error = ex
                        }, Options.Priorities.Event, CancelToken).DynamicContext();
                    }
                    catch (Exception ex2)
                    {
                        Logger?.Log(LogLevel.Warning, "{this} sending event \"{name}\" error response failed: {ex2}", ToString(), message.Name, ex2);
                    }
            }
            finally
            {
                await message.DisposeArgumentsAsync().DynamicContext();
            }
        }
    }
}
