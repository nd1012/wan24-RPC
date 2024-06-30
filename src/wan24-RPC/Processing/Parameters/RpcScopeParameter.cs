using System.Diagnostics.CodeAnalysis;
using wan24.RPC.Processing.Scopes;

namespace wan24.RPC.Processing.Parameters
{
    /// <summary>
    /// <see cref="RpcScope"/> parameter
    /// </summary>
    public sealed record class RpcScopeParameter : RpcScopeParameterBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        [SetsRequiredMembers]
        public RpcScopeParameter() : base() => Type = (int)RpcScopeTypes.Scope;

        /// <inheritdoc/>
        public override Task DisposeScopeValueAsync() => Task.CompletedTask;
    }
}
