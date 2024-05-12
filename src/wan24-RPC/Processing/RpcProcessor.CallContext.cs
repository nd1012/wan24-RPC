using Microsoft.Extensions.Logging;
using wan24.Core;
using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing
{
    // Call context
    public partial class RpcProcessor
    {
        /// <summary>
        /// RPC call (context)
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        protected record class Call() : DisposableRecordBase()
        {
            /// <summary>
            /// RPC Processor
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
            /// Cancellation (will be disposed)
            /// </summary>
            public CancellationTokenSource Cancellation { get; } = new();

            /// <summary>
            /// Completion
            /// </summary>
            public TaskCompletionSource<object?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Context
            /// </summary>
            public RpcContext? Context { get; set; }

            /// <summary>
            /// If the call was processing (the targeted API method has been called)
            /// </summary>
            public bool WasProcessing { get; set; }

            /// <summary>
            /// Incoming streams from parameters (will be disposed)
            /// </summary>
            public HashSet<IncomingStream> ParameterStreams { get; } = [];

            /// <summary>
            /// Outgoing stream from the return value (will be disposed on error)
            /// </summary>
            public OutgoingStream? ReturnStream { get; set; }

            /// <summary>
            /// Set done
            /// </summary>
            public virtual void SetDone()
            {
                if (IsDone)
                    return;
                Done = DateTime.Now;
                Processor.Logger?.Log(LogLevel.Debug, "{processor} RPC call #{id} processing done within {runtime}", Processor.ToString(), Message.Id, Runtime);
            }

            /// <summary>
            /// Handle an exception (this method should not throw)
            /// </summary>
            /// <param name="exception">Exception</param>
            public virtual async Task HandleExceptionAsync(Exception exception)
            {
                try
                {
                    Completion.TrySetException(exception);
                    if (Message is RequestMessage request && request.Parameters is not null)
                        await Processor.HandleValuesOnErrorAsync(outgoing: false, exception, request.Parameters).DynamicContext();
                    if (ReturnStream is not null)
                    {
                        await Processor.RemoveOutgoingStreamAsync(ReturnStream).DynamicContext();
                        await ReturnStream.DisposeAsync().DynamicContext();
                    }
                }
                catch
                {
                }
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                Cancellation.Cancel();
                Cancellation.Dispose();
                if (!Completion.Task.IsCompleted)
                    Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                SetDone();
                if (ParameterStreams.Any(s => !s.IsDisposing))
                {
                    List<IncomingStream> disposeStreams = [];
                    using (SemaphoreSyncContext ssc = Processor.IncomingStreamsSync)
                        foreach (IncomingStream stream in ParameterStreams.Where(s => !s.IsDisposing))
                        {
                            Processor.RemoveIncomingStream(stream);
                            if (stream.ApiParameter?.DisposeParameterValue ?? stream.ApiParameter?.Stream?.DisposeRpcStream ?? true)
                                disposeStreams.Add(stream);
                        }
                    disposeStreams.DisposeAll();
                }
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                Cancellation.Cancel();
                Cancellation.Dispose();
                if (!Completion.Task.IsCompleted)
                    Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                SetDone();
                if (ParameterStreams.Any(s => !s.IsDisposing))
                {
                    List<IncomingStream> disposeStreams = [];
                    using (SemaphoreSyncContext ssc = await Processor.IncomingStreamsSync.SyncContextAsync().DynamicContext())
                        foreach (IncomingStream stream in ParameterStreams.Where(s => !s.IsDisposing))
                        {
                            Processor.RemoveIncomingStream(stream);
                            if (stream.ApiParameter?.DisposeParameterValue ?? stream.ApiParameter?.Stream?.DisposeRpcStream ?? true)
                                disposeStreams.Add(stream);
                        }
                    await disposeStreams.DisposeAllAsync().DynamicContext();
                }
            }
        }
    }
}
