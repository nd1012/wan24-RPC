using wan24.RPC.Processing.Messages;

namespace wan24.RPC.Processing.Messages.Streaming
{
    /// <summary>
    /// Remote stream start request message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class StreamStartMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 5;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;
    }
}
