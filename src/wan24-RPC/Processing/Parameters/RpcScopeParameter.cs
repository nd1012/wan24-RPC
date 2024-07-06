using System.Diagnostics.CodeAnalysis;
using wan24.Core;
using wan24.RPC.Api.Reflection;
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

        /// <summary>
        /// Scope object (will be disposed)
        /// </summary>
        public object? ScopeObject { get; set; }

        /// <inheritdoc/>
        public override async Task DisposeScopeValueAsync()
        {
            if (!ShouldDisposeScopeValue || ScopeObject is null)
                return;
            await ScopeObject.TryDisposeAsync().DynamicContext();
        }

        /// <summary>
        /// Create
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="scope">RPC scope</param>
        /// <param name="apiMethod">RPC API method (if <see langword="null"/>, the scope was used as request parameter)</param>
        /// <param name="cancellationToken">Cancelation token</param>
        /// <returns>Scope parameter</returns>
        public static Task<IRpcScopeParameter> CreateAsync(
            RpcProcessor processor, 
            RpcProcessor.RpcScopeBase scope, 
            RpcApiMethodInfo? apiMethod = null, 
            CancellationToken cancellationToken = default
            )
        {
            RpcScopeParameter parameter = new();
            apiMethod?.InitializeScopeParameter(parameter);
            return Task.FromResult<IRpcScopeParameter>(parameter);
        }
    }
}
