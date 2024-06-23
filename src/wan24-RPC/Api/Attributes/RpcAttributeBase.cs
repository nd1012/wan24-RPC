using wan24.RPC.Api.Reflection;

namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Base class for an extended RPC attribute
    /// </summary>
    public abstract class RpcAttributeBase() : Attribute()
    {
        /// <summary>
        /// Handle an assigned API
        /// </summary>
        /// <param name="api">API</param>
        public virtual void HandleAssignedApi(RpcApiInfo api) { }

        /// <summary>
        /// Handle an assigned API method
        /// </summary>
        /// <param name="method">Method</param>
        public virtual void HandleAssignedApiMethod(RpcApiMethodInfo method) { }

        /// <summary>
        /// Handle an assigned API method parameter
        /// </summary>
        /// <param name="parameter">Parameter</param>
        public virtual void HandleAssignedApiMethodParameter(RpcApiMethodParameterInfo parameter) { }
    }
}
