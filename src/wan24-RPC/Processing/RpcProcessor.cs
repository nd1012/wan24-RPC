using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api;
using wan24.RPC.Processing.Exceptions;
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
            // Create queues
            IncomingMessages = CreateIncomingMessageQueue();
            OutgoingMessages = CreateOutgoingMessageQueue();
            Calls = CreateCallQueue();
            Requests = CreateRequestQueue();
            // Keep alive
            if (options.KeepAlive is not null && options.KeepAlive.Timeout > TimeSpan.Zero)
            {
                HeartBeat = new(options.KeepAlive.Timeout)
                {
                    Name = "RPC processor heartbeat"
                };
                HeartBeat.OnTimeout += HandleHeartBeatTimeoutAsync;
                PeerHeartBeat = new(options.KeepAlive.Timeout + options.KeepAlive.PeerTimeout)
                {
                    Name = "RPC processor peer heartbeat"
                };
                PeerHeartBeat.OnTimeout += HandlePeerHeartBeatTimeoutAsync;
            }
            else
            {
                HeartBeat = null;
                PeerHeartBeat = null;
            }
            // Others
            foreach (object api in Options.API.Values.Select(a => a.Instance))
                if (api is IWantRpcProcessorInfo processorInfo)
                    processorInfo.Processor = this;
            RpcProcessorTable.Processors[GUID] = this;
        }

        /// <summary>
        /// GUID
        /// </summary>
        public string GUID { get; } = Guid.NewGuid().ToString();

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
                yield return new(__("Scopes"), Scopes.Count, __("Number of RPC scopes"));
                yield return new(__("Remote scopes"), RemoteScopes.Count, __("Number of RPC remote scopes"));
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
                if (Options.Stream.CanRead)
                {
                    IRpcMessage? message;
                    while (!CancelToken.IsCancellationRequested)
                    {
                        // Wait for incoming mesage queue space
                        if (!IncomingMessages.HasSpace)
                        {
                            Logger?.Log(LogLevel.Trace, "{this} worker waiting for incoming message queue space", ToString());
                            await IncomingMessages.WaitSpaceAsync().DynamicContext();
                        }
                        // Read the next RPC message
                        Logger?.Log(LogLevel.Trace, "{this} worker waiting for incoming RPC messages", ToString());
                        using (LimitedLengthStream limited = new(Options.Stream, Options.MaxMessageLength, leaveOpen: true)
                        {
                            ThrowOnReadOverflow = true
                        })
                            message = await limited.ReadRpcMessageAsync(Options.SerializerVersion, cancellationToken: CancelToken).DynamicContext();
                        LastMessageReceived = DateTime.Now;
                        // Allow pre-queue message processing
                        Logger?.Log(LogLevel.Trace, "{this} worker pre-processing incoming RPC message", ToString());
                        try
                        {
                            if (await PreHandleIncomingMessageAsync(message).DynamicContext())
                            {
                                Logger?.Log(LogLevel.Trace, "{this} worker incoming RPC message pre-processor processed the message", ToString());
                                message = null;
                                continue;
                            }
                        }
                        catch
                        {
                            Logger?.Log(LogLevel.Error, "{this} worker failed to pre-process incoming RPC message", ToString());
                            throw;
                        }
                        // Enqueue the incoming message for processing
                        Logger?.Log(LogLevel.Trace, "{this} worker enqueue incoming RPC message", ToString());
                        try
                        {
                            if (Options.KeepAlive != default)
                            {
                                // Exceeding the incoming message queue is not allowed when using keep alive
                                if (IncomingMessages.Queued >= Options.IncomingMessageQueue.Capacity)
                                    throw new TooManyRpcMessagesException("Can't keep alive anymore (incoming message queue is exhausted)");
                            }
                            else if (IncomingMessages.Queued + 1 >= Options.IncomingMessageQueue.Capacity)
                            {
                                // Block reading incoming messages due to an incoming message queue overflow
                                Logger?.Log(LogLevel.Debug, "{this} worker blocking incoming RPC message reading", ToString());
                                await IncomingMessages.ResetSpaceEventAsync().DynamicContext();
                            }
                            await IncomingMessages.EnqueueAsync(message, CancelToken).DynamicContext();
                            Logger?.Log(LogLevel.Trace, "{this} worker incoming RPC message queued (now {count} messages are queued)", ToString(), IncomingMessages.Count);
                        }
                        catch
                        {
                            Logger?.Log(LogLevel.Error, "{this} worker failed to enqueue incoming RPC message", ToString());
                            throw;
                        }
                        message = null;
                    }
                }
                else
                {
                    Logger?.Log(LogLevel.Debug, "{this} worker waiting for cancellation (RPC stream is write-only)", ToString());
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
            await Task.WhenAll(
                Calls.StartAsync(),
                Requests.StartAsync(),
                OutgoingMessages.StartAsync(),
                IncomingMessages.StartAsync()
                ).DynamicContext();
            HeartBeat?.Reset();
            PeerHeartBeat?.Reset();
        }

        /// <summary>
        /// Stop exceptional and dispose
        /// </summary>
        /// <param name="ex">Exception</param>
        protected virtual async Task StopExceptionalAndDisposeAsync(Exception ex)
        {
            Logger?.Log(LogLevel.Error, "{this} stop exceptional: {ex}", ToString(), ex);
            if (StoppedExceptional || LastException is not null)
            {
                Logger?.Log(LogLevel.Warning, "{this} had an exception already", ToString());
                return;
            }
            StoppedExceptional = true;
            LastException = ex;
            await DisposeAsync().DynamicContext();
        }

        /// <summary>
        /// End working
        /// </summary>
        protected virtual async Task EndWorkAsync()
        {
            Logger?.Log(LogLevel.Debug, "{this} end work", ToString());
            HeartBeat?.Stop();
            PeerHeartBeat?.Stop();
            await Task.WhenAll(
                IncomingMessages.StopAsync(),
                Requests.StopAsync(),
                Calls.StopAsync(),
                OutgoingMessages.StopAsync(),
                IncomingMessages.StopAsync()
                ).DynamicContext();
        }
    }
}
