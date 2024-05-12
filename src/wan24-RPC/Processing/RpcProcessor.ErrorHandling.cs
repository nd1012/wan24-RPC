using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Streaming;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing
{
    // Error handling
    public partial class RpcProcessor
    {
        /// <summary>
        /// Handle an error with the incoming message storage (handler should dispose values, if required; peer will be disconnected; this method should not throw)
        /// </summary>
        /// <param name="message">RPC Message</param>
        /// <param name="ex">Exception</param>
        /// <returns>If handled</returns>
        protected virtual async Task<bool> HandleIncomingMessageStorageErrorAsync(IRpcMessage message, Exception ex)
        {
            try
            {
                Logger?.Log(LogLevel.Warning, "{this} handling incoming message type {type} queue error", ToString(), message.GetType());
                switch (message)
                {
                    case StreamStartMessage streamStart:
                        {
                            if (await RemoveOutgoingStreamAsync(streamStart.Id!.Value).DynamicContext() is OutgoingStream outStream)
                            {
                                await using (outStream.DynamicContext())
                                    if (!CancelToken.IsCancellationRequested && !outStream.IsDisposing && outStream.IsStarted && !outStream.IsDone)
                                        try
                                        {
                                            outStream.LastException ??= ex;
                                            outStream.Cancellation.Cancel();
                                        }
                                        catch
                                        {
                                        }
                                if (!CancelToken.IsCancellationRequested)
                                    await SendMessageAsync(new LocalStreamCloseMessage()
                                    {
                                        PeerRpcVersion = Options.RpcVersion,
                                        Id = outStream.Id,
                                        Error = ex

                                    }, RPC_PRIORTY).DynamicContext();
                            }
                        }
                        break;
                    case StreamChunkMessage streamChunk:
                        {
                            if (await RemoveIncomingStreamAsync(streamChunk.Stream).DynamicContext() is IncomingStream inStream)
                                await using (inStream.DynamicContext())
                                    if (!CancelToken.IsCancellationRequested && !inStream.IsDisposing && inStream.IsStarted && !inStream.IsDone)
                                        try
                                        {
                                            await inStream.CancelAsync().DynamicContext();
                                        }
                                        catch
                                        {
                                        }
                        }
                        break;
                    case RemoteStreamCloseMessage remoteClose:
                        {
                            if (await RemoveOutgoingStreamAsync(remoteClose.Id!.Value).DynamicContext() is OutgoingStream outStream)
                            {
                                await using (outStream.DynamicContext())
                                    if (!CancelToken.IsCancellationRequested && !outStream.IsDisposing && outStream.IsStarted && !outStream.IsDone)
                                        try
                                        {
                                            outStream.LastException ??= ex;
                                            outStream.Cancellation.Cancel();
                                        }
                                        catch
                                        {
                                        }
                                if (!CancelToken.IsCancellationRequested)
                                    await SendMessageAsync(new LocalStreamCloseMessage()
                                    {
                                        PeerRpcVersion = Options.RpcVersion,
                                        Id = outStream.Id,
                                        Error = ex

                                    }, RPC_PRIORTY).DynamicContext();
                            }
                        }
                        break;
                    case RequestMessage request:
                        if (request.Parameters is not null)
                            await HandleValuesOnErrorAsync(outgoing: false, ex, request.Parameters).DynamicContext();
                        await request.DisposeParametersAsync().DynamicContext();
                        break;
                    case ResponseMessage response:
                        if (response.ReturnValue is not null)
                            await HandleValueOnErrorAsync(response.ReturnValue, outgoing: false, ex).DynamicContext();
                        await response.DisposeReturnValueAsync().DynamicContext();
                        break;
                    case EventMessage e:
                        if (e.Arguments is not null)
                            await HandleValueOnErrorAsync(e.Arguments, outgoing: false, ex).DynamicContext();
                        await e.DisposeArgumentsAsync().DynamicContext();
                        break;
                    default:
                        Logger?.Log(LogLevel.Information, "{this} not required to handle incoming message type {type} queue error", ToString(), message.GetType());
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Handle parameter or return values on error (this method shouldn't throw)
        /// </summary>
        /// <param name="outgoing">If the values were going to be sent to the peer</param>
        /// <param name="ex">Exception</param>
        /// <param name="values">Values (will be disposed)</param>
        protected virtual async Task HandleValuesOnErrorAsync(bool outgoing, Exception ex, params object?[] values)
        {
            foreach (object? value in values)
                if (value is not null)
                    await HandleValueOnErrorAsync(value, outgoing, ex).DynamicContext();
        }

        /// <summary>
        /// Handle a parameter or return value on error (this method shouldn't throw)
        /// </summary>
        /// <param name="value">Value (will be disposed)</param>
        /// <param name="outgoing">If the value was going to be sent to the peer</param>
        /// <param name="ex">Exception</param>
        /// <returns>If handled</returns>
        protected virtual async Task<bool> HandleValueOnErrorAsync(object value, bool outgoing, Exception ex)
        {
            try
            {
                // Special type handling
                switch (value)
                {
                    case RpcOutgoingStreamParameter streamParameter:
                        if (streamParameter.DisposeSource)
                            await streamParameter.Source.DisposeAsync().DynamicContext();
                        break;
                    case RpcStreamValue streamValue:
                        if (!streamValue.Stream.HasValue)
                            break;
                        if (outgoing && await RemoveOutgoingStreamAsync(streamValue.Stream.Value).DynamicContext() is OutgoingStream outStream)
                        {
                            await using (outStream.DynamicContext())
                                if (!CancelToken.IsCancellationRequested && !outStream.IsDisposing && outStream.IsStarted && !outStream.IsDone)
                                    try
                                    {
                                        outStream.LastException ??= ex;
                                        outStream.Cancellation.Cancel();
                                    }
                                    catch
                                    {
                                    }
                        }
                        else if (!outgoing && await RemoveIncomingStreamAsync(streamValue.Stream.Value).DynamicContext() is IncomingStream inStream)
                        {
                            await using (inStream.DynamicContext())
                                if (!CancelToken.IsCancellationRequested && !inStream.IsDisposing && inStream.IsStarted && !inStream.IsDone)
                                    try
                                    {
                                        await inStream.CancelAsync().DynamicContext();
                                    }
                                    catch
                                    {
                                    }
                        }
                        break;
                    case IAsyncDisposable:
                    case IDisposable:
                        break;
                    default:
                        return false;
                }
                // Disposable handling
                switch (value)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().DynamicContext();
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Handle a call processing error
        /// </summary>
        /// <param name="call">Call</param>
        /// <param name="exception">Exception</param>
        /// <returns>If handled</returns>
        protected virtual async Task<bool> HandleCallProcessingErrorAsync(Call call, Exception exception)
        {
            await call.HandleExceptionAsync(exception).DynamicContext();
            await SendErrorResponseAsync(
                call.Message as RequestMessage ?? throw new ArgumentException("Missing request message", nameof(call)), 
                exception
                ).DynamicContext();
            return true;
        }

        /// <summary>
        /// Handle an event processing error
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="e">Event</param>
        /// <param name="ex">Exception</param>
        /// <returns>If handled</returns>
        protected virtual Task<bool> HandleEventProcessingErrorAsync(EventMessage message, RpcEvent? e, Exception ex) => Task.FromResult(false);

        /// <summary>
        /// Handle an incoming stream processing error
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="ex">Exception</param>
        /// <returns>If handled</returns>
        protected virtual Task<bool> HandleIncomingStreamProcessingErrorAsync(IncomingStream stream, Exception ex) => Task.FromResult(false);

        /// <summary>
        /// Handle an outgoing stream processing error
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="ex">Exception</param>
        /// <returns>If handled</returns>
        protected virtual Task<bool> HandleOutgoingStreamProcessingErrorAsync(OutgoingStream stream, Exception ex) => Task.FromResult(false);

        /// <summary>
        /// Handle an invalid response return value (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>If handled</returns>
        protected virtual async Task<bool> HandleInvalidResponseReturnValueAsync(ResponseMessage message)
        {
            // Handle invalid stream return value (close the remote stream)
            if (!CancelToken.IsCancellationRequested && message.ReturnValue is RpcStreamValue incomingStream && incomingStream.Stream.HasValue)
            {
                try
                {
                    Logger?.Log(LogLevel.Debug, "{this} closing invalid remote stream return value for request #{id}", ToString(), message.Id);
                    await SendMessageAsync(new RemoteStreamCloseMessage()
                    {
                        PeerRpcVersion = Options.RpcVersion,
                        Id = incomingStream.Stream
                    }, RPC_PRIORTY).DynamicContext();
                }
                catch (ObjectDisposedException) when (IsDisposing)
                {
                }
                catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Logger?.Log(LogLevel.Warning, "{this} failed to close invalid remote stream returned for request #{id}: {ex}", ToString(), message.Id, ex);
                }
                return true;
            }
            return false;
        }
    }
}
