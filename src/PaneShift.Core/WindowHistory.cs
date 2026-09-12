namespace PaneShift.Core;

public sealed record WindowState(PixelRect Bounds, bool WasMaximized);

/// <summary>Retains the first pre-change state, rather than the last tile.</summary>
public sealed class WindowHistory
{
    private readonly Dictionary<nint, WindowState> states = [];
    public void Remember(nint handle, WindowState state) => states.TryAdd(handle, state);
    public bool TryGet(nint handle, out WindowState? state) => states.TryGetValue(handle, out state);
    public void Forget(nint handle) => states.Remove(handle);
}
