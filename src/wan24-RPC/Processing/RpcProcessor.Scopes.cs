using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using wan24.Core;
using wan24.RPC.Processing.Exceptions;
using wan24.RPC.Processing.Messages;
using wan24.RPC.Processing.Messages.Scopes;
using wan24.RPC.Processing.Scopes;
using static System.Formats.Asn1.AsnWriter;

namespace wan24.RPC.Processing
{
    // Scopes
    public partial class RpcProcessor
    {
        /// <summary>
        /// Scopes (key is the ID)
        /// </summary>
        protected readonly ConcurrentDictionary<long, RpcScopeBase> Scopes = [];
        /// <summary>
        /// Keyed scopes (key is the scope key)
        /// </summary>
        protected readonly ConcurrentDictionary<string, RpcScopeBase> KeyedScopes = [];
        /// <summary>
        /// Remote scopes (key is the ID)
        /// </summary>
        protected readonly ConcurrentDictionary<long, RpcRemoteScopeBase> RemoteScopes = [];
        /// <summary>
        /// Keyed remote scopes (key is the scope key)
        /// </summary>
        protected readonly ConcurrentDictionary<string, RpcRemoteScopeBase> KeyedRemoteScopes = [];
        /// <summary>
        /// Scope ID
        /// </summary>
        protected long ScopeId = 0;
        /// <summary>
        /// Number of stored scopes
        /// </summary>
        protected volatile int _StoredScopeCount = 0;
        /// <summary>
        /// Number of stored remote scopes
        /// </summary>
        protected volatile int _StoredRemoteScopeCount = 0;

        /// <summary>
        /// Create a scope ID
        /// </summary>
        /// <returns>Scope ID</returns>
        protected virtual long CreateScopeId()
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return Interlocked.Increment(ref ScopeId);
        }

        /// <summary>
        /// Add a scope
        /// </summary>
        /// <param name="scope">Scope (will be disposed, if stored)</param>
        /// <returns>If stored</returns>
        protected virtual bool AddScope(RpcScopeBase scope)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            Logger?.Log(LogLevel.Trace, "{this} storing scope \"{scope}\" (using key \"{key}\")", ToString(), scope.ToString(), scope.Key);
            if (!scope.IsStored)
            {
                Logger?.Log(LogLevel.Trace, "{this} won't store scope #{scope}", ToString(), scope.Id);
                return false;
            }
            bool scopeAdded = false;
            try
            {
                if (_StoredScopeCount > Options.ScopeLimit)
                    throw new TooManyRpcScopesException();
                if (!Scopes.TryAdd(scope.Id, scope))
                    throw new InvalidOperationException($"Scope #{scope.Id} added already (double scope ID)");
                scopeAdded = true;
                _StoredScopeCount++;
                Logger?.Log(LogLevel.Trace, "{this} scope #{scope} stored (now storing {count} scopes)", ToString(), scope.Id, _StoredScopeCount);
                if (scope.Key is not null && !KeyedScopes.TryAdd(scope.Key, scope))
                    throw new InvalidOperationException($"Scope #{scope.Id} key exists already");
                return true;
            }
            catch
            {
                if (scopeAdded && Scopes.TryRemove(scope.Id, out _))
                    _StoredScopeCount--;
                scope.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Add a remote scope
        /// </summary>
        /// <param name="scope">Scope (will be disposed)</param>
        /// <returns>If stored</returns>
        protected virtual async Task<bool> AddRemoteScopeAsync(RpcRemoteScopeBase scope)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            Logger?.Log(LogLevel.Trace, "{this} storing scope \"{scope}\" (using key \"{key}\")", ToString(), scope.ToString(), scope.Key);
            if (!scope.IsStored)
            {
                Logger?.Log(LogLevel.Trace, "{this} won't store scope #{scope}", ToString(), scope.Id);
                return false;
            }
            bool scopeAdded = false;
            try
            {
                if (_StoredRemoteScopeCount > Options.ScopeLimit)
                    throw new TooManyRpcRemoteScopesException();
                if (!RemoteScopes.TryAdd(scope.Id, scope))
                    throw new InvalidOperationException($"Remote scope #{scope.Id} added already (double remote scope ID)");
                scopeAdded = true;
                _StoredRemoteScopeCount++;
                Logger?.Log(LogLevel.Trace, "{this} scope #{scope} stored (now storing {count} scopes)", ToString(), scope.Id, _StoredRemoteScopeCount);
                if (scope.Key is not null)
                    while (EnsureNotCanceled())
                        if (KeyedRemoteScopes.TryGetValue(scope.Key, out RpcRemoteScopeBase? existing))
                        {
                            if (!scope.ReplaceExistingScope)
                                throw new InvalidOperationException($"Remote scope #{scope.Id} key exists already");
                            if (!KeyedRemoteScopes.TryUpdate(scope.Key, scope, existing))
                                continue;
                            await existing.DisposeAsync().DynamicContext();
                            break;
                        }
                        else if (KeyedRemoteScopes.TryAdd(scope.Key, scope))
                        {
                            break;
                        }
                return true;
            }
            catch
            {
                if (scopeAdded && RemoteScopes.TryRemove(scope.Id, out _))
                    _StoredRemoteScopeCount--;
                await scope.DisposeAsync().DynamicContext();
                throw;
            }
        }

