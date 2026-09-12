using System.Collections.Immutable;

namespace PaneShift.Core;

public static class HotkeySettings
{
    public static IReadOnlyList<HotkeyBinding> Resolve(PaneShiftSettings settings)
    {
        if (settings.Hotkeys is not null)
            foreach (string id in settings.Hotkeys.Keys)
                if (!ActionCatalog.All.Any(a => a.Id == id)) throw new ArgumentException($"Unknown or unimplemented shortcut action: {id}.");
        var bindings = new List<HotkeyBinding>();
        foreach (var action in ActionCatalog.All)
        {
            if (settings.Hotkeys is not null && settings.Hotkeys.TryGetValue(action.Id, out var text))
            {
                if (text is null) continue; // Explicitly disabled, unlike an absent entry.
                try
                {
                    var chord = HotkeyChord.Parse(text);
                    bindings.Add(new(chord.Modifiers, chord.VirtualKey, action.Action));
                }
                catch (ArgumentException ex) { throw new ArgumentException($"{action.Name}: {ex.Message}", ex); }
            }
            else if (PaneShiftConfiguration.Default.Hotkeys.FirstOrDefault(b => b.Action == action.Action) is { } binding)
                bindings.Add(binding);
        }
        foreach (var group in bindings.GroupBy(b => new HotkeyChord(b.Modifiers, b.VirtualKey)))
            if (group.Count() > 1)
                throw new ArgumentException($"{group.Key.Display} is assigned to both {string.Join(" and ", group.Select(b => ActionCatalog.Get(b.Action).Name))}.");
        return bindings.AsReadOnly();
    }

    public static ImmutableSortedDictionary<string, string?> ToMap(IEnumerable<HotkeyBinding> bindings)
    {
        var map = ImmutableSortedDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
        foreach (var action in ActionCatalog.All) map[action.Id] = null;
        foreach (var binding in bindings) map[ActionCatalog.Get(binding.Action).Id] = new HotkeyChord(binding.Modifiers, binding.VirtualKey).ToString();
        return map.ToImmutable();
    }
}
