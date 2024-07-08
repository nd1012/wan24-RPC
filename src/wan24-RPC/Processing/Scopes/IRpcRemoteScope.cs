using System.Diagnostics.CodeAnalysis;
using wan24.RPC.Api.Reflection;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// Interface for a remote scope (see <see cref="RpcProcessor.RpcRemoteScopeBase"/>)
    /// </summary>
    public interface IRpcRemoteScope : IRpcScope
    {
        /// <summary>
        /// Received RPC scope value
        /// </summary>
        RpcScopeValue ScopeValue { get; }
        /// <summary>
        /// RPC method parameter which got the scope
        /// </summary>
        RpcApiMethodParameterInfo? Parameter { get; }
        /// <summary>
        /// If to replace the existing keyed remote scope
        /// </summary>
        bool ReplaceExistingScope { get; }
        /// <summary>
        /// Value
        /// </summary>
        object? Value { get; }
        /// <summary>
        /// Dispose the <see cref="Value"/> when disposing?
        /// </summary>
        bool DisposeValue { get; set; }
        /// <summary>
        /// If to dispose the <see cref="Value"/> on error
        /// </summary>
        bool DisposeValueOnError { get; }
        /// <summary>
        /// If there was an error
        /// </summary>
        bool IsError { get; }
        /// <summary>
        /// If to inform the scope master when disposing
        /// </summary>
        bool InformMasterWhenDisposing { get; set; }
        /// <summary>
        /// Last exception
        /// </summary>
        Exception? LastException { get; }
        /// <summary>
        /// Set the value of <see cref="IsError"/> to <see langword="true"/> and perform required actions
        /// </summary>
        /// <param name="ex">Exception</param>
        [MemberNotNull(nameof(LastException))]
        Task SetIsErrorAsync(Exception ex);
    }
}
