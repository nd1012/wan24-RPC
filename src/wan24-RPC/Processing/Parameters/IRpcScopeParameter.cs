using System.Diagnostics.CodeAnalysis;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Scopes;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing.Parameters
{
    /// <summary>
    /// Interface for a RPC scope parameter
    /// </summary>
    public interface IRpcScopeParameter
    {
        /// <summary>
        /// RPC processor
        /// </summary>
        RpcProcessor? Processor { get; }
        /// <summary>
        /// RPC scope value (DTO)
        /// </summary>
        RpcScopeValue? Value { get; }
        /// <summary>
        /// Store the scope?
        /// </summary>
        bool StoreScope { get; }
        /// <summary>
        /// RPC scope type (see <see cref="RpcScopeTypes"/>)
        /// </summary>
        int Type { get; }
        /// <summary>
        /// Scope key
        /// </summary>
        string? Key { get; }
        /// <summary>
        /// Replace an existing keyed scope (will be disposed)?
        /// </summary>
        bool ReplaceExistingScope { get; }
        /// <summary>
        /// Dispose the scope value (NOT the <see cref="Value"/>!) when disposing?
        /// </summary>
        bool DisposeScopeValue { get; set; }
        /// <summary>
        /// If to dispose the scope value (NOT the <see cref="Value"/>!) on error
        /// </summary>
        bool DisposeScopeValueOnError { get; set; }
        /// <summary>
        /// If the scope value (NOT the <see cref="Value"/>!) should be disposed at the current state
        /// </summary>
        public bool ShouldDisposeScopeValue { get; }
        /// <summary>
        /// If there was an error
        /// </summary>
        bool IsError { get; }
        /// <summary>
        /// Inform the scope master when disposing
        /// </summary>
        bool InformMasterWhenDisposing { get; }
        /// <summary>
        /// Create the RPC scope value (DTO)
        /// </summary>
        /// <param name="processor">RPC processor</param>
        /// <param name="id">Scope ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC scope value</returns>
        [MemberNotNull(nameof(Processor), nameof(Value))]
        Task<RpcScopeValue> CreateValueAsync(RpcProcessor processor, long id, CancellationToken cancellationToken);
        /// <summary>
        /// Set the <see cref="IsError"/> value to <see langword="true"/> and perform required actions
        /// </summary>
        Task SetIsErrorAsync();
        /// <summary>
        /// Dispose the scope value (NOT the <see cref="Value"/>!), if it should be disposed
        /// </summary>
        Task DisposeScopeValueAsync();
    }
}
