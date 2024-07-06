using wan24.Core;
using wan24.RPC.Processing.Values;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages.Scopes
{
    /// <summary>
    /// RPC scope registration message for the consumer
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class ScopeRegistrationMessage() : RpcMessageBase(), IRpcRequest
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 15;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// RPC scope value
        /// </summary>
        public required RpcScopeValue Value { get; set; }

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteStringAsync(Value.GetType().ToString(), cancellationToken).DynamicContext();
            await stream.WriteSerializedAsync(Value, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            string valueTypeName = await stream.ReadStringAsync(version, minLen: 1, maxLen: byte.MaxValue, cancellationToken: cancellationToken).DynamicContext();
            Type valueType = TypeHelper.Instance.GetType(valueTypeName)
                ?? throw new InvalidDataException($"Failed to instance RPC scope value type {valueTypeName.ToQuotedLiteral()}");
            if (!valueType.CanConstruct() || !typeof(RpcScopeValue).IsAssignableFrom(valueType))
                throw new TypeInitializationException(valueType.FullName, new InvalidCastException($"{valueType} is incompatible to {typeof(RpcScopeValue)}"));
            Value = (RpcScopeValue)await stream.ReadSerializedObjectAsync(valueType, version, cancellationToken).DynamicContext();
        }
    }
}
