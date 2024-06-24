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
            (TestRpcProcessor server, TestRpcProcessor client) = await GetRpcAsync();
            try
            {
                serverApi = server.Options.API.Values.First().Instance as ServerApi;
                Assert.IsNotNull(serverApi);
                Assert.IsNotNull(serverApi.Processor);
                await Task.Delay(200);// Wait for the RPC processor worker to start working
                Assert.IsFalse(serverApi.ProcessorCancellation.IsEqualTo(default));
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
            // Simple client/server echo method calls
            (TestRpcProcessor server, TestRpcProcessor client) = await GetRpcAsync();
            ServerSdk serverSdk = new(client);
            ClientSdk clientSdk = new(server);
            try
            {

                Logging.WriteInfo("Sync echo client -> server");
                string? result = await serverSdk.EchoSyncAsync("test");
                Assert.AreEqual("test", result);

                Logging.WriteInfo("Async echo client -> server");
                result = await serverSdk.EchoAsync("test");
                Assert.AreEqual("test", result);

                Logging.WriteInfo("Sync echo server -> client");
                result = await clientSdk.EchoSyncAsync("test");
                Assert.AreEqual("test", result);

                Logging.WriteInfo("Async echo server -> client");
                result = await clientSdk.EchoAsync("test");
                Assert.AreEqual("test", result);
            }
            finally
            {
                await serverSdk.DisposeAsync();
                await clientSdk.DisposeAsync();
                await DisposeRpcAsync(server, client);
            }
        }

        [TestMethod, Timeout(3000)]
        public async Task Event_TestsAsync()
        {
            // Simple remote event
            (TestRpcProcessor server, TestRpcProcessor client) = await GetRpcAsync();
            ServerSdk serverSdk = new(client);
            try
            {
                int eventHandlerCall = 0,
                    eventRaised = 0;
                Logging.WriteInfo("Client event registration");
                RpcEvent e = client.RegisterEvent("test", (e, m, ct) =>
                {
                    Logging.WriteInfo("Server executed registered event handler");
                    eventHandlerCall++;
                    return Task.CompletedTask;
                });
                client.OnRemoteEvent += (s, e) =>
                {
                    Logging.WriteInfo("Server raised event");
                    eventRaised++;
                };
                Logging.WriteInfo("Client raises event");
                await serverSdk.RaiseRemoteEventAsync();
                Assert.AreEqual(1, e.RaiseCount);
                Assert.AreEqual(1, eventHandlerCall);
                Assert.AreEqual(1, eventRaised);
            }
            finally
            {
                await serverSdk.DisposeAsync();
                await DisposeRpcAsync(server, client);
            }
        }

        [TestMethod, Timeout(3000)]
        public async Task PingPong_TestsAsync()
        {
            // Ping/pong
            (TestRpcProcessor server, TestRpcProcessor client) = await GetRpcAsync();
            try
            {
                // Both processors watch the heartbeat bi-directional (uni-directional would be good already, actually)
                Logging.WriteInfo("Client/server state checks");
                Assert.IsNotNull(server.ServerHeartBeat);
                Assert.IsNotNull(server.ClientHeartBeat);
                Assert.IsNotNull(client.ServerHeartBeat);
                Assert.IsNotNull(client.ClientHeartBeat);
                Assert.AreEqual(TimeSpan.Zero, client.MessageLoopDuration);
                Assert.AreEqual(TimeSpan.Zero, server.MessageLoopDuration);

                // Manual ping/pong sequence
                Logging.WriteInfo("Ping client -> server");
                DateTime time = DateTime.Now;
                await client.PingAsync(TimeSpan.FromSeconds(1));
                Assert.IsTrue(time < client.LastMessageSent);
                Assert.IsTrue(time < server.LastMessageReceived);
                Assert.IsTrue(client.MessageLoopDuration > TimeSpan.Zero);
                Assert.AreEqual(TimeSpan.Zero, server.MessageLoopDuration);

                // Automatic heartbeat messages
                Logging.WriteInfo("Heartbeat");
                time = DateTime.Now;
                await Task.Delay(1000);// Wait for a ping/pong heartbeat sequence
                Assert.IsTrue(time < client.LastMessageSent);
                Assert.IsTrue(time < client.LastMessageReceived);
                Assert.IsTrue(time < server.LastMessageSent);
                Assert.IsTrue(time < server.LastMessageReceived);
                Assert.IsTrue(server.MessageLoopDuration > TimeSpan.Zero);
            }
            finally
            {
                await DisposeRpcAsync(server, client);
            }
        }

        public static async Task<(TestRpcProcessor Server, TestRpcProcessor Client)> GetRpcAsync()
        {
            TestRpcProcessor? serverProcessor = null,
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
                    IncomingMessageQueue = new()
                    {
                        Capacity = 20,
                        Threads = 10
                    },
                    OutgoingMessageQueueCapacity = 10,
                    CallQueue = new()
                    {
                        Capacity = 2,
                        Threads = 1
                    },
                    RequestQueue = new()
                    {
                        Capacity = 2,
                        Threads = 1
                    },
                    KeepAlive = new()
                    {
                        Timeout = TimeSpan.FromMilliseconds(500),
                        PeerTimeout  = TimeSpan.FromMilliseconds(700)
                    }
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
                    IncomingMessageQueue = new()
                    {
                        Capacity = 20,
                        Threads = 10
                    },
                    OutgoingMessageQueueCapacity = 10,
                    CallQueue = new()
                    {
                        Capacity = 2,
                        Threads = 1
                    },
                    RequestQueue = new()
                    {
                        Capacity = 2,
                        Threads = 1
                    },
                    KeepAlive = new()
                    {
                        Timeout = TimeSpan.FromMilliseconds(500),
                        PeerTimeout = TimeSpan.FromMilliseconds(700)
                    }
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

        public static async Task DisposeRpcAsync(TestRpcProcessor server, TestRpcProcessor client)
        {
            await server.Options.API.Values.First().Instance.TryDisposeAsync();
            await server.DisposeAsync();
            await client.DisposeAsync();
        }
    }
}
