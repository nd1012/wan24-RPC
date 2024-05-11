using wan24.Core;
using wan24.RPC.Processing;

namespace wan24_RPC_Tests
{
    [TestClass]
    public class RPC_Tests
    {
        [TestMethod, Timeout(3000)]
        public async Task NotDisposingApi_TestsAsync()
        {
            // Server API shouldn't be disposed when the server RPC processor is being disposed (because it's using the NoRpcDisposeAttribute)
            ServerApi? serverApi = null;
            (RpcProcessor server, RpcProcessor client) = await GetRpcAsync();
            try
            {
                serverApi = server.Options.API.Values.First().Instance as ServerApi;
                Assert.IsNotNull(serverApi);
                Assert.IsNotNull(serverApi.Processor);
                Assert.IsFalse(CancellationToken.Equals(serverApi.ProcessorCancellation, default));
            }
            finally
            {
                await server.DisposeAsync();
                await client.DisposeAsync();
                if (serverApi is not null)
                {
                    try
                    {
                        Assert.IsFalse(serverApi.IsDisposing);
                    }
                    finally
                    {
                        await serverApi.DisposeAsync();
                    }
                    Assert.IsTrue(serverApi.IsDisposed);
                }
            }
        }
        [TestMethod, Timeout(3000)]
        public async Task Echo_TestsAsync()
        {
            // Simple echo method calls
            (RpcProcessor server, RpcProcessor client) = await GetRpcAsync();
            try
            {
                Logging.WriteInfo("Sync echo client -> server");
                string? result = await client.CallValueAsync<string>(nameof(ServerApi), nameof(ServerApi.Echo), parameters: ["test"]);
                Assert.AreEqual("test", result);

                Logging.WriteInfo("Async echo client -> server");
                result = await client.CallValueAsync<string>(nameof(ServerApi), nameof(ServerApi.EchoAsync), parameters: ["test"]);
                Assert.AreEqual("test", result);

                Logging.WriteInfo("Sync echo server -> client");
                result = await server.CallValueAsync<string>(nameof(ClientApi), nameof(ClientApi.Echo), parameters: ["test"]);
                Assert.AreEqual("test", result);

                Logging.WriteInfo("Async echo server -> client");
                result = await server.CallValueAsync<string>(nameof(ClientApi), nameof(ClientApi.EchoAsync), parameters: ["test"]);
                Assert.AreEqual("test", result);
            }
            finally
            {
                await server.Options.API.Values.First().Instance.TryDisposeAsync();
                await server.DisposeAsync();
                await client.DisposeAsync();
            }
        }

        [TestMethod, Timeout(3000)]
        public async Task Event_TestsAsync()
        {
            // Simple remote event
            (RpcProcessor server, RpcProcessor client) = await GetRpcAsync();
            try
            {
                int eventHandlerCall = 0;
                RpcEvent e = client.RegisterEvent("test", (e, m, ct) =>
                {
                    eventHandlerCall++;
                    return Task.CompletedTask;
                });
                await client.CallVoidAsync(nameof(ServerApi), nameof(ServerApi.RaiseRemoteEventAsync));
                Assert.AreEqual(1, e.RaiseCount);
                Assert.AreEqual(1, eventHandlerCall);
            }
            finally
            {
                await server.Options.API.Values.First().Instance.TryDisposeAsync();
                await server.DisposeAsync();
                await client.DisposeAsync();
            }
        }

        public static async Task<(RpcProcessor Server, RpcProcessor Client)> GetRpcAsync()
        {
            RpcProcessor? serverProcessor = null,
                clientProcessor = null;
            BlockingBufferStream serverIO = new(Settings.BufferSize)
                {
                    UseFlush = true
                },
                clientIO = new(Settings.BufferSize)
                {
                    UseFlush = true
                };
            try
            {
                serverProcessor = new(new RpcProcessorOptions(typeof(ServerApi))
                {
                    Logger = Logging.Logger,
                    Stream = new BiDirectionalStream(serverIO, clientIO, leaveOpen: true),
                    FlushStream = true,
                    IncomingMessageQueueCapacity = 20,
                    IncomingMessageQueueThreads = 10,
                    OutgoingMessageQueueCapacity = 10,
                    CallQueueSize = 2,
                    CallThreads = 1,
                    RequestQueueSize = 2,
                    RequestThreads = 1
                })
                {
                    Name = "Server"
                };
                await serverProcessor.StartAsync();
                clientProcessor = new(new RpcProcessorOptions(new ClientApi())
                {
                    Logger = Logging.Logger,
                    Stream = new BiDirectionalStream(clientIO, serverIO),
                    FlushStream = true,
                    IncomingMessageQueueCapacity = 20,
                    IncomingMessageQueueThreads = 10,
                    OutgoingMessageQueueCapacity = 10,
                    CallQueueSize = 2,
                    CallThreads = 1,
                    RequestQueueSize = 2,
                    RequestThreads = 1
                })
                {
                    Name = "Client"
                };
                await clientProcessor.StartAsync();
            }
            catch
            {
                if (serverProcessor is not null)
                {
                    if (serverProcessor.Options.API.Values.FirstOrDefault()?.Instance is ServerApi api)
                        await api.DisposeAsync();
                    await serverProcessor.DisposeAsync();
                }
                if (clientProcessor is not null)
                    await clientProcessor.DisposeAsync();
                await serverIO.DisposeAsync();
                await clientIO.DisposeAsync();
                throw;
            }
            return (serverProcessor, clientProcessor);
        }
    }
}
