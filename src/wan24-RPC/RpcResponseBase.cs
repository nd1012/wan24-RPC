namespace wan24.RPC
{
    /// <summary>
    /// Base type for a RPC response message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public abstract class RpcResponseBase() : RpcMessageBase()
    {
        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// Return value
        /// </summary>
        public object? ReturnValue { get; set; }
    }
}
