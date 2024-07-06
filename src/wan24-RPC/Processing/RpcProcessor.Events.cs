using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Processing.Messages;

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
            await Task.Yield();
            Logger?.Log(LogLevel.Debug, "{this} handling event \"{name}\" with arguments type {type}", ToString(), message.Name, message.Arguments?.GetType().ToString() ?? "NULL");
            RpcEvent? handler;
            try
            {
                handler = GetRemoteEvent(message.Name);
                if (handler?.Arguments is not null && message.HasSerializedArguments)
                    await message.DeserializeArgumentsAsync(handler.Arguments, CancelToken).DynamicContext();
                RaiseOnRemoteEvent(handler, message);
                if (handler is null)
                {
                    Logger?.Log(LogLevel.Debug, "{this} no event \"{name}\" handler - ignoring", ToString(), message.Name);
                    return;
                }
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

        /// <summary>
        /// Delegate for a remote event handler
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="message">Event message</param>
        public delegate void RemoteEventHandler_Delegate(RpcProcessor processor, RemoteEventArgs message);
        /// <summary>
        /// Raised on remote event
        /// </summary>
        public event RemoteEventHandler_Delegate? OnRemoteEvent;
        /// <summary>
        /// Raise the <see cref="OnRemoteEvent"/>
        /// </summary>
        /// <param name="handler">Event handler</param>
        /// <param name="message">Event RPC message</param>
        protected virtual void RaiseOnRemoteEvent(RpcEvent? handler, EventMessage message) => OnRemoteEvent?.Invoke(this, new(handler,message));

        /// <summary>
        /// Remote event arguments
        /// </summary>
        /// <param name="e">Event handler</param>
        /// <param name="message">Event RPC message</param>
        public class RemoteEventArgs(in RpcEvent? e, in EventMessage message) : EventArgs()
        {
            /// <summary>
            /// Event handler
            /// </summary>
            public RpcEvent? EventHandler { get; } = e;

            /// <summary>
            /// Event RPC message
            /// </summary>
            public EventMessage Message { get; } = message;
        }
    }
}
