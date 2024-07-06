using System.ComponentModel.DataAnnotations;
using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// Base class for a RPC scope message
    /// </summary>
    public abstract class ScopeMessageBase : RpcMessageBase, IRpcScopeMessage
    {
        /// <inheritdoc/>
        public virtual bool FailOnScopeNotFound => true;

        /// <inheritdoc/>
        public virtual bool WarnOnScopeNotFound => true;

        /// <inheritdoc/>
        [Range(1, long.MaxValue)]
        public required long ScopeId { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(ScopeId, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            ScopeId = await stream.ReadNumberAsync<long>(version, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
