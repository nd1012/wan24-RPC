using wan24.Core;

namespace wan24.RPC
{
    /// <summary>
    /// Registered RPC message types
    /// </summary>
    public static class RpcMessageTypes
    {
        /// <summary>
        /// Registered RPC message types
        /// </summary>
        private static readonly Dictionary<int, Type> Registered = [];

        /// <summary>
        /// Constructor
        /// </summary>
        static RpcMessageTypes()
        {
            Registered[RpcErrorResponseMessage.TYPE_ID] = typeof(RpcErrorResponseMessage);
            Registered[RpcCancellationMessage.TYPE_ID] = typeof(RpcCancellationMessage);
            //TODO Register more implemented message types
        }

        /// <summary>
        /// Register a RPC message type
        /// </summary>
        /// <typeparam name="T">RPC message type</typeparam>
        /// <param name="id">RPC message type ID</param>
        public static void Register<T>(in int id) where T : RpcMessageBase, new()
        {
            if (Registered.TryGetValue(id, out Type? existing))
                Logging.WriteWarning($"Overriding already registered RPC message type #{id} ({existing}) with {typeof(T)}");
            Registered[id] = typeof(T);
        }

        /// <summary>
        /// Get a RPC message type
        /// </summary>
        /// <param name="id">RPC message type ID</param>
        /// <returns>RPC message type</returns>
        public static Type? Get(in int id) => Registered.TryGetValue(id, out Type? res) ? res : null;
    }
}
