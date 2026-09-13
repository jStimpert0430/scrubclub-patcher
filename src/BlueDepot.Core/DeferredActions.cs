using System;

namespace BlueDepot.Core;

// Restore normal resource visibility before rebuilding any cached recipe UI.
public sealed class ExecutionWindow
{
    int depth;
    public bool Active => depth > 0;
    public IDisposable Enter(Action refresh)
    {
        depth++;
        return new Lease(() => { if (--depth == 0) refresh(); });
    }
    sealed class Lease : IDisposable
    {
        Action? close;
        public Lease(Action close) { this.close = close; }
        public void Dispose() { var action = close; close = null; action?.Invoke(); }
    }
}

public sealed class DeferredIntent
{
    bool pending = true;
    public bool TryConsume(bool valid)
    {
        if (!pending) return false;
        pending = false;
        return valid;
    }
}

public static class StationSupplyRules
{
    public static bool ShouldFetch(bool valid, bool hasCapacity, bool carried, bool stored,
        bool explicitItem, bool busy) => valid && hasCapacity && !carried && stored && !explicitItem && !busy;
}
