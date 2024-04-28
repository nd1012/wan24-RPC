using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC
{
    /// <summary>
    /// Stream extensions
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Read a RPC message
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="serializerVersion">Serializer version</param>
        /// <param name="expectedType">Expected RPC message type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC message</returns>
        public static async ValueTask<RpcMessageBase> ReadRpcMessageAsync(
            this Stream stream, 
            int? serializerVersion = null, 
            Type? expectedType = null, 
            CancellationToken cancellationToken = default
            )
        {
            int typeId = await stream.ReadNumberAsync<int>(cancellationToken: cancellationToken);
            Type type = RpcMessageTypes.Get(typeId) ?? throw new InvalidDataException($"Unknown RPC message type ID #{typeId}");
            if (expectedType is not null && !expectedType.IsAssignableFrom(type))
                throw new InvalidDataException($"Expected {expectedType}, got {type} instead");
            RpcMessageBase res = Activator.CreateInstance(type) as RpcMessageBase ?? throw new InvalidProgramException($"Failed to instance {type}");
            await ((IStreamSerializer)res).DeserializeAsync(stream, serializerVersion ?? StreamSerializer.Version, cancellationToken).DynamicContext();
            return res;
        }

        /// <summary>
        /// Read a RPC message
        /// </summary>
        /// <typeparam name="T">Expected RPC message type</typeparam>
        /// <param name="stream">Stream</param>
        /// <param name="serializerVersion">Serializer version</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC message</returns>
        public static ValueTask<RpcMessageBase> ReadRpcMessageAsync<T>(
            this Stream stream,
            int? serializerVersion = null,
            CancellationToken cancellationToken = default
            )
            where T : RpcMessageBase, new()
            => ReadRpcMessageAsync(stream, serializerVersion, typeof(T), cancellationToken);

        /// <summary>
        /// Write a RPC message
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async ValueTask WriteRpcMessageAsync(this Stream stream, RpcMessageBase message, CancellationToken cancellationToken = default)
        {
            await stream.WriteNumberAsync(message.Type, cancellationToken).DynamicContext();
            await ((IStreamSerializer)message).SerializeAsync(stream, cancellationToken).DynamicContext();
        }
    }
}
