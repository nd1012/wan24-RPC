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
            /// Cancellation
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
            /// If the call was processing
            /// </summary>
            public bool WasProcessing { get; set; }

            /// <summary>
            /// Incoming streams from parameters
            /// </summary>
            public HashSet<IncomingStream> ParameterStreams { get; } = [];

            /// <summary>
            /// Outgoing stream from the return value
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
            /// Handle an exception
            /// </summary>
            /// <param name="ex">Exception</param>
            public virtual async Task HandleExceptionAsync(Exception ex)
            {
                if (ParameterStreams.Any(s => !s.IsDisposing))
                {
                    using (SemaphoreSyncContext ssc = await Processor.IncomingStreamsSync.SyncContextAsync().DynamicContext())
                        foreach (IncomingStream stream in ParameterStreams.Where(s => !s.IsDisposing))
                            Processor.RemoveIncomingStream(stream);
                    await ParameterStreams.Where(s => !s.IsDisposing).DisposeAllAsync().DynamicContext();
                }
                if (ReturnStream is not null && !ReturnStream.IsDisposing)
                {
                    await Processor.RemoveOutgoingStreamAsync(ReturnStream).DynamicContext();
                    await ReturnStream.DisposeAsync().DynamicContext();
                }
            }

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                Cancellation.Cancel();
                Cancellation.Dispose();
                Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                SetDone();
                if (ParameterStreams.Any(s => !s.IsDisposing))
                {
                    List<IncomingStream> disposeStreams = [];
                    using (SemaphoreSyncContext ssc = Processor.IncomingStreamsSync)
                        foreach (IncomingStream stream in ParameterStreams.Where(s => !s.IsDisposing))
                            if (stream.ApiParameter?.DisposeParameterValue ?? stream.ApiParameter?.Stream?.DisposeRpcStream ?? true)
                            {
                                Processor.RemoveIncomingStream(stream);
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
                Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                SetDone();
                if (ParameterStreams.Any(s => !s.IsDisposing))
                {
                    List<IncomingStream> disposeStreams = [];
                    using (SemaphoreSyncContext ssc = await Processor.IncomingStreamsSync.SyncContextAsync().DynamicContext())
                        foreach (IncomingStream stream in ParameterStreams.Where(s => !s.IsDisposing))
                            if (stream.ApiParameter?.DisposeParameterValue ?? stream.ApiParameter?.Stream?.DisposeRpcStream ?? true)
                            {
                                Processor.RemoveIncomingStream(stream);
                                disposeStreams.Add(stream);
                            }
                    await disposeStreams.DisposeAllAsync().DynamicContext();
                }
            }
        }
    }
}
