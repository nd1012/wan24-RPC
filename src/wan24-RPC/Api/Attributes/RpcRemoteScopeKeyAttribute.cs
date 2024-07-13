namespace wan24.RPC.Api.Attributes
{
    /// <summary>
    /// RPC remote scope key attribute tells the call processor to get the RPC API method parameter value from a remote scope which was stored using the specified key 
    /// (the scope won't be disposed)
    /// </summary>
    /// <param name="key">Key</param>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class RpcRemoteScopeKeyAttribute(string key) : Attribute(), INoRpcDisposeAttribute
    {
        /// <summary>
        /// Key
        /// </summary>
        public string Key { get; } = key;

        /// <summary>
        /// Throw an exception when the scope wasn't found?
        /// </summary>
        public bool ThrowOnMissingScope { get; set; }
    }
}
