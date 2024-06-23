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
            /// Message ID
            /// </summary>
            public long Id => Message.Id ?? throw new InvalidDataException("Missing message ID");

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
            /// If the called API method returned without error
            /// </summary>
            public bool DidReturn { get; set; }

            /// <summary>
            /// Remote scopes from parameters
            /// </summary>
            public HashSet<RpcRemoteScopeBase> ParameterScopes { get; } = [];

            /// <summary>
            /// Scope return value
            /// </summary>
            public RpcScopeBase? ReturnScope { get; set; }

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

            /// <inheritdoc/>
            protected override void Dispose(bool disposing)
            {
                if (!Completion.Task.IsCompleted)
                    Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                Cancellation.Cancel();
                Cancellation.Dispose();
                SetDone();
            }

            /// <inheritdoc/>
            protected override async Task DisposeCore()
            {
                if (!Completion.Task.IsCompleted)
                    Completion.TrySetException(new ObjectDisposedException(GetType().ToString()));
                await Cancellation.CancelAsync().DynamicContext();
                Cancellation.Dispose();
                SetDone();
            }
        }
    }
}
