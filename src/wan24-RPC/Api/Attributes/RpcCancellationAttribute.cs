namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// Attribute for a RPC API method cancellation parameter or return value configuration
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter)]
    public class RpcCancellationAttribute() : Attribute()
    {
        /// <summary>
        /// If to dispose the RPC cancellation from the RPC processor
        /// </summary>
        public bool DisposeRpcCancellation { get; set; }
    }
}
