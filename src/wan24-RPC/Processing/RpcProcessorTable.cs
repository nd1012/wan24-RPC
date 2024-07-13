using System.Collections.Concurrent;
using wan24.Core;

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
        [InstanceTable]
        public static readonly ConcurrentDictionary<string, RpcProcessor> Processors = [];
    }
}
