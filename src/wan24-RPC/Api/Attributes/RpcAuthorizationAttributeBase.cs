using wan24.RPC.Processing;

namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Base type for a RPC API and/or method authorization attribue
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public abstract class RpcAuthorizationAttributeBase() : Attribute()
    {
        /// <summary>
        /// Determine if a context is authorized
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>If authorized (if <see langword="false"/>, the peer will be disconnected)</returns>
        public abstract Task<bool> IsAuthorizedAsync(RpcContext context);
    }
}
