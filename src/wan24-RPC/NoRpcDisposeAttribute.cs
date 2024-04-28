namespace wan24.RPC
{
    /// <summary>
    /// Attribute for a RPC API which shouldn't be disposed after use, or a RPC API method which returns a value which shouldn't be disposed after sending the response
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class NoRpcDisposeAttribute() : Attribute()
    {
    }
}
