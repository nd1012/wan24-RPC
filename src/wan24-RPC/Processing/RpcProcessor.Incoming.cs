using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Exceptions;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Scopes;

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
        /// Handle an incoming message (should call <see cref="StopExceptionalAndDisposeAsync(Exception)"/> on exception)
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>If handled</returns>
        protected virtual async Task<bool> PreHandleIncomingMessageAsync(IRpcMessage message) => await HandleHeartBeatMessageAsync(message).DynamicContext();

        /// <summary>
        /// Handle an incoming message (should call <see cref="StopExceptionalAndDisposeAsync(Exception)"/> on exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleIncomingMessageAsync(IRpcMessage message)
        {
            await Task.Yield();
            Logger?.Log(LogLevel.Debug, "{this} handling message type {type}", ToString(), message.Type);
            try
            {
                switch (message)
                {
                    case RequestMessage request:
                        await HandleRequestAsync(request).DynamicContext();
                        return;
                    case ResponseMessage response:
                        await HandleResponseAsync(response).DynamicContext();
                        return;
                    case EventMessage remoteEvent:
                        await HandleEventAsync(remoteEvent).DynamicContext();
                        return;
                    case IRpcRemoteScopeMessage scopeMessage:
                        if (GetRemoteScope(scopeMessage.ScopeId) is RpcRemoteScopeBase remoteScope)
                        {
                            await remoteScope.HandleMessageAsync(scopeMessage, CancelToken).DynamicContext();
                        }
                        else if (scopeMessage.FailOnScopeNotFound)
                        {
                            throw new RpcScopeNotFoundException($"Unknown remote scope #{scopeMessage.ScopeId} in message {scopeMessage.GetType()}")
                            {
                                ScopeMessage = scopeMessage
                            };
                        }
                        else
                        {
                            Logger?.Log(
                                scopeMessage.WarnOnScopeNotFound 
                                    ? LogLevel.Warning 
                                    : LogLevel.Debug, 
                                "{this} received unknown remote scope #{id} in message {type}", 
                                ToString(), 
                                scopeMessage.ScopeId, 
                                scopeMessage.GetType()
                                );
                        }
                        return;
                    case IRpcScopeMessage scopeMessage:
                        if (GetScope(scopeMessage.ScopeId) is RpcScopeBase scope)
                        {
                            await scope.HandleMessageAsync(scopeMessage, CancelToken).DynamicContext();
                        }
                        else if (scopeMessage.FailOnScopeNotFound)
                        {
                            throw new RpcScopeNotFoundException($"Unknown local scope #{scopeMessage.ScopeId} in message {scopeMessage.GetType()}")
                            {
                                ScopeMessage = scopeMessage
                            };
                        }
                        else
                        {
                            Logger?.Log(
                                scopeMessage.WarnOnScopeNotFound 
                                    ? LogLevel.Warning 
                                    : LogLevel.Debug, 
                                "{this} received unknown local scope #{id} in message {type}", 
                                ToString(), 
                                scopeMessage.ScopeId, 
                                scopeMessage.GetType()
                                );
                        }
                        return;
                    case ErrorResponseMessage error:
                        await HandleErrorResponseAsync(error).DynamicContext();
                        return;
                    case CancelMessage cancel:
                        await HandleCallCancellationAsync(cancel).DynamicContext();
                        return;
                }
                if(!await HandleIncomingCustomMessageAsync(message).DynamicContext())
                    throw new InvalidDataException($"Can't handle message type #{message.Id} ({message.GetType()})");
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
                _ = StopExceptionalAndDisposeAsync(ex);
            }
        }

        /// <summary>
        /// Handle an incoming custom message (will disconnect on exception by this method)
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>If handled</returns>
        protected virtual Task<bool> HandleIncomingCustomMessageAsync(IRpcMessage message) => Task.FromResult(false);
    }
}
