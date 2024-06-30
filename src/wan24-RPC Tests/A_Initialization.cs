using Microsoft.Extensions.Logging;
using System.Reflection;
using wan24.Core;
using wan24.ObjectValidation;
using wan24.RPC.Processing;

namespace wan24_RPC_Tests
{
    [TestClass]
    public class A_Initialization
    {
        public static ILoggerFactory LoggerFactory { get; private set; } = null!;

        [AssemblyInitialize]
        public static void Init(TestContext tc)
        {
            TypeHelper.Instance.ScanAssemblies(typeof(A_Initialization).Assembly);
            //DisposableBase.CreateStackInfo = true;
            //DisposableRecordBase.CreateStackInfo = true;
            Bootstrap.Async().GetAwaiter().GetResult();
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole());
            Logging.Logger = FileLogger.CreateAsync(
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "test.log"),
                LogLevel.Trace,
                LoggerFactory.CreateLogger("Tests")
                )
                .GetAwaiter()
                .GetResult();
            Settings.LogLevel = LogLevel.Trace;
            ValidateObject.Logger = (message) => Logging.WriteDebug(message);
            TypeHelper.Instance.ScanAssemblies(typeof(RpcProcessor).Assembly);
            Logging.WriteInfo("wan24-RPC Tests initialized");
        }
    }
}
