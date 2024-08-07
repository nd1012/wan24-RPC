﻿using Microsoft.Extensions.Logging;
using System.Reflection;
using wan24.Core;
using wan24.ObjectValidation;
using wan24.RPC.Processing;
using wan24.RPC.Processing.Scopes;

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
            DisposableBase.CreateStackInfo = true;
            DisposableRecordBase.CreateStackInfo = true;
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
            ErrorHandling.ErrorHandler = (error) =>
            {
                Logging.WriteError(error.Exception.ToString());
                if (error.Exception is StackInfoException ex)
                {
                    Logging.WriteInfo(ex.StackInfo.Stack);
                    if (error.Tag is TestDisposable disposable)
                        Logging.WriteInfo($"TestDisposable \"{disposable.Name}\"");
                }
            };
            TypeHelper.Instance.ScanAssemblies(typeof(RpcProcessor).Assembly);
            RpcScopes.RegisterScope(new()
            {
                Type = TestScope.HL_TYPE,
                LocalScopeType = typeof(TestScope),
                RemoteScopeType = typeof(TestRemoteScope),
                LocalScopeFactory = TestScope.CreateAsync,
                RemoteScopeFactory = TestRemoteScope.CreateAsync,
                LocalScopeParameterFactory = TestScopeParameter.CreateAsync,
                LocalScopeObjectTypes = [typeof(TestDisposable)],
                ParameterLocalScopeFactory = (proc, value, ct) => Task.FromResult<RpcProcessor.RpcScopeBase?>(new TestScope((TestRpcProcessor)proc) { ScopeParameter = new TestScopeParameter() { ScopeObject = value } }),
                RemoteScopeObjectTypes = [typeof(TestDisposable)],
                ReturnLocalScopeFactory = (proc, method, value, ct) => Task.FromResult<RpcProcessor.RpcScopeBase?>(new TestScope((TestRpcProcessor)proc) { ScopeParameter = new TestScopeParameter() { ScopeObject = value } })
            });
            Logging.WriteInfo("wan24-RPC Tests initialized");
        }
    }
}
