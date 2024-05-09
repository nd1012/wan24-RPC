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
        public static bool CanSerialize(Type? type) => type is not null && typeof(IStreamSerializer).IsAssignableFrom(type);

        /// <summary>
        /// Object serializer
        /// </summary>
        /// <param name="obj">Object</param>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ObjectSerializerAsync(object? obj, Stream stream, CancellationToken cancellationToken)
        {
            IStreamSerializer? serializable = obj as IStreamSerializer;
            if (obj is not null && serializable is null)
                throw new ArgumentException("Not serializable", nameof(obj));
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
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object</returns>
        public static async Task<object?> ObjectDeserializerAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (await stream.ReadStringNullableAsync(cancellationToken: cancellationToken).DynamicContext() is not string typeName)
                return null;
            Type type = TypeHelper.Instance.GetType(typeName, throwOnError: true)!;
            if (type.GetCustomAttributeCached<NoRpcAttribute>() is not null)
                throw new InvalidDataException($"{type} was denied for RPC deserialization");
            if (
                (RpcSerializer.Get(SERIALIZER)?.GetIsOptIn() ?? throw new InvalidProgramException("Failed to get opt-in behavior")) &&
                type.GetCustomAttributeCached<RpcAttribute>() is null &&
                !StreamSerializer.IsTypeAllowed(type)
                )
                throw new InvalidDataException($"Opt-in is required for binary deserializing {type} using RpcAttribute or by registering an allowed type to StreamSerializer");
            return await stream.ReadSerializedObjectAsync(type, cancellationToken: cancellationToken).DynamicContext();
        }

        /// <summary>
        /// Object list deserializer
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object list</returns>
        public static async Task<object?[]?> ObjectListDeserializerAsync(Stream stream, CancellationToken cancellationToken)
        {
            int len = await stream.ReadNumberAsync<int>(cancellationToken: cancellationToken).DynamicContext();
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
