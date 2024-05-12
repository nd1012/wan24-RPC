using wan24.Core;
using wan24.RPC.Processing.Messages.Streaming;

namespace wan24.RPC.Processing.Messages
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
            { EventMessage.TYPE_ID, typeof(EventMessage) },
            { StreamStartMessage.TYPE_ID, typeof(StreamStartMessage) },
            { StreamChunkMessage.TYPE_ID, typeof(StreamChunkMessage) },
            { RemoteStreamCloseMessage.TYPE_ID, typeof(RemoteStreamCloseMessage) },
            { LocalStreamCloseMessage.HL_TYPE_ID, typeof(LocalStreamCloseMessage) },
            { PingMessage.TYPE_ID, typeof(PingMessage) },
            { PongMessage.TYPE_ID, typeof(PongMessage) }
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
