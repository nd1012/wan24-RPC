using Microsoft.Extensions.Logging;
using System.Diagnostics;
using wan24.Core;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Messages.Interfaces;
using wan24.RPC.Api.Messages.Serialization.Extensions;
using wan24.RPC.Api.Reflection;
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
            Calls = new(this)
            {
                Name = "Incoming RPC calls"
            };
            Requests = new(this)
            {
                Name = "Outgoing RPC requests"
            };
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
                Options.Logger?.Log(LogLevel.Debug, "{this} worker", this);
                await BeginWorkAsync().DynamicContext();
                if (Options.API.Count > 0 && Options.Stream.CanRead)
                {
                    IRpcMessage? message;
                    while (!CancelToken.IsCancellationRequested)
                    {
                        Options.Logger?.Log(LogLevel.Trace, "{this} worker waiting for incoming RPC messages", this);
                        message = await Options.Stream.ReadRpcMessageAsync(Options.SerializerVersion, cancellationToken: CancelToken).DynamicContext();
                        Options.Logger?.Log(LogLevel.Trace, "{this} worker handling incoming RPC message", this);
                        _ = HandleMessageAsync(message);
                        message = null;
                    }
                }
                else
                {
                    Options.Logger?.Log(LogLevel.Trace, "{this} worker waiting for cancellation", this);
                    await CancelToken.WaitHandle.WaitAsync().DynamicContext();
                }
                Options.Logger?.Log(LogLevel.Debug, "{this} worker done", this);
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Options.Logger?.Log(LogLevel.Trace, "{this} worker canceled for disposing", this);
            }
            catch (SerializerException ex) when (ex.InnerException is OperationCanceledException && CancelToken.IsCancellationRequested)
            {
                Options.Logger?.Log(LogLevel.Trace, "{this} worker canceled during deserialization", this);
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Options.Logger?.Log(LogLevel.Trace, "{this} worker canceled", this);
            }
            catch (Exception ex)
            {
                Options.Logger?.Log(LogLevel.Error, "{this} worker catched exception: {ex}", this, ex);
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
            Options.Logger?.Log(LogLevel.Debug, "{this} begin work", this);
            await Calls.StartAsync(CancelToken).DynamicContext();
            await Requests.StartAsync(CancelToken).DynamicContext();
        }

        /// <summary>
        /// Stop exceptional
        /// </summary>
        /// <param name="ex">Exception</param>
        protected virtual async Task StopExceptionalAsync(Exception ex)
        {
            Options.Logger?.Log(LogLevel.Error, "{this} stop exceptional: {ex}", this, ex);
            if (StoppedExceptional || LastException is not null)
            {
                Options.Logger?.Log(LogLevel.Warning, "{this} had an exception already", this);
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
            Options.Logger?.Log(LogLevel.Debug, "{this} end work", this);
            await Requests.StopAsync(CancellationToken.None).DynamicContext();
            await Calls.StopAsync(CancellationToken.None).DynamicContext();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Options.Logger?.Log(LogLevel.Trace, "{this} sync disposing", this);
            base.Dispose(disposing);
            WriteSync.Dispose();
            Requests.Dispose();
            PendingRequests.Values.DisposeAll();
            Calls.Dispose();
            PendingCalls.Values.DisposeAll();
            Options.Dispose();
            PendingRequests.Clear();
            PendingCalls.Clear();
            _RemoteEvents.Values.TryDisposeAll();
            _RemoteEvents.Clear();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            Options.Logger?.Log(LogLevel.Trace, "{this} async disposing", this);
            await base.DisposeCore().DynamicContext();
            await WriteSync.DisposeAsync().DynamicContext();
            await Requests.DisposeAsync().DynamicContext();
            await PendingRequests.Values.DisposeAllAsync().DynamicContext();
            await Calls.DisposeAsync().DynamicContext();
            await PendingCalls.Values.DisposeAllAsync().DynamicContext();
            await Options.DisposeAsync().DynamicContext();
            PendingRequests.Clear();
            PendingCalls.Clear();
            await _RemoteEvents.Values.TryDisposeAllAsync().DynamicContext();
            _RemoteEvents.Clear();
        }
    }
}
