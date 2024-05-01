namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for a RPC type which is allowed to be deserialized (opt-in)
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class RpcAttribute() : Attribute()
    {
    }
}
