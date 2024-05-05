using wan24.Core;
using wan24.RPC.Api.Messages.Interfaces;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages.Serialization.Extensions
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
        /// <param name="strictType">Expect strictly the given RPC message type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC message</returns>
        public static async ValueTask<IRpcMessage> ReadRpcMessageAsync(
            this Stream stream,
            int? serializerVersion = null,
            Type? expectedType = null,
            bool strictType = false,
            CancellationToken cancellationToken = default
            )
        {
            int typeId = await stream.ReadNumberAsync<int>(cancellationToken: cancellationToken);
            Type type = RpcMessages.Get(typeId) ?? throw new InvalidDataException($"Unknown RPC message type ID #{typeId}");
            if (expectedType is not null && ((strictType && expectedType != type) || (!strictType && !expectedType.IsAssignableFrom(type))))
                throw new InvalidDataException($"Expected {expectedType}, got {type} instead");
            IRpcMessage res = Activator.CreateInstance(type) as IRpcMessage ?? throw new InvalidProgramException($"Failed to instance {type}");
            await res.DeserializeAsync(stream, serializerVersion ?? StreamSerializer.Version, cancellationToken).DynamicContext();
            return res;
        }

        /// <summary>
        /// Read a RPC message
        /// </summary>
        /// <typeparam name="T">Expected RPC message type</typeparam>
        /// <param name="stream">Stream</param>
        /// <param name="serializerVersion">Serializer version</param>
        /// <param name="strictType">Expect strictly the given RPC message type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>RPC message</returns>
        public static async ValueTask<T> ReadRpcMessageAsync<T>(
            this Stream stream,
            int? serializerVersion = null,
            bool strictType = false,
            CancellationToken cancellationToken = default
            )
            where T : IRpcMessage, new()
        {
            int typeId = await stream.ReadNumberAsync<int>(cancellationToken: cancellationToken);
            Type type = RpcMessages.Get(typeId) ?? throw new InvalidDataException($"Unknown RPC message type ID #{typeId}");
            if ((strictType && typeof(T) != type) || (!strictType && !typeof(T).IsAssignableFrom(type)))
                throw new InvalidDataException($"Expected {typeof(T)}, got {type} instead");
            T res = new();
            await res.DeserializeAsync(stream, serializerVersion ?? StreamSerializer.Version, cancellationToken).DynamicContext();
            return res;
        }

        /// <summary>
        /// Write a RPC message
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="message">Message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async ValueTask WriteRpcMessageAsync(this Stream stream, IRpcMessage message, CancellationToken cancellationToken = default)
        {
            await stream.WriteNumberAsync(message.Type, cancellationToken).DynamicContext();
            await message.SerializeAsync(stream, cancellationToken).DynamicContext();
        }
    }
}
