using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing
{
    // Request context
    public partial class RpcProcessor
    {
        /// <summary>
        /// RPC request (context)
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        protected record class Request() : DisposableRecordBase()
        {
            /// <summary>
            /// RPC processor
            /// </summary>
            public required RpcProcessor Processor { get; init; }

            /// <summary>
            /// Created time
            /// </summary>
            public DateTime Created { get; } = DateTime.Now;

            /// <summary>
            /// Done time
            /// </summary>
            public DateTime Done { get; protected set; } = DateTime.MinValue;

            /// <summary>
            /// If done
            /// </summary>
            public bool IsDone => Done != DateTime.MinValue;

            /// <summary>
            /// Runtime
            /// </summary>
            public TimeSpan Runtime => IsDone
                ? Done - Created
                : DateTime.Now - Created;

            /// <summary>
            /// Message
            /// </summary>
            public required IRpcRequest Message { get; init; }

            /// <summary>
            /// Message ID
            /// </summary>
            public long Id => Message.Id ?? throw new InvalidDataException("Missing message ID");

            /// <summary>
            /// Expected return value type
            /// </summary>
            public Type? ExpectedReturnType { get; init; }

            /// <summary>
            /// Cancellation token
            /// </summary>
            public CancellationToken Cancellation { get; init; }

            /// <summary>
            /// Processor completion (completes the request queue processing)
            /// </summary>
            public TaskCompletionSource<object?> ProcessorCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Request completion (completes the whole request)
            /// </summary>
            public TaskCompletionSource<object?> RequestCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// If the request was processed by the peer (the request message was sent)
            /// </summary>
            public bool WasProcessing { get; set; }

            /// <summary>
            /// Scope parameters
            /// </summary>
            public HashSet<RpcScopeBase> ParameterScopes { get; } = [];

            /// <summary>
            /// The returned remote scope
            /// </summary>
            public RpcRemoteScopeBase? ReturnScope { get; set; }

            /// <summary>
            /// Set done
            /// </summary>
            public virtual void SetDone()
            {
                if (IsDone)
                    return;
                Done = DateTime.Now;
                Processor.Logger?.Log(LogLevel.Trace, "{processor} RPC request #{id} processing done within {runtime}", Processor.ToString(), Message.Id, Runtime);
            }

            /// <summary>
            /// Handle an exception (this method should not throw)
            /// </summary>
            /// <param name="exception">Exception</param>
            public virtual async Task HandleExceptionAsync(Exception exception)
            {
                try
                {
                    RequestCompletion.TrySetException(exception);
                    ProcessorCompletion.TrySetException(exception);
                    foreach (RpcScopeBase scope in ParameterScopes)
                        await scope.SetIsErrorAsync(exception).DynamicContext();
                    if (ReturnScope is not null)
                        await ReturnScope.SetIsErrorAsync(exception).DynamicContext();
                }
                catch (Exception ex)
                {
                    Processor.Logger?.Log(LogLevel.Warning, "Request context #{id} failed to handle processing error: {ex}", Id, ex);
                }
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                Processor.Logger?.Log(LogLevel.Trace, "{processor} request context #{id} disposing", Processor.ToString(), Id);
                SetDone();
                if (!RequestCompletion.Task.IsCompleted || !ProcessorCompletion.Task.IsCompleted)
                {
                    ObjectDisposedException exception = new(GetType().ToString());
                    RequestCompletion.TrySetException(exception);
                    ProcessorCompletion.TrySetException(exception);
                }
                if (RequestCompletion.Task.IsFaulted)
                {
                    ParameterScopes.DisposeAll();
                    ReturnScope?.Dispose();
                }
                else
                {
                    ParameterScopes.Where(s => !s.IsStored).DisposeAll();
                    if (!(ReturnScope?.IsStored ?? true))
                        ReturnScope?.Dispose();
                }
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                Processor.Logger?.Log(LogLevel.Trace, "{processor} request context #{id} disposing", Processor.ToString(), Id);
                SetDone();
                if (!RequestCompletion.Task.IsCompleted || !ProcessorCompletion.Task.IsCompleted)
                {
                    ObjectDisposedException exception = new(GetType().ToString());
                    RequestCompletion.TrySetException(exception);
                    ProcessorCompletion.TrySetException(exception);
                }
                if (RequestCompletion.Task.IsFaulted)
                {
                    await ParameterScopes.DisposeAllAsync().DynamicContext();
                    if (ReturnScope is not null)
                        await ReturnScope.DisposeAsync().DynamicContext();
                }
                else
                {
                    await ParameterScopes.Where(s => !s.IsStored).DisposeAllAsync().DynamicContext();
                    if (!(ReturnScope?.IsStored ?? true))
                        await ReturnScope.DisposeAsync().DynamicContext();
                }
            }
        }
    }
}
