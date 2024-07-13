using wan24.Core;
using wan24.RPC.Processing.Messages.Scopes;

namespace wan24.RPC.Processing.Scopes
{
    /// <summary>
    /// RPC scope types enumeration (use <c>&gt;255</c> as scope type ID for your custom implementations)
    /// </summary>
    public enum RpcScopeTypes : int
    {
        /// <summary>
        /// Simple sope without any specific functionality (supports <see cref="ScopeTriggerMessage"/> and <see cref="RpcScopeEvent"/>)
        /// </summary>
        [DisplayText("Scope")]
        Scope = 0,
        /// <summary>
        /// Stream (chunked remote <see cref="System.IO.Stream"/>; unidirectional)
        /// </summary>
        [DisplayText("Stream")]
        Stream = 1,
        /// <summary>
        /// Enumerable (asynchronous dynamic item list transfer using <see cref="IAsyncEnumerable{T}"/>)
        /// </summary>
        [DisplayText("Enumerable")]
        Enumerable = 2,
        /// <summary>
        /// Cancellation (a remote <see cref="CancellationToken"/>)
        /// </summary>
        [DisplayText("Cancellation")]
        Cancellation = 3,
        /// <summary>
        /// TCP/IP port tunnel (uni- or bidirectional)
        /// </summary>
        [DisplayText("TCP/IP port tunnel")]
        TcpTunnel = 4,
        /// <summary>
        /// UDP port tunnel (uni- or bidirectional)
        /// </summary>
        [DisplayText("UDP port tunnel")]
        UdpTunnel = 5,
#if !RELEASE //TODO Enable as soon as QUIC is out of preview
        /// <summary>
        /// QUIC port tunnel (uni- or bidirectional)
        /// </summary>
        [DisplayText("QUIC port tunnel")]
        QuicTunnel = 6
#endif
    }
}
