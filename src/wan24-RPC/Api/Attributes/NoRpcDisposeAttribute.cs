using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Values;

namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for
    /// <list type="bullet">
    /// <item>A disposable RPC API method parameter which shouldn't be disposed after the method returned regular (NOTE: A scope value may still be disposed by its remote 
    /// scope, if it wasn't configured to keep the value undisposed (see <see cref="RpcScopeValue"/>; the scope may require the value to be consumed when the API method 
    /// returns)!)</item>
    /// <item>A disposable RPC API method return value which shouldn't be disposed after the response was sent to the peer (NOTE: A scope value may still be disposed after 
    /// the response was sent, if the scope wasn't configured to keep the value undisposed (use <see cref="RpcScopeParameterBase"/>/
    /// <see cref="DisposableRpcScopeParameterBase"/> for configuration, if required)!)</item>
    /// <item>A disposable RPC API type which shouldn't be disposed when the RPC processor is disposing</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter)]
    public class NoRpcDisposeAttribute() : Attribute(), INoRpcDisposeAttribute
    {
    }
}
