using System.Text.Json.Serialization;
using wan24.Core;
using wan24.RPC.Processing.Exceptions;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// RPC error response message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public class ErrorResponseMessage() : RpcMessageBase()
    {
        /// <summary>
        /// RPC message type ID
        /// </summary>
        public const int TYPE_ID = 2;

        /// <summary>
        /// JSON error
        /// </summary>
        protected JsonError? _JError = null;

        /// <inheritdoc/>
        public override int Type => TYPE_ID;

        /// <inheritdoc/>
        public sealed override bool RequireId => true;

        /// <summary>
        /// Error
        /// </summary>
        [JsonIgnore]
        public required Exception Error { get; set; }

        /// <summary>
        /// JSON error
        /// </summary>
        public JsonError JError
        {
            get => _JError ??= new(Error);
            set
            {
                if (_JError is not null)
                    throw new InvalidOperationException();
                _JError = value;
                Type? type = TypeHelper.Instance.GetType(value.Type);
                if (type is not null && (!typeof(Exception).IsAssignableFrom(type) || !type.CanConstruct()))
                    throw new InvalidDataException($"Invalid exception type {type} in {GetType()}.{nameof(JError)}");
                Error = new RpcRemoteException(
                    value.Message,
                    type is null
                        ? null
                        : Activator.CreateInstance(type, value.Message) as Exception ?? throw new InvalidProgramException($"Failed to instance {type}")
                    )
                {
                    RemoteExceptionType = value.Type
                };
            }
        }

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
            Error = new RpcRemoteException(
                message,
                type is null
                    ? null
                    : Activator.CreateInstance(type, message) as Exception ?? throw new InvalidProgramException($"Failed to instance {type}")
                )
            {
                RemoteExceptionType = typeName
            };
        }

        /// <summary>
        /// JSON error object
        /// </summary>
        /// <param name="ex">Exception</param>
        public class JsonError(Exception ex)
        {
            /// <summary>
            /// Exception type
            /// </summary>
            public string Type { get; } = ex.GetType().ToString();

            /// <summary>
            /// Message
            /// </summary>
            public string? Message { get; } = ex.Message;
        }
    }
}
