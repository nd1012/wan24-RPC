using wan24.Core;
using wan24.RPC.Processing;

namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute extensions
    /// </summary>
    public static class AttributeExtensions
    {
        /// <summary>
        /// Determine if a RPC context is authorized
        /// </summary>
        /// <param name="attributes">Authorization attributes</param>
        /// <param name="context">RPC context to authorize</param>
        /// <param name="throwOnUnauthorized">Throw an exception if unauthorized?</param>
        /// <returns>If authorized</returns>
        /// <exception cref="UnauthorizedAccessException">Not authorized</exception>
        public static async Task<bool> IsAuthorizedAsync(
            this IEnumerable<RpcAuthorizationAttributeBase> attributes,
            RpcContext context,
            bool throwOnUnauthorized = true
            )
        {
            foreach (RpcAuthorizationAttributeBase authZ in attributes)
                if (!await authZ.IsContextAuthorizedAsync(context).DynamicContext())
                {
                    if (RpcContext.UnauthorizedHandler is RpcContext.Unauthorized_Delegate handler)
                        await handler(context, authZ).DynamicContext();
                    if (!throwOnUnauthorized)
                        return false;
                    throw new UnauthorizedAccessException($"{authZ} doesn't authorize the RPC context");
                }
            return true;
        }
    }
}
