using wan24.Core;
using wan24.RPC.Processing.Options;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages
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

        /// <summary>
        /// Serialized return value
        /// </summary>
        protected byte[]? _ReturnValue = null;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// Return value
        /// </summary>
        public object? ReturnValue { get; set; }

        /// <summary>
        /// Deserialize the return value
        /// </summary>
        /// <param name="type">Expected return value type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public virtual async Task DeserializeReturnValueAsync(Type type, CancellationToken cancellationToken = default)
        {
            if (_ReturnValue is null)
                throw new InvalidOperationException();
            using MemoryStream ms = new(_ReturnValue);
            ReturnValue = await DeserializeObjectAsync(ms, type, cancellationToken).DynamicContext();
            _ReturnValue = null;
        }

        /// <summary>
        /// Dispose the return value
        /// </summary>
        public virtual async Task DisposeReturnValueAsync()
        {
            if (ReturnValue is not null)
                await ReturnValue.TryDisposeAsync().DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            using MemoryPoolStream ms = new();
            await SerializeObjectAsync(ms, ReturnValue, cancellationToken).DynamicContext();
            ms.Position = 0;
            await stream.WriteNumberAsync(ms.Length, cancellationToken).DynamicContext();
            await ms.CopyToAsync(stream, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            ushort len = await stream.ReadNumberAsync<ushort>(version, cancellationToken: cancellationToken).DynamicContext();
            if(len<1||len> StreamScopeOptions.MaxStreamContentLength)
                throw new InvalidDataException($"Invalid serialized return value length {len}");
            _ReturnValue = new byte[len];
            await stream.ReadExactlyAsync(_ReturnValue, cancellationToken).DynamicContext();
        }
    }
}
