using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Streaming;

namespace wan24.RPC.Processing
{
    // Incoming
    public partial class RpcProcessor
    {
        /// <summary>
        /// Incoming messages
        /// </summary>
        protected readonly IncomingQueue IncomingMessages;

        /// <summary>
        /// Handle a message (should call <see cref="StopExceptionalAndDisposeAsync(Exception)"/> on exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleMessageAsync(IRpcMessage message)
        {
            await Task.Yield();
            Logger?.Log(LogLevel.Debug, "{this} handling message type {type}", ToString(), message.Type);
            try
            {
                switch (message)
                {
                    case RequestMessage request:
                        try
                        {
                            await HandleRequestAsync(request).DynamicContext();
                        }
                        catch
                        {
                            await request.DisposeParametersAsync().DynamicContext();
                            throw;
                        }
                        break;
                    case ResponseMessage response:
                        try
                        {
                            await HandleResponseAsync(response).DynamicContext();
                        }
                        catch
                        {
                            await response.DisposeReturnValueAsync().DynamicContext();
                            throw;
                        }
                        break;
                    case EventMessage remoteEvent:
                        try
                        {
                            await HandleEventAsync(remoteEvent).DynamicContext();
                        }
                        catch
                        {
                            await remoteEvent.DisposeArgumentsAsync().DynamicContext();
                            throw;
                        }
                        break;
                    case StreamStartMessage streamStart:
                        EnsureStreamsAreEnabled();
                        await HandleStreamStartAsync(streamStart).DynamicContext();
                        break;
                    case StreamChunkMessage streamChunk:
                        EnsureStreamsAreEnabled();
                        await HandleStreamChunkAsync(streamChunk).DynamicContext();
                        break;
                    case RemoteStreamCloseMessage remoteClose:
                        EnsureStreamsAreEnabled();
                        await HandleRemoteStreamCloseAsync(remoteClose).DynamicContext();
                        break;
                    case LocalStreamCloseMessage localClose:
                        EnsureStreamsAreEnabled();
                        await HandleLocalStreamCloseAsync(localClose).DynamicContext();
                        break;
                    case ErrorResponseMessage error:
                        await HandleErrorResponseAsync(error).DynamicContext();
                        break;
                    case CancellationMessage cancellation:
                        await HandleCancellationAsync(cancellation).DynamicContext();
                        break;
                    case PingMessage ping:
                        Logger?.Log(LogLevel.Debug, "{this} sending pong response for ping request #{id}", ToString(), message.Id);
                        await SendMessageAsync(new PongMessage(ping)
                        {
                            PeerRpcVersion = Options.RpcVersion
                        }).DynamicContext();
                        break;
                    case PongMessage:
                        {
                            if (GetPendingRequest(message.Id!.Value) is Request pingRequest)
                            {
                                Logger?.Log(LogLevel.Debug, "{this} got pong response for ping request #{id}", ToString(), message.Id);
                                pingRequest.ProcessorCompletion.TrySetResult(result: null);
                            }
                        }
                        break;
                    default:
                        throw new InvalidDataException($"Can't handle message type #{message.Id} ({message.GetType()})");
                }
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Logger?.Log(LogLevel.Warning, "{this} handling message type {type} canceled due to disposing", ToString(), message.Type);
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Warning, "{this} handling message type {type} canceled", ToString(), message.Type);
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Error, "{this} handling message type {type} failed (will dispose): {ex}", ToString(), message.Type, ex);
                await StopExceptionalAndDisposeAsync(ex).DynamicContext();
            }
        }
    }
}
