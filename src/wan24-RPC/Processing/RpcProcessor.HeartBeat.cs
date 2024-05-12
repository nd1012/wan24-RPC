using Microsoft.Extensions.Logging;
using wan24.Core;

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
        /// Handle a heartbeat timeout
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Arguments</param>
        protected virtual async void HandleHeartBeatTimeoutAsync(Core.Timeout sender, EventArgs e)
        {
            await Task.Yield();
            Logger?.Log(LogLevel.Debug, "{this} heartbeat timeout", ToString());
            using CancellationTokenSource cts = new(Options.KeepAlive);
            try
            {
                using Cancellations cancellation = new(CancelToken, cts.Token);
                await PingAsync(cancellation).DynamicContext();
                Logger?.Log(LogLevel.Trace, "{this} got peer heartbeat", ToString());
            }
            catch (OperationCanceledException ex) when (Equals(ex.CancellationToken, CancelToken))
            {
            }
            catch(OperationCanceledException ex) when(Equals(ex.CancellationToken, cts.Token))
            {
                Logger?.Log(LogLevel.Error, "{this} heartbeat timeout - disposing", ToString());
                await DisposeAsync().DynamicContext();
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Error, "{this} heartbeat timeout handling error (disposing): {ex}", ToString(), ex);
                await DisposeAsync().DynamicContext();
            }
            finally
            {
                if (!IsDisposing)
                {
                    Logger?.Log(LogLevel.Trace, "{this} heartbeat restarting timeout", ToString());
                    HeartBeat!.Start();
                }
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
            Logger?.Log(LogLevel.Warning, "{this} peer heartbeat timeout - disposing", ToString());
            await DisposeAsync().DynamicContext();
        }
    }
}
