using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Serialization;
using wan24.StreamSerializerExtensions;
using static wan24.Core.TranslationHelper;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// RPC processor
    /// </summary>
    public partial class RpcProcessor : HostedServiceBase, IStatusProvider
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Options (will be disposed)</param>
        public RpcProcessor(in RpcProcessorOptions options) : base()
        {
            Options = options;
            IncomingMessages = CreateIncomingMessageQueue();
            OutgoingMessages = CreateOutgoingMessageQueue();
            Calls = CreateCallQueue();
            Requests = CreateRequestQueue();
            if (options.KeepAlive > TimeSpan.Zero)
            {
                HeartBeat = new(options.KeepAlive);
                HeartBeat.OnTimeout += HandleHeartBeatTimeoutAsync;
                PeerHeartBeat = new(options.KeepAlive + options.PeerTimeout);
                PeerHeartBeat.OnTimeout += HandlePeerHeartBeatTimeoutAsync;
            }
            else
            {
                HeartBeat = null;
                PeerHeartBeat = null;
            }
            foreach (object api in Options.API.Values.Select(a => a.Instance))
                if (api is IWantRpcProcessorInfo processorInfo)
                    processorInfo.Processor = this;
            RpcProcessorTable.Processors[GetHashCode()] = this;
        }

        /// <summary>
        /// Options (will be disposed)
        /// </summary>
        public RpcProcessorOptions Options { get; }

        /// <summary>
        /// Logger
        /// </summary>
        public virtual ILogger? Logger => Options?.Logger;

        /// <summary>
        /// Registered remote events
        /// </summary>
        public IEnumerable<RpcEvent> RemoteEvents => _RemoteEvents.Values;

        /// <inheritdoc/>
        public virtual IEnumerable<Status> State
        {
            get
            {
                yield return new(__("Type"), GetType(), __("RPC processor CLR type"));
                yield return new(__("Name"), Name, __("RPC processor name"));
                yield return new(__("Running"), IsRunning, __("If the RPC processor is running at present"));
                yield return new(__("Started"), Started == DateTime.MinValue ? __("(never)") : Started.ToString(), __("Started time"));
                yield return new(__("Stopped"), Stopped == DateTime.MinValue ? __("(never)") : Stopped.ToString(), __("Stopped time"));
                yield return new(__("Exception"), LastException?.Message ?? __("(none)"), __("Last exception"));
                yield return new(__("Calls"), PendingCalls.Count, __("Number of pending RPC calls"));
                yield return new(__("Requests"), PendingRequests.Count, __("Number of pending RPC requests"));
                yield return new(__("Events"), _RemoteEvents.Count, __("Number of registered remote event handlers"));
                foreach (Status status in IncomingMessages.State)
                    yield return new(
                        status.Name,
                        status.State,
                        status.Description,
                        $"{__("Incoming message queue")}{(status.Group is null ? string.Empty : $"\\{status.Group}")}"
                        );
                foreach (Status status in Calls.State)
                    yield return new(
                        status.Name,
                        status.State,
                        status.Description,
                        $"{__("Incoming calls queue")}{(status.Group is null ? string.Empty : $"\\{status.Group}")}"
                        );
                foreach (Status status in OutgoingMessages.State)
                    yield return new(
                        status.Name,
                        status.State,
                        status.Description,
                        $"{__("Outgoing message queue")}{(status.Group is null ? string.Empty : $"\\{status.Group}")}"
                        );
                foreach (Status status in Requests.State)
                    yield return new(
                        status.Name,
                        status.State,
                        status.Description,
                        $"{__("Incoming requests queue")}{(status.Group is null ? string.Empty : $"\\{status.Group}")}"
                        );
            }
        }

        /// <inheritdoc/>
        protected override async Task WorkerAsync()
        {
            try
            {
                Logger?.Log(LogLevel.Debug, "{this} worker", ToString());
                await BeginWorkAsync().DynamicContext();
                if (Options.API.Count > 0 && Options.Stream.CanRead)
                {
                    IRpcMessage? message;
                    while (!CancelToken.IsCancellationRequested)
                    {
                        // Wait for incoming mesage queue space
                        Logger?.Log(LogLevel.Trace, "{this} worker waiting for incoming message queue space", ToString());
                        await IncomingMessages.WaitSpaceAsync().DynamicContext();
                        // Read the next RPC message
                        Logger?.Log(LogLevel.Trace, "{this} worker waiting for incoming RPC messages", ToString());
                        using (LimitedLengthStream limited = new(Options.Stream, Options.MaxMessageLength, leaveOpen: true)
                        {
                            ThrowOnReadOverflow = true
                        })
                            message = await limited.ReadRpcMessageAsync(Options.SerializerVersion, cancellationToken: CancelToken).DynamicContext();
                        LastMessageReceived = DateTime.Now;
                        // Enqueue the incoming message for processing
                        Logger?.Log(LogLevel.Trace, "{this} worker storing incoming RPC message", ToString());
                        try
                        {
                            if (IncomingMessages.Queued + 1 >= Options.IncomingMessageQueueCapacity)
                            {
                                // Block reading incoming messages due to an incoming message queue overflow
                                Logger?.Log(LogLevel.Debug, "{this} worker blocking incoming RPC message reading", ToString());
                                await IncomingMessages.SpaceEvent.ResetAsync().DynamicContext();
                            }
                            await IncomingMessages.EnqueueAsync(message, CancelToken).DynamicContext();
                            Logger?.Log(LogLevel.Trace, "{this} worker incoming RPC message queued", ToString());
                        }
                        catch (Exception ex)
                        {
                            Logger?.Log(LogLevel.Error, "{this} worker failed to enqueue incoming RPC message", ToString());
                            await HandleIncomingMessageStorageErrorAsync(message, ex).DynamicContext();
                            throw;
                        }
                        message = null;
                    }
                }
                else
                {
                    Logger?.Log(LogLevel.Trace, "{this} worker waiting for cancellation (no incoming messages are being accepted, 'cause there are no APIs)", ToString());
                    await CancelToken.WaitHandle.WaitAsync().DynamicContext();
                }
                Logger?.Log(LogLevel.Debug, "{this} worker done", ToString());
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Logger?.Log(LogLevel.Trace, "{this} worker canceled for disposing", ToString());
            }
            catch (SerializerException ex) when (ex.InnerException is OperationCanceledException && CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Trace, "{this} worker canceled during deserialization", ToString());
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Trace, "{this} worker canceled", ToString());
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Error, "{this} worker catched exception: {ex}", ToString(), ex);
                StoppedExceptional = true;
                LastException ??= ex;
                _ = DisposeAsync().AsTask();
                throw;
            }
            finally
            {
                await EndWorkAsync().DynamicContext();
            }
        }

        /// <summary>
        /// Begin working
        /// </summary>
        protected virtual async Task BeginWorkAsync()
        {
            Logger?.Log(LogLevel.Debug, "{this} begin work", ToString());
            foreach (object api in Options.API.Values.Select(a => a.Instance))
                if (api is IWantRpcProcessorInfo processorInfo)
                    processorInfo.ProcessorCancellation = CancelToken;
            await Calls.StartAsync(CancelToken).DynamicContext();
            await Requests.StartAsync(CancelToken).DynamicContext();
            await OutgoingMessages.StartAsync(CancelToken).DynamicContext();
            await IncomingMessages.StartAsync(CancelToken).DynamicContext();
            HeartBeat?.Reset();
            PeerHeartBeat?.Reset();
        }

        /// <summary>
        /// End working
        /// </summary>
        protected virtual async Task EndWorkAsync()
        {
            Logger?.Log(LogLevel.Debug, "{this} end work", ToString());
            HeartBeat?.Stop();
            PeerHeartBeat?.Stop();
            await IncomingMessages.StopAsync(CancelToken).DynamicContext();
            await Requests.StopAsync(CancellationToken.None).DynamicContext();
            await Calls.StopAsync(CancellationToken.None).DynamicContext();
            await OutgoingMessages.StopAsync(CancelToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Logger?.Log(LogLevel.Trace, "{this} sync disposing", ToString());
            RpcProcessorTable.Processors.TryRemove(GetHashCode(), out _);
            HeartBeat?.Dispose();
            PeerHeartBeat?.Dispose();
            ObjectDisposedException disposedException = new(GetType().ToString());
            // Cancel outgoing streams
            using (SemaphoreSyncContext ssc = OutgoingStreamsSync)
                foreach (OutgoingStream stream in OutgoingStreams.Values)
                    if (!stream.IsDisposing && stream.IsStarted && !stream.IsDone)
                        try
                        {
                            stream.LastException ??= disposedException;
                            stream.Cancellation.Cancel();
                        }
                        catch
                        {
                        }
            // Cancel incoming streams
            using (SemaphoreSyncContext ssc = IncomingStreamsSync)
                foreach (IncomingStream stream in IncomingStreams.Values)
                    if (!stream.IsDisposing && stream.IsStarted && !stream.IsDone)
                        try
                        {
                            stream.CancelAsync().GetAwaiter().GetResult();
                        }
                        catch
                        {
                        }
            // Stop processing
            base.Dispose(disposing);
            // Dispose message synchronization
            WriteSync.Dispose();
            // Dispose requests
            Requests.Dispose();
            foreach (Request request in PendingRequests.Values)
            {
                request.ProcessorCompletion.TrySetException(disposedException);
                request.RequestCompletion.TrySetException(disposedException);
            }
            PendingRequests.Values.DisposeAll();
            PendingRequests.Clear();
            // Dispose calls
            Calls.Dispose();
            foreach (Call call in PendingCalls.Values)
                call.Completion.TrySetException(disposedException);
            PendingCalls.Values.DisposeAll();
            PendingCalls.Clear();
            // Dispose events (if disposable)
            _RemoteEvents.Values.TryDisposeAll();
            _RemoteEvents.Clear();
            // Dispose outgoing streams
            OutgoingStreamsSync.Dispose();
            OutgoingStreams.Values.DisposeAll();
            OutgoingStreams.Clear();
            // Dispose incoming streams
            IncomingStreamsSync.Dispose();
            IncomingStreams.Values.DisposeAll();
            IncomingStreams.Clear();
            // Dispose incoming messages
            IncomingMessages.Dispose();
            // Dispose outgoing messages
            OutgoingMessages.Dispose();
            // Dispose others
            Options.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            Logger?.Log(LogLevel.Trace, "{this} async disposing", ToString());
            RpcProcessorTable.Processors.TryRemove(GetHashCode(), out _);
            if (HeartBeat is not null)
                await HeartBeat.DisposeAsync().DynamicContext();
            if (PeerHeartBeat is not null)
                await PeerHeartBeat.DisposeAsync().DynamicContext();
            ObjectDisposedException disposedException = new(GetType().ToString());
            // Cancel outgoing streams
            using (SemaphoreSyncContext ssc = await OutgoingStreamsSync.SyncContextAsync().DynamicContext())
                foreach (OutgoingStream stream in OutgoingStreams.Values)
                    if (!stream.IsDisposing && stream.IsStarted && !stream.IsDone)
                        try
                        {
                            stream.LastException ??= disposedException;
                            stream.Cancellation.Cancel();
                        }
                        catch
                        {
                        }
            // Cancel incoming streams
            using (SemaphoreSyncContext ssc = await IncomingStreamsSync.SyncContextAsync().DynamicContext())
                foreach (IncomingStream stream in IncomingStreams.Values)
                    if (!stream.IsDisposing && stream.IsStarted && !stream.IsDone)
                        try
                        {
                            await stream.CancelAsync().DynamicContext();
                        }
                        catch
                        {
                        }
            // Stop processing
            await base.DisposeCore().DynamicContext();
            // Dispose message synchronization
            await WriteSync.DisposeAsync().DynamicContext();
            // Dispose requests
            await Requests.DisposeAsync().DynamicContext();
            foreach (Request request in PendingRequests.Values)
            {
                request.ProcessorCompletion.TrySetException(disposedException);
                request.RequestCompletion.TrySetException(disposedException);
            }
            await PendingRequests.Values.DisposeAllAsync().DynamicContext();
            PendingRequests.Clear();
            // Dispose calls
            await Calls.DisposeAsync().DynamicContext();
            foreach (Call call in PendingCalls.Values)
                call.Completion.TrySetException(disposedException);
            await PendingCalls.Values.DisposeAllAsync().DynamicContext();
            PendingCalls.Clear();
            // Dispose events (if disposable)
            await _RemoteEvents.Values.TryDisposeAllAsync().DynamicContext();
            _RemoteEvents.Clear();
            // Dispose outgoing streams
            await OutgoingStreamsSync.DisposeAsync().DynamicContext();
            await OutgoingStreams.Values.DisposeAllAsync().DynamicContext();
            OutgoingStreams.Clear();
            // Dispose incoming streams
            await IncomingStreamsSync.DisposeAsync().DynamicContext();
            await IncomingStreams.Values.DisposeAllAsync().DynamicContext();
            IncomingStreams.Clear();
            // Dispose incoming messages
            await IncomingMessages.DisposeAsync().DynamicContext();
            // Dispose outgoing messages
            await OutgoingMessages.DisposeAsync().DynamicContext();
            // Dispose others
            await Options.DisposeAsync().DynamicContext();
        }
    }
}
