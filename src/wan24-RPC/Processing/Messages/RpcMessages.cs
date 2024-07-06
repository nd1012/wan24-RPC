using wan24.Core;
using wan24.RPC.Processing.Messages.Scopes;

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
            { CancelMessage.TYPE_ID, typeof(CancelMessage) },
            { EventMessage.TYPE_ID, typeof(EventMessage) },
            { PingMessage.TYPE_ID, typeof(PingMessage) },
            { PongMessage.TYPE_ID, typeof(PongMessage) },
            { ScopeDiscardedMessage.TYPE_ID, typeof(ScopeDiscardedMessage) },
            { RemoteScopeDiscardedMessage.TYPE_ID, typeof(RemoteScopeDiscardedMessage) },
            { ScopeTriggerMessage.TYPE_ID, typeof(ScopeTriggerMessage) },
            { RemoteScopeTriggerMessage.TYPE_ID, typeof(RemoteScopeTriggerMessage) },
            { ScopeErrorMessage.TYPE_ID, typeof(ScopeErrorMessage) },
            { RemoteScopeErrorMessage.TYPE_ID, typeof(RemoteScopeErrorMessage) },
            { ScopeEventMessage.TYPE_ID, typeof(ScopeEventMessage) },
            { RemoteScopeEventMessage.TYPE_ID, typeof(RemoteScopeEventMessage) },
            { ScopeRegistrationMessage.TYPE_ID, typeof(ScopeRegistrationMessage) },
            { CloseMessage.TYPE_ID, typeof(CloseMessage) }
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
