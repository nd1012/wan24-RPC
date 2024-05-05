using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Messages.Interfaces;
using wan24.RPC.Api.Messages.Streaming;

namespace wan24.RPC.Processing
{
    // Incoming
    public partial class RpcProcessor
    {
        /// <summary>
        /// Handle a message (should call <see cref="StopExceptionalAsync(Exception)"/> on exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleMessageAsync(IRpcMessage message)
        {
            await Task.Yield();
            Options.Logger?.Log(LogLevel.Debug, "{this} handling message type {type}", this, message.Type);
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
                    case ErrorResponseMessage error:
                        await HandleErrorAsync(error).DynamicContext();
                        break;
                    case CancellationMessage cancellation:
                        await HandleCancellationAsync(cancellation).DynamicContext();
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
                    case StreamStartMessage chunkRequest:
                        await HandleStreamStartAsync(chunkRequest).DynamicContext();
                        break;
                    case StreamChunkMessage chunkResponse:
                        //TODO
                        break;
                    case RemoteStreamCloseMessage remoteClose:
                        await HandleOutgoingStreamCloseAsync(remoteClose).DynamicContext();
                        break;
                    case LocalStreamCloseMessage localClose:
                        //TODO
                        break;
                    default:
                        throw new InvalidDataException($"Can't handle message type #{message.Id}");
                }
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                await StopExceptionalAsync(ex).DynamicContext();
            }
        }
    }
}
