using wan24.Core;
using wan24.StreamSerializerExtensions;

namespace wan24.RPC.Processing.Messages.Serialization
{
    /// <summary>
    /// Mixed RPC serializer (chooses between binary and JSON serialization, prefers binary)
    /// </summary>
    public static class MixedSerializer
    {
        /// <summary>
        /// Serializer ID
        /// </summary>
        public const int SERIALIZER = 2;

        /// <summary>
        /// Determine if a type can be serialized
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>If the type can be serialized</returns>
        public static bool CanSerialize(Type? type) => JsonSerializer.CanSerialize(type);

        /// <summary>
        /// Object serializer
        /// </summary>
        /// <param name="obj">Object</param>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task ObjectSerializerAsync(object? obj, Stream stream, CancellationToken cancellationToken)
        {
            Type? type = obj?.GetType();
            if (BinarySerializer.CanSerialize(type))
            {
                await stream.WriteNumberAsync(BinarySerializer.SERIALIZER, cancellationToken).DynamicContext();
                await BinarySerializer.ObjectSerializerAsync(obj, stream, cancellationToken).DynamicContext();
            }
            else if(JsonSerializer.CanSerialize(type))
            {
                await stream.WriteNumberAsync(JsonSerializer.SERIALIZER, cancellationToken).DynamicContext();
                await JsonSerializer.ObjectSerializerAsync(obj, stream, cancellationToken).DynamicContext();
            }
            else
            {
                throw new ArgumentException("Not serializable", nameof(obj));
            }
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
            int serializer = await stream.ReadNumberAsync<int>(cancellationToken: cancellationToken).DynamicContext();
            return serializer switch
            {
                BinarySerializer.SERIALIZER => await BinarySerializer.ObjectDeserializerAsync(stream, type, cancellationToken).DynamicContext(),
                JsonSerializer.SERIALIZER => await JsonSerializer.ObjectDeserializerAsync(stream, type, cancellationToken).DynamicContext(),
                _ => throw new InvalidDataException($"Invalid serializer ID {serializer}"),
            };
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
