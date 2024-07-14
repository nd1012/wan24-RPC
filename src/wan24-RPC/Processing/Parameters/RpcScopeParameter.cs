using System.Diagnostics.CodeAnalysis;
using wan24.Core;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Scopes;

namespace wan24.RPC.Processing.Parameters
{
    /// <summary>
    /// <see cref="RpcScope"/> parameter
    /// </summary>
    public record class RpcScopeParameter : RpcScopeParameterBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        [SetsRequiredMembers]
        public RpcScopeParameter() : this(RpcScope.TYPE) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">RPC scope type (see <see cref="RpcScopeTypes"/>)</param>
        [SetsRequiredMembers]
        protected RpcScopeParameter(in int type) : base() => Type = type;

        /// <summary>
        /// Scope object (will be disposed)
        /// </summary>
        public virtual object? ScopeObject { get; set; }

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
        /// <param name="apiMethod">RPC API method</param>
        /// <param name="cancellationToken">Cancelation token</param>
        /// <returns>Scope parameter</returns>
        public static Task<IRpcScopeParameter> CreateAsync(
            RpcProcessor processor, 
            RpcProcessor.RpcScopeBase scope, 
            RpcApiMethodInfo? apiMethod = null, 
            CancellationToken cancellationToken = default
            )
            => Task.FromResult<IRpcScopeParameter>(new RpcScopeParameter()
            {
                Processor = processor
            });
    }
}
