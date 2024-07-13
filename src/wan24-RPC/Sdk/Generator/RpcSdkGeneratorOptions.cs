#if !RELEASE //TODO Enable when fully implemented
using wan24.Core;
using wan24.RPC.Api.Reflection;

namespace wan24.RPC.Sdk.Generator
{
    /// <summary>
    /// SDK generator options
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public abstract record class RpcSdkGeneratorOptions() : DisposableRecordBase(asyncDisposing: false)
    {
        /// <summary>
        /// RPC SDK generator target
        /// </summary>
        public abstract int Target { get; }

        /// <summary>
        /// API
        /// </summary>
        public required Dictionary<string, RpcApiInfo> API { get; init; }

        /// <summary>
        /// SDK description (used in documentation)
        /// </summary>
        public string Description { get; init; } = "RPC SDK";

        /// <inheritdoc/>
        protected override void Dispose(bool disposing) => API.Values.DisposeAll();
    }
}
#endif
