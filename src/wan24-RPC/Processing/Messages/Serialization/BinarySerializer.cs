using wan24.Core;
using wan24.RPC.Api.Attributes;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages.Serialization
{
    /// <summary>
    /// Binary RPC serializer
    /// </summary>
    public static class BinarySerializer
    {
        /// <summary>
        /// Serializer ID
        /// </summary>
        public const int SERIALIZER = 0;

        /// <summary>
        /// Determine if a type can be serialized
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>If the type can be serialized</returns>
        public static bool CanSerialize(Type? type)
            => type is null ||
                (
                    typeof(IStreamSerializer).IsAssignableFrom(type) &&
                    type.GetCustomAttributeCached<NoRpcAttribute>() is null &&
                    (
                        !(RpcSerializer.Get(SERIALIZER)?.GetIsOptIn() ?? throw new InvalidProgramException("Failed to get opt-in behavior")) ||
                        type.GetCustomAttributeCached<RpcAttribute>() is not null
                    )
                );

        /// <summary>
        /// Object serializer
        /// </summary>
        /// <param name="obj">Object</param>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ObjectSerializerAsync(object? obj, Stream stream, CancellationToken cancellationToken)
        {
            if (!CanSerialize(obj?.GetType()))
                throw new ArgumentException("Not serializable", nameof(obj));
            IStreamSerializer? serializable = obj as IStreamSerializer;
            await stream.WriteStringNullableAsync(serializable?.GetType().ToString(), cancellationToken).DynamicContext();
            if (serializable is not null)
                await stream.WriteSerializedAsync(serializable, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Object list serializer
        /// </summary>
        /// <param name="objList">Object list</param>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ObjectListSerializerAsync(object?[]? objList, Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteNumberAsync(objList?.Length ?? 0, cancellationToken).DynamicContext();
            if (objList is null || objList.Length < 1)
                return;
            foreach (object? obj in objList)
                await ObjectSerializerAsync(obj, stream, cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Object deserializer
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="type">Object type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object</returns>
        public static async Task<object?> ObjectDeserializerAsync(Stream stream, Type type, CancellationToken cancellationToken)
        {
            if (await stream.ReadStringNullableAsync(cancellationToken: cancellationToken).DynamicContext() is not string typeName)
                return null;
            Type serializedType = TypeHelper.Instance.GetType(typeName)
                ?? throw new InvalidDataException($"Unknown serialized type {typeName.ToQuotedLiteral()}");
            if (!CanSerialize(serializedType))
                throw new InvalidDataException($"Serialized type {serializedType} is not serializable");
            if (!type.IsAssignableFrom(serializedType) && !RpcSerializer.RaiseOnTypeValidation(type, serializedType))
                throw new InvalidDataException($"Serialized type {serializedType} not valid for expected type {type}");
            return await stream.ReadSerializedObjectAsync(serializedType, cancellationToken: cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Object list deserializer
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="types">Object types</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object list</returns>
        public static async Task<object?[]?> ObjectListDeserializerAsync(Stream stream, Type[] types, CancellationToken cancellationToken)
        {
            int len = await stream.ReadNumberAsync<int>(cancellationToken: cancellationToken).DynamicContext();
            if (len < 1)
                return null;
            if (len > types.Length)
                throw new InvalidDataException($"Invalid object list length {len}");
            object?[] res = new object[len];
            for (int i = 0; i < len; i++)
                res[i] = await ObjectDeserializerAsync(stream, types[i], cancellationToken).DynamicContext();
            return res;
        }
    }
}
