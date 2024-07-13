#if !RELEASE //TODO Enable when fully implemented
using wan24.RPC.Processing;

namespace wan24.RPC.Sdk.Generator
{
    /// <summary>
    /// RPC C# SDK generator options (don't forget to dispose!)
    /// </summary>
    public record class RpcCSharpSdkGeneratorOptions():RpcSdkGeneratorOptions()
    {
        /// <inheritdoc/>
        public sealed override int Target => (int)RpcSdkGeneratorTargets.CSharp;

        /// <summary>
        /// If to dispose the RPC processor the SDK uses, when the SDK is being disposed
        /// </summary>
        public bool DisposeProcessor { get; init; } = true;

        /// <summary>
        /// SDK namespace name (f.e. <c>Your.NameSpace</c>)
        /// </summary>
        public string NameSpace { get; init; } = "wan24.RPC";

        /// <summary>
        /// SDK type name (f.e. <c>YourSdk</c>)
        /// </summary>
        public string Sdk { get; init; } = "RpcSdk";

        /// <summary>
        /// RPC processor type (must be a <see cref="Processing.RpcProcessor"/>)
        /// </summary>
        public Type RpcProcessor { get; init; } = typeof(RpcProcessor);

        /// <summary>
        /// If the <see cref="RpcProcessor"/> is an <see cref="IRpcProcessorInternals"/> and <see cref="RpcSdkBaseExt{T}"/> should be used as base type for the generated 
        /// SDK
        /// </summary>
        public bool Extended { get; init; }
    }
}
#endif
