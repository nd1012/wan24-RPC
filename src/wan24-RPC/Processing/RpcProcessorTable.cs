using System.Collections.Concurrent;

namespace wan24.RPC.Processing
{
    /// <summary>
    /// RPC processor table
    /// </summary>
    public static class RpcProcessorTable
    {
        /// <summary>
        /// RPC processors (key is the object hash code)
        /// </summary>
        public static readonly ConcurrentDictionary<int, RpcProcessor> Processors = [];//TODO Use GUID as key
    }
}
