using wan24.RPC.Processing.Parameters;

namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Base class for an attribute which configures a RPC scope
    /// </summary>
    public abstract class RpcScopeAttributeBase() : RpcAttributeBase()
    {
        /// <summary>
        /// Initialize a scope parameter for a return value
        /// </summary>
        /// <param name="parameter">Scope parameter</param>
        public abstract void InitializeScopeParameter(in IRpcScopeParameter parameter);
    }
}
