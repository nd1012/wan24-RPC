using wan24.Compression;
using wan24.Core;
using wan24.RPC.Processing;

[assembly: Bootstrapper(typeof(wan24.RPC.Bootstrapper), nameof(wan24.RPC.Bootstrapper.Boot))]

namespace wan24.RPC
{
    /// <summary>
    /// Bootstrapper
    /// </summary>
    public static class Bootstrapper
    {
        /// <summary>
        /// Boot
        /// </summary>
        public static void Boot()
        {
            InstanceTables.Registered[typeof(RpcProcessor)] = typeof(RpcProcessorTable);
        }
    }
}
