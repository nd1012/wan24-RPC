namespace wan24.RPC
{
    /// <summary>
    /// Attribute for a RPC API method return value or parameter which shouldn't be communicated enumerated
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public class NoRpcEnumerableAttribute() : Attribute()
    {
    }
}
