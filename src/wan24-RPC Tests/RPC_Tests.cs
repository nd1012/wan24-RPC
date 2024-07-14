using wan24.Core;
using wan24.RPC.Processing;
using wan24.RPC.Processing.Exceptions;
using wan24.RPC.Processing.Parameters;
using wan24.RPC.Processing.Scopes;

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
                await Task.Delay(200);// Wait for the RPC processor worker to start the message loop
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
        public async Task CloseMessage_TestsAsync()
        {
            // Peer disconnects, if a close message was received
            (TestRpcProcessor server, TestRpcProcessor client) = await GetRpcAsync();
            try
            {
                await client.CloseAsync();
                Assert.IsTrue(client.IsDisposed);// Client is disposed already!
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(server.IsDisposing));// Server is disposing asynchronous
            }
            finally
            {
                await server.DisposeAsync();
                await client.DisposeAsync();
            }
        }

        [TestMethod, Timeout(3000)]
        public async Task Echo_TestsAsync()
        {
            // Simple client/server echo method calls
            (TestRpcProcessor server, TestRpcProcessor client) = await GetRpcAsync();
            ClientSdk clientSdk = new(server);
            ServerSdk serverSdk = new(client);
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
                await clientSdk.DisposeAsync();
                await serverSdk.DisposeAsync();
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
                    Logging.WriteInfo("\tServer executed registered event handler");
                    eventHandlerCall++;
                    return Task.CompletedTask;
                });
                client.OnRemoteEvent += (s, e) =>
                {
                    Logging.WriteInfo("\tServer raised event");
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
        public async Task Unauthorized_TestsAsync()
        {
            // Fail calling a remote API method (unauthorized)
            (TestRpcProcessor server, TestRpcProcessor client) = await GetRpcAsync();
            ServerSdk serverSdk = new(client);
            try
            {
                using CancellationTokenSource cts = new();
                Logging.WriteInfo("Calling unauthorized server method");
                Task task = serverSdk.NotAuthorizedAsync(cts.Token);
                Logging.WriteInfo("Waiting server disposing");
                await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(server.IsDisposing));

                Logging.WriteInfo("Client cancellation");
                await cts.CancelAsync();
                Logging.WriteInfo("Disposing client");
                await client.DisposeAsync();
                Logging.WriteInfo("Assuming task cancelled exception for the request task");
                await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () => await task);
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

                // Manual ping/pong sequence
                Logging.WriteInfo("Ping client -> server");
                DateTime time = DateTime.Now;
                await client.PingAsync(TimeSpan.FromSeconds(1));
                Assert.IsTrue(time < client.LastMessageSent);
                Assert.IsTrue(time < server.LastMessageReceived);
                Assert.IsTrue(client.MessageLoopDuration > TimeSpan.Zero);

                // Automatic heartbeat messages
                Logging.WriteInfo("Heartbeat");
                time = DateTime.Now;
                await Task.Delay(1000);// Wait for a ping/pong heartbeat sequence
                Assert.IsTrue(time < client.LastMessageSent);
                Assert.IsTrue(time < client.LastMessageReceived);
                Assert.IsTrue(time < server.LastMessageSent);
                Assert.IsTrue(time < server.LastMessageReceived);
            }
            finally
            {
                await DisposeRpcAsync(server, client);
            }
        }

        [TestMethod, Timeout(10000)]
        public async Task Scope_TestsAsync()
        {
            // Simple scope functionality
            (TestRpcProcessor server, TestRpcProcessor client) = await GetRpcAsync();
            ServerApi? serverApi = null;
            ServerSdk serverSdk = new(client);
            try
            {
                serverApi = server.Options.API.Values.First().Instance as ServerApi;
                Assert.IsNotNull(serverApi);

                // Scope factory
                {
                    Logging.WriteInfo("Scope factory");
                    RpcScopes.ScopeFactory_Delegate? factory = RpcScopes.GetLocalScopeFactory(RpcScope.TYPE);
                    Assert.IsNotNull(factory);
                    RpcScope scope = (RpcScope)await factory.Invoke(
                        client,
                        new RpcScopeParameter()
                        {
                            Key = "test"
                        },
                        CancellationToken.None
                        );
                    await using (scope)
                    {
                        scope.InformConsumerWhenDisposing = false;
                        Assert.AreEqual(1, scope.Id);
                        Assert.AreEqual(1, client.CurrentScopeId);
                        Assert.AreEqual("test", scope.Key);
                        Assert.IsNotNull(client.GetScope("test"));
                        Assert.IsTrue(client.LocalScopes.ContainsKey(scope.Id));
                    }
                }
                Assert.IsNull(client.GetScope("test"));
                Assert.IsFalse(client.LocalScopes.ContainsKey(1));

                // Manual scope creation
                {
                    Logging.WriteInfo("Manual scope creation");
                    RpcScope scope = new(client, "test")
                    {
                        IsStored = true,
                        InformConsumerWhenDisposing = false
                    };
                    await using (scope)
                    {
                        Assert.AreEqual(2, scope.Id);
                        Assert.AreEqual(2, client.CurrentScopeId);
                        Assert.AreEqual("test", scope.Key);
                        Assert.IsNotNull(client.GetScope("test"));
                        Assert.IsTrue(client.LocalScopes.ContainsKey(scope.Id));
                    }
                }
                Assert.IsNull(client.GetScope("test"));
                Assert.IsFalse(client.LocalScopes.ContainsKey(2));

                // Remote scope registration
                {
                    Logging.WriteInfo("Remote scope registration");
                    RpcScope scope = new(client, "test")
                    {
                        ScopeParameter = new RpcScopeParameter()
                        {
                            Key = "test"
                        }
                    };
                    await using (scope)
                    {
                        // After registration the scope must be stored local and at the peer
                        Logging.WriteInfo("Register scope");
                        Assert.AreEqual(3, scope.Id);
                        Assert.AreEqual(3, client.CurrentScopeId);
                        Assert.AreEqual("test", scope.Key);
                        await scope.RegisterRemoteAsync();
                        Assert.IsNotNull(client.GetScope("test"));
                        Assert.IsTrue(client.LocalScopes.ContainsKey(scope.Id));
                        RpcProcessor.RpcRemoteScopeBase? remoteScope = server.GetRemoteScope("test");
                        Assert.IsNotNull(remoteScope);
                        Assert.IsTrue(server.PeerScopes.ContainsKey(scope.Id));

                        // After the peer disposed the remote scope, the local scope must be disposed, too
                        Logging.WriteInfo("Dispose remote scope");
                        await remoteScope.DisposeAsync();
                        Assert.IsNull(server.GetRemoteScope("test"));
                        Assert.IsFalse(server.PeerScopes.ContainsKey(scope.Id));
                        Logging.WriteInfo("Waiting scope disposed");
                        await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(20), (ct) => Task.FromResult(scope.IsDisposed));
                    }
                }

                // Remote scope discarded when the scope is being disposed
                {
                    Logging.WriteInfo("Dispose local scope");
                    RpcScope scope = new(client, "test")
                    {
                        ScopeParameter = new RpcScopeParameter()
                        {
                            Key = "test"
                        }
                    };
                    await using (scope)
                    {
                        await scope.RegisterRemoteAsync();
                        Assert.IsTrue(server.PeerScopes.ContainsKey(scope.Id));
                        await scope.DisposeAsync().DynamicContext();
                        Logging.WriteInfo("Waiting remote scope removed");
                        await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(!server.PeerScopes.ContainsKey(scope.Id)));
                    }
                }

                // Trigger
                {
                    Logging.WriteInfo("Trigger");
                    RpcScope scope = new(client, "test")
                    {
                        ScopeParameter = new RpcScopeParameter()
                        {
                            Key = "test"
                        }
                    };
                    await using (scope)
                    {
                        await scope.RegisterRemoteAsync();
                        RpcRemoteScope remoteScope = (RpcRemoteScope)(server.GetRemoteScope("test") ?? throw new InvalidProgramException());
                        int localTrigger = 0,
                            remoteTrigger = 0;
                        scope.OnTrigger += (s, e) => localTrigger++;
                        remoteScope.OnTrigger += (s, e) => remoteTrigger++;

                        // Trigger at the peer
                        Logging.WriteInfo("Trigger peer");
                        await scope.SendVoidTriggerAsync();
                        Logging.WriteInfo("Waiting trigger");
                        await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(remoteTrigger != 0));
                        Assert.AreEqual(0, localTrigger);
                        Assert.AreEqual(1, remoteTrigger);

                        // Trigger from the remote
                        Logging.WriteInfo("Remote trigger");
                        await remoteScope.SendVoidTriggerAsync();
                        Logging.WriteInfo("Waiting trigger");
                        await wan24.Core.Timeout.WaitConditionAsync(TimeSpan.FromMilliseconds(50), (ct) => Task.FromResult(localTrigger != 0));
                        Assert.AreEqual(1, localTrigger);
                        Assert.AreEqual(1, remoteTrigger);

                        // Trigger at the peer (waiting)
                        Logging.WriteInfo("Trigger peer (waiting)");
                        await scope.SendVoidTriggerAsync(wait: true);
                        Assert.AreEqual(1, localTrigger);
                        Assert.AreEqual(2, remoteTrigger);

                        // Trigger from the remote (waiting)
                        Logging.WriteInfo("Remote trigger (waiting)");
                        await remoteScope.SendVoidTriggerAsync(wait: true);
                        Assert.AreEqual(2, localTrigger);
                        Assert.AreEqual(2, remoteTrigger);
                    }
                }

                // Events
                {
                    Logging.WriteInfo("Events");
                    RpcScope scope = new(client, "test")
                    {
                        ScopeParameter = new RpcScopeParameter()
                        {
                            Key = "test"
                        }
                    };
                    await using (scope)
                    {
                        await scope.RegisterRemoteAsync();
                        RpcRemoteScope remoteScope = (RpcRemoteScope)(server.GetRemoteScope("test") ?? throw new InvalidProgramException());
                        int localEvent = 0,
                            remoteEvent = 0;
                        scope.RegisterEvent("local", (e,m,ct) =>
                        {
                            Logging.WriteInfo("\tLocal event raised");
                            localEvent++;
                            return Task.CompletedTask;
                        });
                        remoteScope.RegisterEvent("remote", (e, m, ct) =>
                        {
                            Logging.WriteInfo("\tRemote event raised");
                            remoteEvent++;
                            return Task.CompletedTask;
                        });

                        // Event from the peer
                        Logging.WriteInfo("Peer raises event");
                        await remoteScope.RaiseEventAsync("local", wait: true);
                        Assert.AreEqual(1, localEvent);
                        Assert.AreEqual(0, remoteEvent);

                        // Event at the peer
                        Logging.WriteInfo("Client raises event");
                        await scope.RaiseEventAsync("remote", wait: true);
                        Assert.AreEqual(1, localEvent);
                        Assert.AreEqual(1, remoteEvent);
                    }
                }

                // Parameter and return value without disposing options
                Logging.WriteInfo("Parameter and return value without disposing options");
                using (TestDisposable clientObj = new()
                {
                    Name = "Client"
                })
                {
                    using TestDisposable serverObj = await serverSdk.ScopesAsync(clientObj);
                    serverObj.Name ??= "Remote server";
                    Assert.IsNull(client.GetScopeOf(clientObj));// Local scope wasn't stored and disposed when returning the value
                    Assert.IsNull(client.GetRemoteScopeOf(serverObj));// Remote scope wasn't stored and disposed when returning the value
                    await Task.Delay(200);// Wait for server call cleanup
                    Assert.IsNotNull(serverApi.ClientObj);
                    Assert.IsNotNull(serverApi.ServerObj);
                    Assert.AreNotEqual(serverApi.ClientObj, clientObj);
                    Assert.AreNotEqual(serverApi.ServerObj, serverObj);
                    Assert.IsTrue(clientObj.IsDisposing);// Local scope wasn't configured to leave the value undisposed
                    Assert.IsTrue(serverObj.IsDisposing);// Remote scope wasn't configured to leave the value undisposed
                    Assert.IsTrue(serverApi.ClientObj.IsDisposing);// Remote scope wasn't configured to leave the value undisposed
                    Assert.IsTrue(serverApi.ServerObj.IsDisposing);// Local scope wasn't configured to leave the value undisposed
                    Assert.AreEqual(0, client.StoredScopeCount);
                    Assert.AreEqual(0, server.StoredScopeCount);
                    Assert.AreEqual(0, client.StoredRemoteScopeCount);
                    Assert.AreEqual(0, server.StoredRemoteScopeCount);
                    serverApi.ClientObj = null;
                    serverApi.ServerObj = null;
                }

                // Parameter and return value with disposing options
                Logging.WriteInfo("Parameter and return value with disposing options");
                using (TestDisposable clientObj = new()
                {
                    Name = "Client"
                })
                {
                    using TestRemoteScope remoteScope = await serverSdk.Scopes2Async(clientObj);
                    remoteScope.Name ??= "Remote server";
                    Assert.IsNull(client.GetScopeOf(clientObj));// Local scope wasn't stored and disposed when returning the value
                    await Task.Delay(200);// Wait for server call cleanup
                    Assert.IsNotNull(serverApi.ClientObj);
                    Assert.IsNotNull(serverApi.ServerObj);
                    Assert.AreNotEqual(serverApi.ClientObj, clientObj);
                    Assert.AreNotEqual(serverApi.ServerObj, remoteScope.Value);
                    Assert.IsTrue(clientObj.IsDisposing);// Local scope wasn't configured to leave the value undisposed
                    Assert.IsFalse(remoteScope.IsDisposing);// Remote scope not disposed 'cause it's a return value
                    Assert.IsTrue(serverApi.ClientObj.IsDisposing);// Remote scope wasn't configured to leave the value undisposed
                    Assert.IsFalse(serverApi.ServerObj.IsDisposing);// Local scope was configured to leave the value undisposed
                    Assert.AreEqual(0, client.StoredScopeCount);
                    Assert.AreEqual(0, server.StoredScopeCount);
                    Assert.AreEqual(0, client.StoredRemoteScopeCount);
                    Assert.AreEqual(0, server.StoredRemoteScopeCount);
                    serverApi.ServerObj.Dispose();
                    serverApi.ClientObj = null;
                    serverApi.ServerObj = null;
                }


                // Parameter and return value with extended client disposing options
                Logging.WriteInfo("Parameter and return value with extended client disposing options");
                using (TestDisposable clientObj = new()
                {
                    Name = "Client"
                })
                {
                    using TestRemoteScope serverScope = await serverSdk.Scopes3Async(clientObj);
                    serverScope.Name ??= "Remote server scope";
                    Assert.IsNotNull(serverScope.Value);
                    Assert.IsFalse(serverScope.IsDisposing);
                    Assert.IsNull(client.GetScopeOf(clientObj));// Local scope wasn't stored and disposed when returning the value
                    Assert.IsNull(client.GetRemoteScopeOf(serverScope.Value));// Remote scope wasn't stored when returning the value
                    await Task.Delay(200);// Wait for server call cleanup
                    Assert.IsNotNull(serverApi.ClientObj);
                    Assert.IsNotNull(serverApi.ServerObj);
                    Assert.AreNotEqual(serverApi.ClientObj, clientObj);
                    Assert.AreNotEqual(serverApi.ServerObj, serverScope.Value);
                    Assert.IsFalse(clientObj.IsDisposing);// Local scope was configured to leave the value undisposed
                    Assert.IsFalse(serverApi.ClientObj.IsDisposing);// API method and remote scope was configured to leave the value undisposed
                    Assert.IsFalse(serverApi.ServerObj.IsDisposing);// Local scope was configured to leave the value undisposed
                    Assert.AreEqual(0, client.StoredScopeCount);
                    Assert.AreEqual(0, server.StoredScopeCount);
                    Assert.AreEqual(0, client.StoredRemoteScopeCount);
                    Assert.AreEqual(0, server.StoredRemoteScopeCount);
                    serverApi.ClientObj.Dispose();
                    serverApi.ServerObj.Dispose();
                    serverApi.ClientObj = null;
                    serverApi.ServerObj = null;
                }
            }
            finally
            {
                await serverSdk.DisposeAsync();
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
