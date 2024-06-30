using Microsoft.Extensions.Logging;
using System.Diagnostics.Contracts;
using wan24.Core;
using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing
{
    // Heartbeat
    public partial class RpcProcessor
    {
        /// <summary>
        /// Heartbeat
        /// </summary>
        protected readonly Core.Timeout? HeartBeat;
        /// <summary>
        /// Peer heartbeat
        /// </summary>
        protected readonly Core.Timeout? PeerHeartBeat;
        /// <summary>
        /// Time when the last message was received
        /// </summary>
        protected DateTime _LastMessageReceived = DateTime.MinValue;
        /// <summary>
        /// Time when the last message was sent
        /// </summary>
        protected DateTime _LastMessageSent = DateTime.MinValue;

        /// <summary>
        /// Time when the last message was received
        /// </summary>
        public virtual DateTime LastMessageReceived
        {
            get => _LastMessageReceived;
            set
            {
                if (!IsDisposing)
                    PeerHeartBeat?.Reset();
                _LastMessageReceived = value;
            }
        }

        /// <summary>
        /// Time when the last message was sent
        /// </summary>
        public virtual DateTime LastMessageSent
        {
            get => _LastMessageSent;
            set
            {
                if (!IsDisposing)
                    HeartBeat?.Reset();
                _LastMessageSent = value;
            }
        }

        /// <summary>
        /// Message loop duration (only available if <see cref="RpcProcessorOptions.KeepAlive"/> is used or <see cref="PingAsync(TimeSpan, CancellationToken)"/> was called)
        /// </summary>
        public TimeSpan MessageLoopDuration { get; protected set; }

        /// <summary>
        /// Handle a heartbeat message
        /// </summary>
        /// <param name="message">Message</param>
        /// <returns>If the message was handled</returns>
        protected virtual async Task<bool> HandleHeartBeatMessageAsync(IRpcMessage message)
        {
            switch (message)
            {
                case PingMessage ping:
                    await HandlePingMessageAsync(ping).DynamicContext();
                    break;
                case PongMessage pong:
                    await HandlePongMessageAsync(pong).DynamicContext();
                    break;
                default:
                    return false;
            }
            Logger?.Log(LogLevel.Trace, "{this} worker handled heartbeat message", ToString());
            return true;
        }

        /// <summary>
        /// Handle a ping message (processing should be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandlePingMessageAsync(PingMessage message)
        {
            try
            {
                Logger?.Log(LogLevel.Debug, "{this} sending pong response for ping request #{id}", ToString(), message.Id);
                await SendMessageAsync(new PongMessage(message)
                {
                    PeerRpcVersion = Options.RpcVersion
                }).DynamicContext();
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Logger?.Log(LogLevel.Debug, "{this} handling ping message #{id} canceled due to disposing", ToString(), message.Id);
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Debug, "{this} handling ping message #{id} canceled", ToString(), message.Id);
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} handling ping message #{id} failed (will dispose)", ToString(), message.Id);
                await StopExceptionalAndDisposeAsync(ex).DynamicContext();
            }
        }

        /// <summary>
        /// Handle a pong message (processing should be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandlePongMessageAsync(PongMessage message)
        {
            try
            {
                if (GetPendingRequest(message.Id!.Value) is Request pingRequest)
                {
                    Logger?.Log(LogLevel.Debug, "{this} got pong response for ping request #{id}", ToString(), message.Id);
                    pingRequest.ProcessorCompletion.TrySetResult(result: null);
                }
                else
                {
                    Logger?.Log(LogLevel.Warning, "{this} got pong response for unknown ping request #{id}", ToString(), message.Id);
                }
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Logger?.Log(LogLevel.Debug, "{this} handling pong message #{id} canceled due to disposing", ToString(), message.Id);
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Debug, "{this} handling pong message #{id} canceled", ToString(), message.Id);
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} handling pong message #{id} failed (will dispose)", ToString(), message.Id);
                await StopExceptionalAndDisposeAsync(ex).DynamicContext();
            }
        }

        /// <summary>
        /// Handle a heartbeat timeout (processing should be stopped on handler exception)
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Arguments</param>
        protected virtual async void HandleHeartBeatTimeoutAsync(Core.Timeout sender, EventArgs e)
        {
            await Task.Yield();
            Logger?.Log(LogLevel.Debug, "{this} heartbeat timeout", ToString());
            try
            {
                Contract.Assert(Options.KeepAlive is not null);
                DateTime now = DateTime.Now;
                await PingAsync(Options.KeepAlive.PeerTimeout, CancelToken).DynamicContext();
                MessageLoopDuration = DateTime.Now - now;
                Logger?.Log(LogLevel.Trace, "{this} got peer heartbeat", ToString());
            }
            catch (TimeoutException ex)
            {
                Logger?.Log(LogLevel.Information, "{this} heartbeat timeout - disposing", ToString());
                await StopExceptionalAndDisposeAsync(ex).DynamicContext();
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
            }
            catch (OperationCanceledException ex) when (Equals(ex.CancellationToken, CancelToken))
            {
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} heartbeat timeout handling error (disposing)", ToString());
                await StopExceptionalAndDisposeAsync(ex).DynamicContext();
            }
        }

        /// <summary>
        /// Handle a peer heartbeat timeout
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Arguments</param>
        protected virtual async void HandlePeerHeartBeatTimeoutAsync(Core.Timeout sender, EventArgs e)
        {
            await Task.Yield();
            Logger?.Log(LogLevel.Information, "{this} peer heartbeat timeout - disposing", ToString());
            try
            {
                await StopExceptionalAndDisposeAsync(new TimeoutException("Peer heartbeat timeout")).DynamicContext();
            }
            catch
            {
            }
        }
    }
}
