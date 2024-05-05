﻿namespace wan24.RPC.Api.Messages.Streaming
{
    /// <summary>
    /// Signal local RPC stream close to the remote
    /// </summary>
    public class LocalStreamCloseMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 8;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;
    }
}
