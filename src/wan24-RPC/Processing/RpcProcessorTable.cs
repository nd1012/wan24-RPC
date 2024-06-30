using System.Collections.Concurrent;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// RPC processor table
    /// </summary>
    public static class RpcProcessorTable
    {
        /// <summary>
        /// RPC processors (key is the GUID)
        /// </summary>
        public static readonly ConcurrentDictionary<string, RpcProcessor> Processors = [];
    }
}
