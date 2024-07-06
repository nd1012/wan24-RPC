using System.Diagnostics.CodeAnalysis;
using wan24.Core;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing.Parameters
{
    /// <summary>
    /// Base class for a RPC scope parameter
    /// </summary>
    public abstract record class RpcScopeParameterBase() : IRpcScopeParameter
    {
        /// <summary>
        /// If to store the scope at the peer
        /// </summary>
        protected bool _StoreScope = false;

        /// <inheritdoc/>
        public RpcProcessor? Processor { get; protected set; }

        /// <inheritdoc/>
        public RpcScopeValue? Value { get; protected set; }

        /// <inheritdoc/>
        public virtual bool StoreScope
        {
            get => _StoreScope || Key is not null || ReplaceExistingScope;
            init => _StoreScope = value;
        }

        /// <inheritdoc/>
        public required int Type { get; init; }

        /// <inheritdoc/>
        public string? Key { get; init; }

        /// <inheritdoc/>
        public bool ReplaceExistingScope { get; set; }

        /// <inheritdoc/>
        public bool DisposeScopeValue { get; set; } = true;

        /// <inheritdoc/>
        public bool DisposeScopeValueOnError { get; set; } = true;

        /// <inheritdoc/>
        public virtual bool ShouldDisposeScopeValue => DisposeScopeValue || (DisposeScopeValueOnError && IsError);

        /// <inheritdoc/>
        public bool IsError { get; protected set; }

        /// <inheritdoc/>
        public bool InformMasterWhenDisposing { get; set; } = true;

        /// <inheritdoc/>
        [MemberNotNull(nameof(Processor), nameof(Value))]
        public virtual Task<RpcScopeValue> CreateValueAsync(RpcProcessor processor, long id, CancellationToken cancellationToken)
        {
            if (IsError)
                throw new InvalidOperationException("Invalid error state");
            if (Value is not null)
                throw new InvalidOperationException("Value created already");
            ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);
            Processor = processor;
            Value = new()
            {
                Parameter = this,
                Id = id,
                Key = Key,
                ReplaceExistingScope = ReplaceExistingScope,
                Type = Type,
                IsStored = StoreScope,
                DisposeScopeValue = DisposeScopeValue,
                DisposeScopeValueOnError = DisposeScopeValueOnError,
                InformMasterWhenDisposing = InformMasterWhenDisposing
            };
            return Task.FromResult(Value);
        }

        /// <inheritdoc/>
        public virtual async Task SetIsErrorAsync()
        {
            if (IsError)
                return;
            IsError = true;
            if (ShouldDisposeScopeValue)
                await DisposeScopeValueAsync().DynamicContext();
        }

        /// <inheritdoc/>
        public abstract Task DisposeScopeValueAsync();
    }
}
