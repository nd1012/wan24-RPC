namespace wan24.RPC.Processing.Messages.Serialization
{
    /// <summary>
    /// RPC serializer
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    public sealed record class RpcSerializer()
    {
        /// <summary>
        /// Registered
        /// </summary>
        private static readonly Dictionary<int, RpcSerializer> Registered = new()
            {
                { BinarySerializer.SERIALIZER, new()
                {
                    CanSerialize = BinarySerializer.CanSerialize,
                    ObjectSerializer = BinarySerializer.ObjectSerializerAsync,
                    ObjectListSerializer = BinarySerializer.ObjectListSerializerAsync,
                    ObjectDeserializer = BinarySerializer.ObjectDeserializerAsync,
                    ObjectListDeserializer = BinarySerializer.ObjectListDeserializerAsync
                } },
                { JsonSerializer.SERIALIZER, new()
                {
                    CanSerialize = JsonSerializer.CanSerialize,
                    ObjectSerializer = JsonSerializer.ObjectSerializerAsync,
                    ObjectListSerializer = JsonSerializer.ObjectListSerializerAsync,
                    ObjectDeserializer = JsonSerializer.ObjectDeserializerAsync,
                    ObjectListDeserializer = JsonSerializer.ObjectListDeserializerAsync
                } },
                { MixedSerializer.SERIALIZER, new()
                {
                    CanSerialize = MixedSerializer.CanSerialize,
                    ObjectSerializer = MixedSerializer.ObjectSerializerAsync,
                    ObjectListSerializer = MixedSerializer.ObjectListSerializerAsync,
                    ObjectDeserializer = MixedSerializer.ObjectDeserializerAsync,
                    ObjectListDeserializer = MixedSerializer.ObjectListDeserializerAsync
                } }
            };

        /// <summary>
        /// Constructor
        /// </summary>
        static RpcSerializer() => OnTypeValidation += (e) =>
        {
            if (e.IsValid)
                return;
            //TODO Validate stream and enumerable value
            /*if (typeof(Stream).IsAssignableFrom(e.Expected) && typeof(RpcStreamValue).IsAssignableFrom(e.Serialized))
                e.IsValid = true;
            else if (e.Expected.IsEnumerable(strict: true, asyncOnly: true) && typeof(IRpcEnumerableValue).IsAssignableFrom(e.Serialized))
                e.IsValid = true;*/
        };

        /// <summary>
        /// Opt-in deserializable types?
        /// </summary>
        public static bool DefaultOptIn { get; set; }

        /// <summary>
        /// Opt-in deserializable types?
        /// </summary>
        public bool? OptIn { get; init; }

        /// <summary>
        /// Determine if a type can be serialized
        /// </summary>
        public required CanSerialize_Delegate CanSerialize { get; init; }

        /// <summary>
        /// Object serializer
        /// </summary>
        public required ObjectSerializer_Delegate ObjectSerializer { get; init; }

        /// <summary>
        /// Object list serializer
        /// </summary>
        public required ObjectListSerializer_Delegate ObjectListSerializer { get; init; }

        /// <summary>
        /// Object deserializer
        /// </summary>
        public required ObjectDeserializer_Delegate ObjectDeserializer { get; init; }

        /// <summary>
        /// Object list deserializer
        /// </summary>
        public required ObjectListDeserializer_Delegate ObjectListDeserializer { get; init; }

        /// <summary>
        /// Get if opt-in is required for deserializable types
        /// </summary>
        /// <returns>If opt-in is required</returns>
        public bool GetIsOptIn() => OptIn ?? DefaultOptIn;

        /// <summary>
        /// Delegate for a RPC serializer method to determine if a type can be serialized
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>If the type can be serialized</returns>
        public delegate bool CanSerialize_Delegate(Type? type);

        /// <summary>
        /// Delegate for an object serializer
        /// </summary>
        /// <param name="obj">Object</param>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public delegate Task ObjectSerializer_Delegate(object? obj, Stream stream, CancellationToken cancellationToken);

        /// <summary>
        /// Delegate for an object list serializer
        /// </summary>
        /// <param name="objList">Object list</param>
        /// <param name="stream">Stream</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public delegate Task ObjectListSerializer_Delegate(object?[]? objList, Stream stream, CancellationToken cancellationToken);

        /// <summary>
        /// Delegate for an object deserializer
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="type">Object type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object</returns>
        public delegate Task<object?> ObjectDeserializer_Delegate(Stream stream, Type type, CancellationToken cancellationToken);

        /// <summary>
        /// Delegate for an object list deserializer
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="types">Object types</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Object list</returns>
        public delegate Task<object?[]?> ObjectListDeserializer_Delegate(Stream stream, Type[] types, CancellationToken cancellationToken);

        /// <summary>
        /// Delegate for a <see cref="OnTypeValidation"/> event handler
        /// </summary>
        /// <param name="e">Arguments</param>
        public delegate void TypeValidation_Delegate(TypeValidationEventArgs e);
        /// <summary>
        /// Raise on type validation, if the serialized type doesn't match the expected type
        /// </summary>
        public static event TypeValidation_Delegate? OnTypeValidation;
        /// <summary>
        /// Raise the <see cref="OnTypeValidation"/> event
        /// </summary>
        /// <param name="expected">Expected type</param>
        /// <param name="serialized">Serialized type</param>
        /// <returns></returns>
        public static bool RaiseOnTypeValidation(in Type expected, in Type serialized)
        {
            TypeValidationEventArgs e = new(expected, serialized);
            OnTypeValidation?.Invoke(e);
            return e.IsValid;
        }

        /// <summary>
        /// Register a serializer
        /// </summary>
        /// <param name="id">ID</param>
        /// <param name="serializer">Serializer</param>
        public static void Register(in int id, in RpcSerializer serializer) => Registered[id] = serializer;

        /// <summary>
        /// Get a serializer
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Serializer</returns>
        public static RpcSerializer? Get(in int id) => Registered.TryGetValue(id, out RpcSerializer? res) ? res : null;

        /// <summary>
        /// Remove a serializer
        /// </summary>
        /// <param name="id">ID</param>
        public static void Remove(in int id) => Registered.Remove(id);

        /// <summary>
        /// Arguments for the <see cref="OnTypeValidation"/> event
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="expected">Expected type</param>
        /// <param name="serialized">Serialized type</param>
        public class TypeValidationEventArgs(in Type expected, in Type serialized) : EventArgs()
        {
            /// <summary>
            /// Expected type
            /// </summary>
            public Type Expected { get; } = expected;

            /// <summary>
            /// Serialized type
            /// </summary>
            public Type Serialized { get; } = serialized;

            /// <summary>
            /// If the serialized type is valid
            /// </summary>
            public bool IsValid { get; set; }
        }
    }
}
