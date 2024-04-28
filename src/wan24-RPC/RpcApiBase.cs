namespace wan24.RPC
{
    /// <summary>
    /// Base type for a RPC API
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public abstract class RpcApiBase()
    {
        /// <inheritdoc/>
        [NoRpc]
        public override bool Equals(object? obj) => base.Equals(obj);

        /// <inheritdoc/>
        [NoRpc]
        public override int GetHashCode() => base.GetHashCode();

        /// <inheritdoc/>
        [NoRpc]
        public override string? ToString() => base.ToString();
    }
}
