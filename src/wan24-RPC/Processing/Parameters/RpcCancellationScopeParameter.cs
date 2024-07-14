using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Net.Sockets;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Scopes;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing.Parameters
{
    /// <summary>
    /// RPC cancellation scope parameter
    /// </summary>
    public record class RpcCancellationScopeParameter : RpcScopeParameterBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        [SetsRequiredMembers]
        public RpcCancellationScopeParameter() : this(RpcCancellationScope.TYPE) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">RPC scope type (see <see cref="RpcScopeTypes"/>)</param>
        [SetsRequiredMembers]
        protected RpcCancellationScopeParameter(in int type) : base() => Type = type;

        /// <summary>
        /// Scope
        /// </summary>
        public RpcCancellationScope? Scope { get; set; }

        /// <summary>
        /// Cancellation token to use
        /// </summary>
        public CancellationToken Token { get; set; }

        /// <inheritdoc/>
        public override Task DisposeScopeValueAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public override Task<RpcScopeValue> CreateValueAsync(RpcProcessor processor, long id, CancellationToken cancellationToken)
        {
            if (IsError)
                throw new InvalidOperationException("Invalid error state");
            if (Value is not null)
                throw new InvalidOperationException("Value created already");
            ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);
            Contract.Assert(Scope is not null);
            Processor = processor;
            Value = new RpcCancellationScopeValue()
            {
                Parameter = this,
                Id = id,
                Key = Key,
                ReplaceExistingScope = ReplaceExistingScope,
                Type = Type,
                IsStored = !Scope.CancelToken.IsCancellationRequested,
                DisposeScopeValue = DisposeScopeValue,
                DisposeScopeValueOnError = DisposeScopeValueOnError,
                InformMasterWhenDisposing = InformMasterWhenDisposing
            };
            return Task.FromResult(Value);
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
        {
            if (scope is not RpcCancellationScope cancellation)
                throw new InvalidProgramException($"{scope.GetType()} isn't a {typeof(RpcCancellationScope)}");
            return Task.FromResult<IRpcScopeParameter>(new RpcCancellationScopeParameter()
            {
                Processor = processor,
                Scope = cancellation,
                Token = cancellation.Token,
                Key = cancellation.Key,
                StoreScope = !cancellation.Token.IsCancellationRequested,
                DisposeScopeValue = false,
                DisposeScopeValueOnError = false,
                InformMasterWhenDisposing = !cancellation.Token.IsCancellationRequested
            });
        }
    }
}
