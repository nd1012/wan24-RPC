using wan24.Core;

namespace wan24.RPC.Api.Messages
{
    /// <summary>
    /// RPC response message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class ResponseMessage() : SerializerRpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 1;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// Return value
        /// </summary>
        public object? ReturnValue { get; set; }

        /// <summary>
        /// Dispose the return value
        /// </summary>
        public virtual async Task DisposeReturnValueAsync()
        {
            if (ReturnValue is null)
                return;
            await ReturnValue.TryDisposeAsync().DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await SerializeObjectAsync(stream, ReturnValue, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            ReturnValue = await DeserializeObjectAsync(stream, cancellationToken).DynamicContext();
        }
    }
}
