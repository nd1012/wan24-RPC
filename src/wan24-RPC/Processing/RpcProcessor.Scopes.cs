using System.Collections.Concurrent;
using wan24.Core;

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
        /// Get a scope
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Scope (will be disposed)</returns>
        public virtual RpcScopeBase? GetScope(in string key)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return KeyedScopes.TryGetValue(key, out RpcScopeBase? res) ? res : null;
        }

        /// <summary>
        /// Get a remote scope
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Remote scope (will be disposed)</returns>
        public virtual RpcRemoteScopeBase? GetRemoteScope(in string key)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            return KeyedRemoteScopes.TryGetValue(key, out RpcRemoteScopeBase? res) ? res : null;
        }

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
        /// <param name="scope">Scope (will be disposed)</param>
        protected virtual void AddScope(RpcScopeBase scope)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            if (!scope.IsStored)
                return;
            bool scopeAdded = false;
            try
            {
                if (!Scopes.TryAdd(scope.Id, scope))
                    throw new InvalidOperationException($"Scope #{scope.Id} added already (double scope ID)");
                scopeAdded = true;
                if (scope.Key is not null && !KeyedScopes.TryAdd(scope.Key, scope))
                    throw new InvalidOperationException($"Scope #{scope.Id} key exists already");
            }
            catch
            {
                if (scopeAdded)
                    Scopes.TryRemove(scope.Id, out _);
                scope.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Add a remote scope
        /// </summary>
        /// <param name="scope">Scope (will be disposed)</param>
        protected virtual async Task AddRemoteScopeAsync(RpcRemoteScopeBase scope)
        {
            EnsureUndisposed();
            EnsureScopesAreEnabled();
            if (!scope.IsStored)
                return;
            bool scopeAdded = false;
            try
            {
                if (!RemoteScopes.TryAdd(scope.Id, scope))
                    throw new InvalidOperationException($"Remote scope #{scope.Id} added already (double remote scope ID)");
                scopeAdded = true;
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
            }
            catch
            {
                if (scopeAdded)
                    RemoteScopes.TryRemove(scope.Id, out _);
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
            if (scope.Key is not null)
                KeyedScopes.TryRemove(new KeyValuePair<string, RpcScopeBase>(scope.Key, scope));
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
            if (scope.Key is not null)
                KeyedRemoteScopes.TryRemove(new KeyValuePair<string, RpcRemoteScopeBase>(scope.Key, removed));
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
            if (res.Key is not null)
                KeyedScopes.TryRemove(new KeyValuePair<string, RpcScopeBase>(res.Key, res));
            Scopes.TryRemove(id, out _);
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
            if (res.Key is not null)
                KeyedRemoteScopes.TryRemove(new KeyValuePair<string, RpcRemoteScopeBase>(res.Key, res));
            RemoteScopes.TryRemove(id, out _);
            return res;
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
