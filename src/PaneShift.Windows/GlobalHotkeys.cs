using PaneShift.Core;

namespace PaneShift.Windows;

public sealed record HotkeyRegistrationFailure(HotkeyBinding Binding, string Reason);

/// <summary>Owned by the message-window thread. Existing chords remain reserved during transitions.</summary>
public sealed class GlobalHotkeys : IDisposable
{
    public const int HotkeyMessage = 0x0312;
    private readonly IHotkeyBackend backend;
    private readonly Dictionary<HotkeyChord, int> held = [];
    private Dictionary<int, HotkeyBinding> active = [];
    private int nextId = 1;
    private bool disposed;
    public string? CleanupWarning { get; private set; }

    public GlobalHotkeys(nint messageWindow) : this(new NativeHotkeyBackend(messageWindow)) { }
    public GlobalHotkeys(IHotkeyBackend backend) => this.backend = backend;

    public IReadOnlyList<HotkeyRegistrationFailure> Register(IReadOnlyList<HotkeyBinding> bindings)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active.Count != 0) throw new InvalidOperationException("Hotkeys are already registered.");
        var failures = new List<HotkeyRegistrationFailure>();
        foreach (var binding in bindings)
        {
            var chord = new HotkeyChord(binding.Modifiers, binding.VirtualKey);
            string? error = Acquire(chord, out int id);
            if (error is null) active[id] = binding;
            else failures.Add(new(binding, error));
        }
        return failures;
    }

    public string? Transition(IReadOnlyList<HotkeyBinding> bindings, Action commit)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var before = held.Keys.ToHashSet();
        var candidate = new Dictionary<int, HotkeyBinding>();
        try
        {
            foreach (var binding in bindings)
            {
                var chord = new HotkeyChord(binding.Modifiers, binding.VirtualKey);
                string? error = Acquire(chord, out int id);
                if (error is not null)
                    return $"{ActionCatalog.Get(binding.Action).Name} — {chord.Display}: {error}";
                candidate.Add(id, binding);
            }
            commit(); // Atomic file save first; runtime commit cannot fail after validation.
            active = candidate;
            ReleaseExcept(candidate.Keys.ToHashSet());
            return null;
        }
        finally
        {
            // On failure only new reservations are removed. Old working hotkeys never left Windows.
            if (!ReferenceEquals(active, candidate))
                foreach (var chord in held.Keys.Where(c => !before.Contains(c)).ToArray()) Release(chord);
        }
    }

    private string? Acquire(HotkeyChord chord, out int id)
    {
        if (held.TryGetValue(chord, out id)) return null;
        // IDs are never reused, so queued WM_HOTKEY messages cannot invoke a new binding accidentally.
        if (nextId > 0xBFFF) return "Hotkey ID limit reached. Restart PaneShift.";
        id = nextId++;
        string? error = backend.Register(id, chord);
        if (error is null) held.Add(chord, id);
        return error;
    }

    private void Release(HotkeyChord chord)
    {
        string? error = backend.Unregister(held[chord]);
        if (error is null) held.Remove(chord);
        else CleanupWarning = $"Could not release {chord.Display}: {error}";
    }

    private void ReleaseExcept(HashSet<int> keep)
    {
        foreach (var pair in held.Where(p => !keep.Contains(p.Value)).ToArray()) Release(pair.Key);
    }

    public void ReleaseAll() { active.Clear(); ReleaseExcept([]); }
    public bool TryGetBinding(int id, out HotkeyBinding? binding) => active.TryGetValue(id, out binding);
    public bool TryGetAction(int id, out WindowAction action)
    {
        bool found = active.TryGetValue(id, out var binding);
        action = binding?.Action ?? default;
        return found;
    }

    public void Dispose()
    {
        if (disposed) return;
        ReleaseAll();
        disposed = true;
    }
}
