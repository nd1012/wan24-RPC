using wan24.Core;
using wan24.RPC.Api.Messages.Interfaces;

namespace wan24.RPC.Api.Messages
{
    /// <summary>
    /// Registered RPC message types
    /// </summary>
    public static class RpcMessages
    {
        /// <summary>
        /// Registered RPC message types
        /// </summary>
        private static readonly Dictionary<int, Type> Registered = new()
        {
            { RequestMessage.TYPE_ID, typeof(RequestMessage) },
            { ResponseMessage.TYPE_ID, typeof(ResponseMessage) },
            { ErrorResponseMessage.TYPE_ID, typeof(ErrorResponseMessage) },
            { CancellationMessage.TYPE_ID, typeof(CancellationMessage) },
            { EventMessage.TYPE_ID, typeof(EventMessage) }
        };

        /// <summary>
        /// Register a RPC message type
        /// </summary>
        /// <typeparam name="T">RPC message type</typeparam>
        /// <param name="id">RPC message type ID</param>
        public static void Register<T>(in int id) where T : IRpcMessage, new()
        {
            if (Registered.TryGetValue(id, out Type? existing))
                $"Overriding already registered RPC message type #{id} ({existing}) with {typeof(T)}".WriteWarning();
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