        /// <summary>
        /// Get a scope
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Scope (will be disposed)</returns>
        protected virtual RpcScopeBase? GetScope(in long id)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return Scopes.TryGetValue(id, out RpcScopeBase? res) ? res : null;
        }

        /// <summary>
        /// Get a remote scope
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Remote scope (will be disposed)</returns>
        protected virtual RpcRemoteScopeBase? GetRemoteScope(in long id)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return RemoteScopes.TryGetValue(id, out RpcRemoteScopeBase? res) ? res : null;
        }

        /// <summary>
        /// Remove a scope
        /// </summary>
        /// <param name="scope">Scope (don't forget to dispose!)</param>
        /// <returns>If removed</returns>
        protected virtual bool RemoveScope(in RpcScopeBase scope)
        {
            EnsureUndisposed(allowDisposing: true);
            EnsureScopesAreEnabled();
            if (!Scopes.TryRemove(scope.Id, out _))
                return false;
            _StoredScopeCount--;
            if (scope.Key is not null)
                KeyedScopes.TryRemove(new KeyValuePair<string, RpcScopeBase>(scope.Key, scope));
            Logger?.Log(LogLevel.Trace, "{this} removed \"{scope}\"", ToString(), scope.ToString());
            return true;
        }

        /// <summary>
        /// Remove a remote scope
        /// </summary>
        /// <param name="scope">Remote scope (don't forget to dispose!)</param>
        /// <returns>If removed</returns>
        protected virtual bool RemoveRemoteScope(in RpcRemoteScopeBase scope)
        {
            EnsureUndisposed(allowDisposing: true);
            EnsureScopesAreEnabled();
            if (!RemoteScopes.TryRemove(scope.Id, out RpcRemoteScopeBase? removed))
                return false;
            _StoredRemoteScopeCount--;
            if (scope.Key is not null)
                KeyedRemoteScopes.TryRemove(new KeyValuePair<string, RpcRemoteScopeBase>(scope.Key, removed));
            Logger?.Log(LogLevel.Trace, "{this} removed \"{scope}\"", ToString(), scope.ToString());
            return true;
        }

        /// <summary>
        /// Remove a scope
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Removed scope (don't forget to dispose!)</returns>
        protected virtual RpcScopeBase? RemoveScope(in long id)
        {
            EnsureUndisposed(allowDisposing: true);
            EnsureScopesAreEnabled();
            if (!Scopes.TryGetValue(id, out RpcScopeBase? res))
                return null;
            _StoredScopeCount--;
            if (res.Key is not null)
                KeyedScopes.TryRemove(new KeyValuePair<string, RpcScopeBase>(res.Key, res));
            Scopes.TryRemove(id, out _);
            Logger?.Log(LogLevel.Trace, "{this} removed \"{scope}\"", ToString(), res.ToString());
            return res;
        }

        /// <summary>
        /// Remove a remote scope
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns>Removed remote scope (don't forget to dispose!)</returns>
        protected virtual RpcRemoteScopeBase? RemoveRemoteScope(in long id)
        {
            EnsureUndisposed(allowDisposing: true);
            EnsureScopesAreEnabled();
            if (!RemoteScopes.TryGetValue(id, out RpcRemoteScopeBase? res))
                return null;
            _StoredRemoteScopeCount--;
            if (res.Key is not null)
                KeyedRemoteScopes.TryRemove(new KeyValuePair<string, RpcRemoteScopeBase>(res.Key, res));
            RemoteScopes.TryRemove(id, out _);
            Logger?.Log(LogLevel.Trace, "{this} removed \"{scope}\"", ToString(), res.ToString());
            return res;
        }

        /// <summary>
        /// Handle a remote scope registration (processing will be stopped on handler exception)
        /// </summary>
        /// <param name="message">Message</param>
        protected virtual async Task HandleRemoteScopeRegistration(ScopeRegistrationMessage message)
        {
            Logger?.Log(LogLevel.Debug, "{this} registering remote scope #{id} of type #{type}", ToString(), message.Value.Id, message.Value.Type);
            EnsureScopesAreEnabled();
            if (!message.Value.IsStored)
            {
                Logger?.Log(LogLevel.Warning, "{this} can't register not stored remote scope #{id} of type #{type}", ToString(), message.Value.Id, message.Value.Type);
                throw new InvalidDataException($"Remote scope registration #{message.Value.Id} wouldn't be stored");
            }
            if (RpcScopes.GetRemoteScopeFactory(message.Value.Type) is not RpcScopes.RemoteScopeFactory_Delegate factory)
            {
                Logger?.Log(LogLevel.Warning, "{this} can't register remote scope #{id} of unknown type #{type}", ToString(), message.Value.Id, message.Value.Type);
                throw new InvalidDataException($"No remote scope factory for type #{message.Value.Type} for registering remote scope #{message.Value.Id}");
            }
            RpcRemoteScopeBase? remoteScope = null;
            try
            {
                remoteScope = await factory(this, message.Value, CancelToken).DynamicContext();
                if (!remoteScope.IsStored)
                {
                    Logger?.Log(LogLevel.Warning, "{this} can't register remote scope #{id} of type {type} because it wasn't stored", ToString(), message.Value.Id, remoteScope.GetType());
                    InvalidOperationException exception = new($"Remote scope {remoteScope} (#{message.Value.Id}) can't be registered, because it wasn't stored");
                    await remoteScope.SetIsErrorAsync(exception).DynamicContext();
                    await remoteScope.DisposeAsync();
                    throw exception;
                }
                await AddRemoteScopeAsync(remoteScope).DynamicContext();
                await remoteScope.OnScopeCreated(CancelToken).DynamicContext();
                await SendMessageAsync(new ResponseMessage()
                {
                    Id = message.Id,
                    PeerRpcVersion = Options.RpcVersion
                }, Options.Priorities.Rpc).DynamicContext();
            }
            catch (ObjectDisposedException) when (IsDisposing)
            {
                Logger?.Log(LogLevel.Debug, "{this} registering remote scope #{id} of type #{type} canceled due to disposing", ToString(), message.Value.Id, message.Value.Type);
            }
            catch (OperationCanceledException) when (CancelToken.IsCancellationRequested)
            {
                Logger?.Log(LogLevel.Debug, "{this} registering remote scope #{id} of type #{type} canceled", ToString(), message.Value.Id, message.Value.Type);
            }
            catch (Exception ex)
            {
                Logger?.Log(LogLevel.Warning, "{this} remote scope #{id} of type #{type} registration failed exceptional: {ex}", ToString(), message.Value.Id, message.Value.Type, ex.Message);
                try
                {
                    await SendMessageAsync(new ErrorResponseMessage()
                    {
                        Id = message.Id,
                        PeerRpcVersion = Options.RpcVersion,
                        Error = ex
                    }, Options.Priorities.Rpc).DynamicContext();
                }
                catch
                {
                }
                throw;
            }
        }

        /// <summary>
        /// Ensure scopes are enabled
        /// </summary>
        protected virtual void EnsureScopesAreEnabled()
        {
            if (!Options.UseScopes)
                throw new InvalidOperationException("Scopes are disabled");
        }
    }
}
