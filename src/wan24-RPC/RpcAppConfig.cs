using System.Text.Json.Serialization;
using wan24.Core;
using wan24.RPC.Processing.Scopes;

namespace wan24.RPC
{
    /// <summary>
    /// RPC app configuration
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RpcAppConfig() : AppConfigBase()
    {
        /// <summary>
        /// Applied compression app configuration
        /// </summary>
        [JsonIgnore]
        public static RpcAppConfig? AppliedRpcConfig { get; protected set; }

        /// <summary>
        /// Disabled scope type IDs (see <see cref="RpcScopeTypes"/>)
        /// </summary>
        public int[]? DisabledScopeTypes { get; set; }

        /// <inheritdoc/>
        public override void Apply()
        {
            if (SetApplied)
            {
                if (AppliedRpcConfig is not null) throw new InvalidOperationException();
                AppliedRpcConfig = this;
            }
            ApplyProperties(afterBootstrap: false);
            if (DisabledScopeTypes is not null)
                foreach (int id in DisabledScopeTypes)
                    RpcScopes.Registered.Remove(id);
            ApplyProperties(afterBootstrap: true);
        }

        /// <inheritdoc/>
        public override async Task ApplyAsync(CancellationToken cancellationToken = default)
        {
            if (SetApplied)
            {
                if (AppliedRpcConfig is not null) throw new InvalidOperationException();
                AppliedRpcConfig = this;
            }
            await ApplyPropertiesAsync(afterBootstrap: false, cancellationToken).DynamicContext();
            if (DisabledScopeTypes is not null)
                foreach (int id in DisabledScopeTypes)
                    RpcScopes.Registered.Remove(id);
            await ApplyPropertiesAsync(afterBootstrap: true, cancellationToken).DynamicContext();
        }
    }
}
