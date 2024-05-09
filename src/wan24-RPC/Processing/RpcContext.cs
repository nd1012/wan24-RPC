using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// RPC context
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public record class RpcContext() : DisposableRecordBase()
    {
        /// <summary>
        /// Created time
        /// </summary>
        public DateTime Created { get; init; } = DateTime.Now;

        /// <summary>
        /// Done time
        /// </summary>
        public DateTime Done { get; protected set; } = DateTime.MinValue;

        /// <summary>
        /// RPC processor
        /// </summary>
        public required RpcProcessor Processor { get; init; }

        /// <summary>
        /// Cancellation
        /// </summary>
        public CancellationToken Cancellation { get; init; }

        /// <summary>
        /// RPC request
        /// </summary>
        public required RequestMessage Message { get; init; }

        /// <summary>
        /// RPC API
        /// </summary>
        public RpcApiInfo API => Method.API;

        /// <summary>
        /// RPC API method
        /// </summary>
        public required RpcApiMethodInfo Method { get; init; }

        /// <summary>
        /// Service provider (will be disposed)
        /// </summary>
        public required ScopedDiHelper Services { get; init; }

        /// <summary>
        /// Set done
        /// </summary>
        public virtual void SetDone()
        {
            if (Done == DateTime.MinValue)
                Done = DateTime.Now;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            Services.Dispose();
            SetDone();
        }

        /// <inheritdoc/>
        protected override async Task DisposeCore()
        {
            await Services.DisposeAsync().DynamicContext();
            SetDone();
        }
    }
}
