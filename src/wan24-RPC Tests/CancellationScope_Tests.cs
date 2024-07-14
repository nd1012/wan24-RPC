using wan24.Core;
using wan24.RPC.Processing.Scopes;

namespace wan24_RPC_Tests
{
    [TestClass]
    public class CancellationScope_Tests
    {
        [TestMethod, Timeout(3000)]
        public async Task General_TestsAsync()
        {
            // Cancellation scope
            (TestRpcProcessor server, TestRpcProcessor client) = await RPC_Tests.GetRpcAsync();
            try
            {
                Logging.WriteInfo("Create cancellation scope");
                RpcCancellationScope scope = new(client, "cancellation");
                Assert.IsNotNull(client.GetScope("cancellation"));
                await scope.RegisterRemoteAsync();

                Logging.WriteInfo("Get remote cancellation scope");
                RpcCancellationRemoteScope? remoteScope = server.GetRemoteScope("cancellation") as RpcCancellationRemoteScope;
                Assert.IsNotNull(remoteScope);
                Logging.WriteInfo("Waiting for remote scope trigger");
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(scope.WasTriggered));

                Logging.WriteInfo("Not canceled yet");
                Assert.IsFalse(scope.Token.IsCancellationRequested);
                Assert.IsFalse(remoteScope.Token.IsCancellationRequested);

                Logging.WriteInfo("Cancel the scope");
                await scope.CancelAsync();
                Logging.WriteInfo("Waiting for remote scope cancellation");
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(remoteScope.Token.IsCancellationRequested));

                Logging.WriteInfo("Check scope discarded");
                Assert.IsTrue(scope.IsDiscarded);
                Assert.IsTrue(remoteScope.IsDiscarded);

                Logging.WriteInfo("Check scope disposing");
                Assert.IsTrue(scope.IsDisposing);
                Assert.IsTrue(remoteScope.IsDisposing);

                Logging.WriteInfo("Check scope storage");
                Assert.IsNull(client.GetScope("cancellation"));
                Assert.IsNull(server.GetRemoteScope("cancellation"));
                Assert.AreEqual(0, client.StoredScopeCount);
                Assert.AreEqual(0, server.StoredRemoteScopeCount);
            }
            finally
            {
                await RPC_Tests.DisposeRpcAsync(server, client);
            }
        }

        //TODO Test cancellation parameter
        //TODO Test cancellation return
    }
}
