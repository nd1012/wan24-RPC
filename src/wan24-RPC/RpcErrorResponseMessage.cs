using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC
{
    /// <summary>
    /// RPC error response message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class RpcErrorResponseMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 0;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// Error
        /// </summary>
        public required Exception Error { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteStringAsync(Error.GetType().ToString(), cancellationToken).DynamicContext();
            await stream.WriteStringNullableAsync(Error.Message, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            string typeName = await stream.ReadStringAsync(version, minLen: 1, maxLen: short.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            Type? type = TypeHelper.Instance.GetType(typeName);
            if (type is not null && (!typeof(Exception).IsAssignableFrom(type) || !type.CanConstruct()))
                throw new InvalidDataException($"Invalid exception type {type} in {GetType()}.{nameof(Error)}");
            string? message = await stream.ReadStringNullableAsync(version, minLen: 1, maxLen: short.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            Error = new RpcException(
                message,
                type is null
                    ? null
                    : Activator.CreateInstance(type, message) as Exception ?? throw new InvalidProgramException($"Failed to instance {type}")
                )
            { 
                RemoteExceptionType = typeName 
            };
        }
    }
}
