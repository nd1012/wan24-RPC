using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Api;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Serialization;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// RPC processor
    /// </summary>
    public partial class RpcProcessor : HostedServiceBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="options">Options (will be disposed)</param>
        public RpcProcessor(in RpcProcessorOptions options) : base()
        {
            Options = options;
            OutgoingMessages = CreateMessageQueue();
            Calls = CreateCallQueue();
            Requests = CreateRequestQueue();
            foreach (object api in Options.API.Values.Select(a => a.Instance))
                if (api is IWantRpcProcessorInfo processorInfo)
                    processorInfo.Processor = this;
        }

        /// <summary>
        /// Options (will be disposed)
        /// </summary>
        public RpcProcessorOptions Options { get; }

        /// <summary>
        /// Registered remote events
        /// </summary>
        public IEnumerable<RpcEvent> RemoteEvents => _RemoteEvents.Values;

        /// <summary>
        /// Get a context for processing a RPC call
        /// </summary>
        /// <param name="request">Message</param>
        /// <param name="method">API method</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Context</returns>
        protected virtual RpcContext GetContext(
            in RequestMessage request,
            in RpcApiMethodInfo method,
            in CancellationToken cancellationToken
            )
            => Options.DefaultContext is null
                ? new()
                {
                    Created = DateTime.Now,
                    Processor = this,
                    Cancellation = cancellationToken,
                    Message = request,
                    Method = method,
                    Services = Options.DefaultServices is null
                        ? new()
                        : new(Options.DefaultServices)
                        {
                            DisposeServiceProvider = false
                        }
                }
                : Options.DefaultContext with
                {
                    Created = DateTime.Now,
                    Processor = this,
                    Cancellation = cancellationToken,
                    Message = request,
                    Method = method,
                    Services = Options.DefaultServices is null
                        ? new()
                        : new(Options.DefaultServices)
                        {
                            DisposeServiceProvider = false
                        }
                };

        /// <inheritdoc/>
        protected override async Task WorkerAsync()
        {
            try
            {
                Options.Logger?.Log(LogLevel.Debug, "{this} worker", ToString());
                await BeginWorkAsync().DynamicContext();
                if (Options.API.Count > 0 && Options.Stream.CanRead)
                {
                    IRpcMessage? message;
                    while (!CancelToken.IsCancellationRequested)
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} worker waiting for incoming RPC messages", ToString());
                        using (LimitedLengthStream limited = new(Options.Stream, Options.MaxMessageLength, leaveOpen: true)
                        {
                            ThrowOnReadOverflow = true
                        })
                            message = await limited.ReadRpcMessageAsync(Options.SerializerVersion, cancellationToken: CancelToken).DynamicContext();
                        Options.Logger?.Log(LogLevel.Trace, "{this} worker handling incoming RPC message", ToString());
                        _ = HandleMessageAsync(message);
                        message = null;
                    }
                }
                else
                {
                    Options.Logger?.Log(LogLevel.Trace, "{this} worker waiting for cancellation", ToString());
                    await CancelToken.WaitHandle.WaitAsync().DynamicContext();
                }
                Options.Logger?.Log(LogLevel.Debug, "{this} worker done", ToString());
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Options.Logger?.Log(LogLevel.Trace, "{this} worker canceled for disposing", ToString());
            }
            catch (SerializerException ex) when (ex.InnerException is OperationCanceledException && CancelToken.IsCancellationRequested)
            {
                Options.Logger?.Log(LogLevel.Trace, "{this} worker canceled during deserialization", ToString());
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Options.Logger?.Log(LogLevel.Trace, "{this} worker canceled", ToString());
            }
            catch (Exception ex)
            {
                Options.Logger?.Log(LogLevel.Error, "{this} worker catched exception: {ex}", ToString(), ex);
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
            Options.Logger?.Log(LogLevel.Debug, "{this} begin work", ToString());
            foreach (object api in Options.API.Values.Select(a => a.Instance))
                if (api is IWantRpcProcessorInfo processorInfo)
                    processorInfo.ProcessorCancellation = CancelToken;
            await Calls.StartAsync(CancelToken).DynamicContext();
            await Requests.StartAsync(CancelToken).DynamicContext();
            await OutgoingMessages.StartAsync(CancelToken).DynamicContext();
        }

        /// <summary>
        /// Stop exceptional
        /// </summary>
        /// <param name="ex">Exception</param>
        protected virtual async Task StopExceptionalAsync(Exception ex)
        {
            Options.Logger?.Log(LogLevel.Error, "{this} stop exceptional: {ex}", ToString(), ex);
            if (StoppedExceptional || LastException is not null)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} had an exception already", ToString());
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
            Options.Logger?.Log(LogLevel.Debug, "{this} end work", ToString());
            await Requests.StopAsync(CancellationToken.None).DynamicContext();
            await Calls.StopAsync(CancellationToken.None).DynamicContext();
            await OutgoingMessages.StopAsync(CancelToken).DynamicContext();
            foreach (object api in Options.API.Values.Select(a => a.Instance))
                if (api is IWantRpcProcessorInfo processorInfo)
                    processorInfo.ProcessorCancellation = default;
        }

        /// <summary>
        /// Ensure streams are enabled
        /// </summary>
        protected virtual void EnsureStreamsAreEnabled()
        {
            if (Options.MaxStreamCount < 1)
                throw new InvalidOperationException("Streams are disabled");
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Options.Logger?.Log(LogLevel.Trace, "{this} sync disposing", ToString());
            // Cancel outgoing streams
            using (SemaphoreSyncContext ssc = OutgoingStreamsSync)
                foreach (OutgoingStream stream in OutgoingStreams.Values)
                    try
                    {
                        stream.Cancellation.Cancel();
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
            PendingRequests.Values.DisposeAll();
            PendingRequests.Clear();
            // Dispose calls
            Calls.Dispose();
            PendingCalls.Values.DisposeAll();
            PendingCalls.Clear();
            // Dispose the options
            Options.Dispose();
            // Dispose events (if disposable)
            _RemoteEvents.Values.TryDisposeAll();
            _RemoteEvents.Clear();
            // Dispose outgoing streams
            OutgoingStreams.Values.DisposeAll();
            OutgoingStreams.Clear();
            OutgoingStreamsSync.Dispose();
            // Dispose incoming streams
            IncomingStreams.Values.DisposeAll();
            IncomingStreams.Clear();
            IncomingStreamsSync.Dispose();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            Options.Logger?.Log(LogLevel.Trace, "{this} async disposing", ToString());
            // Cancel outgoing streams
            using (SemaphoreSyncContext ssc = await OutgoingStreamsSync.SyncContextAsync().DynamicContext())
                foreach (OutgoingStream stream in OutgoingStreams.Values)
                    try
                    {
                        stream.Cancellation.Cancel();
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
            await PendingRequests.Values.DisposeAllAsync().DynamicContext();
            PendingRequests.Clear();
            // Dispose calls
            await Calls.DisposeAsync().DynamicContext();
            await PendingCalls.Values.DisposeAllAsync().DynamicContext();
            PendingCalls.Clear();
            // Dispose the options
            await Options.DisposeAsync().DynamicContext();
            // Dispose events (if disposable)
            await _RemoteEvents.Values.TryDisposeAllAsync().DynamicContext();
            _RemoteEvents.Clear();
            // Dispose outgoing streams
            await OutgoingStreams.Values.DisposeAllAsync().DynamicContext();
            OutgoingStreams.Clear();
            await OutgoingStreamsSync.DisposeAsync().DynamicContext();
            // Dispose incoming streams
            await IncomingStreams.Values.DisposeAllAsync().DynamicContext();
            IncomingStreams.Clear();
            await IncomingStreamsSync.DisposeAsync().DynamicContext();
        }
    }
}
