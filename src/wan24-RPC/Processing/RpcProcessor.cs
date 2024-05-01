using wan24.Core;
using wan24.RPC.Api.Messages;
using wan24.RPC.Api.Messages.Serialization.Extensions;
using wan24.RPC.Api.Reflection;

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
            Calls = new(this);
            Requests = new(this);
        }

        /// <summary>
        /// Options (will be disposed)
        /// </summary>
        public RpcProcessorOptions Options { get; }

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
                };

        /// <inheritdoc/>
        protected override async Task WorkerAsync()
        {
            await BeginWorkAsync().DynamicContext();
            try
            {
                if (Options.API.Count > 0 && Options.Stream.CanRead)
                {
                    while (!CancelToken.IsCancellationRequested)
                        _ = HandleMessageAsync(await Options.Stream.ReadRpcMessageAsync(Options.SerializerVersion, cancellationToken: CancelToken).DynamicContext());
                }
                else
                {
                    await CancelToken.WaitHandle.WaitAsync().DynamicContext();
                }
            }
            catch(OperationCanceledException ex) when (ex.CancellationToken == CancelToken)
            {
            }
            catch
            {
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
            await Calls.StartAsync(CancelToken).DynamicContext();
            await Requests.StartAsync(CancelToken).DynamicContext();
        }

        /// <summary>
        /// Stop exceptional
        /// </summary>
        /// <param name="ex">Exception</param>
        protected virtual async Task StopExceptionalAsync(Exception ex)
        {
            if (StoppedExceptional || LastException is not null)
            {
                //TODO Handle error
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
            await Requests.StopAsync(CancellationToken.None).DynamicContext();
            await Calls.StopAsync(CancellationToken.None).DynamicContext();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            WriteSync.Dispose();
            Requests.Dispose();
            PendingRequests.Values.DisposeAll();
            Calls.Dispose();
            PendingCalls.Values.DisposeAll();
            Options.Dispose();
            PendingRequests.Clear();
            PendingCalls.Clear();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            await WriteSync.DisposeAsync().DynamicContext();
            await Requests.DisposeAsync().DynamicContext();
            await PendingRequests.Values.DisposeAllAsync().DynamicContext();
            await Calls.DisposeAsync().DynamicContext();
            await PendingCalls.Values.DisposeAllAsync().DynamicContext();
            await Options.DisposeAsync().DynamicContext();
            PendingRequests.Clear();
            PendingCalls.Clear();
        }
    }
}
