using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Messages;

/*
 * This RPC context object will be available to a called RPC API method as non-RPC parameter using DI.
 */

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
        /// Unauthorized context handler
        /// </summary>
        public static Unauthorized_Delegate? UnauthorizedHandler { get; set; }

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

        /// <summary>
        /// Unauthorized handler delegate
        /// </summary>
        /// <param name="context">RPC context</param>
        /// <param name="authZ">Authorization attribute</param>
        public delegate Task Unauthorized_Delegate(RpcContext context, RpcAuthorizationAttributeBase authZ);
    }
}
