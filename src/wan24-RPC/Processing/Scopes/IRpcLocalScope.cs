using System.Diagnostics.CodeAnalysis;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Parameters;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// Interface for a local RPC scope (see <see cref="RpcProcessor.RpcScopeBase"/>)
    /// </summary>
    public interface IRpcLocalScope : IRpcScope
    {
        /// <summary>
        /// Scope parameter
        /// </summary>
        IRpcScopeParameter? ScopeParameter { get; }
        /// <summary>
        /// RPC method which returns the scope
        /// </summary>
        RpcApiMethodInfo? Method { get; }
        /// <summary>
        /// Value
        /// </summary>
        object? Value { get; }
        /// <summary>
        /// If to dispose the <see cref="Value"/> on error
        /// </summary>
        bool DisposeValueOnError { get; }
        /// <summary>
        /// If to inform the scope consumer when disposing
        /// </summary>
        bool InformConsumerWhenDisposing { get; set; }
        /// <summary>
        /// If there was an error
        /// </summary>
        bool IsError { get; }
        /// <summary>
        /// Last exception
        /// </summary>
        Exception? LastException { get; }
        /// <summary>
        /// Create a scope parameter (should use <see cref="SetScopeParameter(in IRpcScopeParameter?)"/> to set the parameter to <see cref="ScopeParameter"/>)
        /// </summary>
        /// <param name="apiMethod">RPC API method (for a return value; if <see langword="null"/>, the scope is being used as request parameter)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <exception cref="InvalidOperationException">Not implemented</exception>
        [MemberNotNull(nameof(ScopeParameter))]
        Task CreateScopeParameterAsync(RpcApiMethodInfo? apiMethod = null, CancellationToken cancellationToken = default);
        /// <summary>
        /// Set the <see cref="ScopeParameter"/>
        /// </summary>
        /// <param name="parameter">Scope parameter</param>
        /// <exception cref="InvalidOperationException"><see cref="ScopeParameter"/> was set already</exception>
        void SetScopeParameter(in IRpcScopeParameter? parameter);
        /// <summary>
        /// Register this scope as a remote scope at the peer
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task RegisterRemoteAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// Set the value of <see cref="IsError"/> to <see langword="true"/> and perform required actions
        /// </summary>
        /// <param name="ex">Exception</param>
        [MemberNotNull(nameof(LastException))]
        Task SetIsErrorAsync(Exception ex);
    }
}
