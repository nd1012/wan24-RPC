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

        [TestMethod, Timeout(3000)]
        public async Task Parameter_TestsAsync()
        {
            // Cancellation scope parameter
            (TestRpcProcessor server, TestRpcProcessor client) = await RPC_Tests.GetRpcAsync();
            using ServerSdk serverSdk = new(client);
            try
            {
                Logging.WriteInfo("Call server method");
                using CancellationTokenSource cts = new();
                Task request = serverSdk.CancellationParameterAsync(cts.Token);
                await Task.Delay(200);
                RpcCancellationScope? scope = client.LocalScopes.Values.First() as RpcCancellationScope;
                Assert.IsNotNull(scope);
                Logging.WriteInfo("Wait for cancellation trigger");
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(scope.WasTriggered));
                RpcCancellationRemoteScope? remoteScope = server.PeerScopes.Values.First() as RpcCancellationRemoteScope;
                Assert.IsNotNull(remoteScope);

                Logging.WriteInfo("Cancel");
                await cts.CancelAsync();
                Logging.WriteInfo("Wait cancellation");
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(remoteScope.Token.IsCancellationRequested));
                Logging.WriteInfo("Wait request");
                await request;
                Logging.WriteInfo("Wait local cleanup");
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(scope.IsDisposed));
                Logging.WriteInfo("Wait remote cleanup");
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(remoteScope.IsDisposed));

                Logging.WriteInfo("Check states");
                Assert.AreEqual(0, client.StoredScopeCount);
                Assert.AreEqual(0, server.StoredRemoteScopeCount);
                Assert.AreEqual(0, server.CallCount);
            }
            finally
            {
                await RPC_Tests.DisposeRpcAsync(server, client);
            }
        }

        [TestMethod, Timeout(3000)]
        public async Task Return_TestsAsync()
        {
            // Cancellation scope parameter
            (TestRpcProcessor server, TestRpcProcessor client) = await RPC_Tests.GetRpcAsync();
            using ServerSdk serverSdk = new(client);
            try
            {
                ServerApi? serverApi = server.Options.API.Values.First().Instance as ServerApi;
                Assert.IsNotNull(serverApi);

                Logging.WriteInfo("Retrieve remote cancellation token");
                CancellationToken cancellation = await serverSdk.CancellationReturnAsync();
                Assert.IsNotNull(serverApi.Cancellation);
                Assert.AreNotEqual(default, cancellation);
                Assert.AreNotEqual(serverApi.Cancellation.Token, cancellation);
                Assert.IsFalse(cancellation.IsCancellationRequested);

                Logging.WriteInfo("Cancellation");
                await serverApi.Cancellation.CancelAsync();
                Logging.WriteInfo("Wait canceled");
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(cancellation.IsCancellationRequested));
                Assert.IsFalse(server.IsDisposing);
                Assert.IsFalse(client.IsDisposing);

                Logging.WriteInfo("Check cleanup");
                Assert.AreEqual(0, client.StoredRemoteScopeCount);
                Assert.AreEqual(0, server.StoredScopeCount);
            }
            finally
            {
                await RPC_Tests.DisposeRpcAsync(server, client);
            }
        }

        //TODO Test cancellation return
    }
}
