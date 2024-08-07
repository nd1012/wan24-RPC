﻿using wan24.Core;

namespace wan24_RPC_Tests
{
    public sealed class TestDisposable() : DisposableBase(asyncDisposing: false)
    {
        public string? Name = null;

        protected override void Dispose(bool disposing) => Logging.WriteInfo($"{typeof(TestDisposable)} \"{Name ?? "(Unknown)"}\" disposing");
    }
}
