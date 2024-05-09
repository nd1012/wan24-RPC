using System.Text.Json.Serialization;
using wan24.Core;
using wan24.RPC.Processing.Messages.Serialization;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages
{
    /// <summary>
    /// Base type for a variable serializer RPC message
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public abstract class SerializerRpcMessageBase() : RpcMessageBase()
    {
        /// <summary>
        /// Default serializer ID
        /// </summary>
        public static int DefaultSerializer { get; set; } = MixedSerializer.SERIALIZER;

        /// <summary>
        /// Serializer ID
        /// </summary>
        [JsonIgnore]
        public int Serializer { get; set; } = DefaultSerializer;

        /// <summary>
        /// Serialize an object
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="obj">Object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected Task SerializeObjectAsync(Stream stream, object? obj, CancellationToken cancellationToken)
            => GetSerializer().ObjectSerializer(obj, stream, cancellationToken);

        /// <summary>
        /// Serialize an object list
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="objList">Object list</param>
        /// <param name="cancellationToken">Cancellation token</param>
        protected Task SerializeObjectListAsync(Stream stream, object?[]? objList, CancellationToken cancellationToken)
            => GetSerializer().ObjectListSerializer(objList, stream, cancellationToken);

        /// <summary>
        /// Deserialize an object
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object</returns>
        protected Task<object?> DeserializeObjectAsync(Stream stream, CancellationToken cancellationToken)
            => GetSerializer().ObjectDeserializer(stream, cancellationToken);

        /// <summary>
        /// Deserialize an object list
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object list</returns>
        protected Task<object?[]?> DeserializeObjectListAsync(Stream stream, CancellationToken cancellationToken)
            => GetSerializer().ObjectListDeserializer(stream, cancellationToken);

        /// <summary>
        /// Get the serializer
        /// </summary>
        /// <returns>Serializer</returns>
        protected RpcSerializer GetSerializer()
            => RpcSerializer.Get(Serializer) ?? throw new InvalidDataException($"Invalid serializer ID {Serializer}");

        /// <inheritdoc/>
        protected override async Task SerializeAsync(Stream stream, CancellationToken cancellationToken)
        {
            await base.SerializeAsync(stream, cancellationToken).DynamicContext();
            await stream.WriteNumberAsync(Serializer, cancellationToken).DynamicContext();
        }

        /// <inheritdoc/>
        protected override async Task DeserializeAsync(Stream stream, int version, CancellationToken cancellationToken)
        {
            await base.DeserializeAsync(stream, version, cancellationToken).DynamicContext();
            Serializer = await stream.ReadNumberAsync<int>(version, cancellationToken: cancellationToken).DynamicContext();
        }
    }
}
