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
            /// Outgoing parameter streams
            /// </summary>
            public HashSet<OutgoingStream> ParameterStreams { get; } = [];

            /// <summary>
            /// The returned incoming stream
            /// </summary>
            public IncomingStream? ReturnStream { get; set; }

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
                    if (ReturnStream is not null)
                    {
                        await Processor.RemoveIncomingStreamAsync(ReturnStream).DynamicContext();
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
                SetDone();
                if (!RequestCompletion.Task.IsCompleted || !ProcessorCompletion.Task.IsCompleted)
                {
                    ObjectDisposedException exception = new(GetType().ToString());
                    RequestCompletion.TrySetException(exception);
                    ProcessorCompletion.TrySetException(exception);
                }
                if (ParameterStreams.Any(s => !s.IsDisposing))
                {
                    List<OutgoingStream> disposeStreams = [];
                    using (SemaphoreSyncContext ssc = Processor.OutgoingStreamsSync)
                        foreach (OutgoingStream stream in ParameterStreams.Where(s => !s.IsDisposing))
                            if (stream.Parameter.DisposeRpcStream)
                            {
                                Processor.RemoveOutgoingStream(stream);
                                disposeStreams.Add(stream);
                            }
                    disposeStreams.DisposeAll();
                }
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                SetDone();
                if (!RequestCompletion.Task.IsCompleted || !ProcessorCompletion.Task.IsCompleted)
                {
                    ObjectDisposedException exception = new(GetType().ToString());
                    RequestCompletion.TrySetException(exception);
                    ProcessorCompletion.TrySetException(exception);
                }
                if (ParameterStreams.Any(s => !s.IsDisposing))
                {
                    List<OutgoingStream> disposeStreams = [];
                    using (SemaphoreSyncContext ssc = await Processor.OutgoingStreamsSync.SyncContextAsync().DynamicContext())
                        foreach (OutgoingStream stream in ParameterStreams.Where(s => !s.IsDisposing))
                            if (stream.Parameter.DisposeRpcStream)
                            {
                                Processor.RemoveOutgoingStream(stream);
                                disposeStreams.Add(stream);
                            }
                    await disposeStreams.DisposeAllAsync().DynamicContext();
                }
            }
        }
    }
}
