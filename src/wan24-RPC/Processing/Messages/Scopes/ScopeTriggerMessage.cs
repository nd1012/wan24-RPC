﻿using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// Trigger a RPC scope at the consumer
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class ScopeTriggerMessage() : ScopeMessageBase(), IRpcRemoteScopeMessage
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 9;

        /// <summary>
        /// Max. <see cref="Data"/> string length
        /// </summary>
        public static int MaxDataLen { get; set; } = ushort.MaxValue;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => false;

        /// <summary>
        /// Data
        /// </summary>
        [RuntimeCountLimit("wan24.RPC.Processing.Messages.Scopes.ScopeTriggerMessage.MaxDataLen")]
        public string? Data { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteStringNullableAsync(Data, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Data = await stream.ReadStringNullableAsync(version, minLen: 0, maxLen: MaxDataLen, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
