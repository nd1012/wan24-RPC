using wan24.Core;

namespace wan24_RPC_Tests
{
    public sealed class TestDisposable() : DisposableBase(asyncDisposing: false)
    {
        protected override void Dispose(bool disposing) { }
    }
}
