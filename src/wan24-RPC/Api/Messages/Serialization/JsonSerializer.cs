using System.Text;
using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Api.Messages.Serialization
{
    /// <summary>
    /// JSON RPC serializer
    /// </summary>
    public static class JsonSerializer
    {
        /// <summary>
        /// Serializer ID
        /// </summary>
        public const int SERIALIZER = 1;

        /// <summary>
        /// Determine if a type can be serialized
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>If the type can be serialized</returns>
#pragma warning disable IDE0060 // Remove unused parameter
        public static bool CanSerialize(Type? type) => true;
#pragma warning restore IDE0060 // Remove unused parameter

        /// <summary>
        /// Object serializer
        /// </summary>
        /// <param name="obj">Object</param>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ObjectSerializerAsync(object? obj, Stream stream, CancellationToken cancellationToken)
        {
            byte[]? type = obj?.GetType().ToString().GetBytes();
            await stream.WriteAsync(type?.Length ?? 0, cancellationToken).DynamicContext();
            if (type is null) return;
            await stream.WriteAsync(type, cancellationToken).DynamicContext();
            byte[] data = JsonHelper.Encode(obj).GetBytes();
            await stream.WriteAsync(data.Length, cancellationToken).DynamicContext();
            await stream.WriteAsync(data, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Object list serializer
        /// </summary>
        /// <param name="objList">Object list</param>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ObjectListSerializerAsync(object?[]? objList, Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(objList is null || objList.Length < 1 ? 0 : objList.Length, cancellationToken).DynamicContext();
            if (objList is null || objList.Length < 1)
                return;
            foreach (object? obj in objList)
                await ObjectSerializerAsync(obj, stream, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Object deserializer
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object</returns>
        public static async Task<object?> ObjectDeserializerAsync(Stream stream, CancellationToken cancellationToken)
        {
            int typeNameLen = await stream.ReadIntAsync(cancellationToken: cancellationToken).DynamicContext();
            if (typeNameLen < 1)
                return null;
            if (typeNameLen > short.MaxValue)
                throw new InvalidDataException($"Object type name invalid length {typeNameLen} bytes (max. {short.MaxValue})");
            using RentedArrayStructSimple<byte> buffer = new(typeNameLen, clean: false);
            await stream.ReadExactlyAsync(buffer.Memory, cancellationToken).DynamicContext();
            int len = await stream.ReadIntAsync(cancellationToken: cancellationToken).DynamicContext();
            using LimitedLengthStream limited = new(stream, len, leaveOpen: true)
            {
                ThrowOnReadOverflow = true
            };
            Type type = TypeHelper.Instance.GetType(buffer.Span.ToUtf8String(), throwOnError: true)!;
            if (type.GetCustomAttributesCached<NoRpcAttribute>() is not null)
                throw new InvalidDataException($"{type} isn't allowed for deserialization");
            if (
                (RpcSerializer.Get(SERIALIZER)?.GetIsOptIn() ?? throw new InvalidProgramException("Failed to get opt-in behavior")) &&
                type.GetCustomAttributeCached<RpcAttribute>() is null
                )
                throw new InvalidDataException($"Opt-in is required for deserializing {type}");
            return JsonHelper.DecodeObjectAsync(type, limited, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Object list deserializer
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object list</returns>
        public static async Task<object?[]?> ObjectListDeserializerAsync(Stream stream, CancellationToken cancellationToken)
        {
            int len = await stream.ReadIntAsync(cancellationToken: cancellationToken).DynamicContext();
            if (len < 1)
                return null;
            if (len > byte.MaxValue)
                throw new InvalidDataException($"Invalid object list length {len}");
            object?[] res = new object[len];
            for (int i = 0; i < len; i++)
                res[i] = await ObjectDeserializerAsync(stream, cancellationToken).DynamicContext();
            return res;
        }
    }
}
