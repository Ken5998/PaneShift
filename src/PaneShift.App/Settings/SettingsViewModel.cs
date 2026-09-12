using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Globalization;
using PaneShift.Core;

namespace PaneShift.App.Settings;

public sealed class ShortcutRow(ActionDefinition definition) : ObservableObject
{
    public ActionDefinition Definition { get; } = definition;
    public string Name => Definition.Name;
    public WindowAction Action => Definition.Action;
    private string? chord;
    public string? Chord { get => chord; set { if (chord == value) return; chord = value; Changed(); } }
    public RelayCommand ClearCommand => new(() => Chord = null);
}

public sealed record ShortcutGroup(string Name, IReadOnlyList<ShortcutRow> Rows);

public sealed class SettingsViewModel : ObservableObject
{
    private readonly Func<PaneShiftSettings, string?> apply;
    private PaneShiftSettings baseline;
    private PaneShiftSettings? external;
    private string baselineFingerprint = "";
    private bool loading;
    private string gapText = "0";
    private bool edges;
    private string feedback = "";
    private string validation = "";
    private string status = "";
    public IReadOnlyList<ShortcutRow> Rows { get; }
    public ObservableCollection<ShortcutGroup> Groups { get; }
    public RelayCommand ApplyCommand { get; }
    public LoginStartupViewModel? LoginStartup { get; }
    public string GapText { get => gapText; set { gapText = value; Edited(); Changed(nameof(GapValue)); } }
    public double GapValue { get => int.TryParse(gapText, out int n) ? Math.Clamp(n, 0, 64) : 0; set => GapText = ((int)value).ToString(CultureInfo.InvariantCulture); }
    public bool ApplyGapToScreenEdges { get => edges; set { edges = value; Edited(); } }
    public string Validation => validation;
    public string Feedback => feedback;
    public bool HasChanges => Fingerprint() != baselineFingerprint;
    public bool HasExternalChange => external is not null;
    public bool CanApply => HasChanges && validation.Length == 0 && !HasExternalChange;
    public string RuntimeStatus { get => status; set { status = value; Changed(); } }

    public SettingsViewModel(PaneShiftSettings current, Func<PaneShiftSettings, string?> apply, LoginStartupViewModel? loginStartup = null)
    {
        this.apply = apply;
        LoginStartup = loginStartup;
        baseline = current;
        Rows = ActionCatalog.All.Select(a => new ShortcutRow(a)).ToArray();
        Groups = new(Rows.GroupBy(r => r.Definition.Group).Select(g => new ShortcutGroup(g.Key, g.ToArray())));
        ApplyCommand = new(() => Apply(), () => CanApply);
        foreach (var row in Rows) row.PropertyChanged += (_, _) => Edited();
        Load(current);
    }

    public void Load(PaneShiftSettings settings)
    {
        loading = true;
        baseline = settings;
        gapText = settings.GapPixels.ToString(CultureInfo.InvariantCulture);
        edges = settings.ApplyGapToScreenEdges;
        var bindings = HotkeySettings.Resolve(settings);
        foreach (var row in Rows)
        {
            var binding = bindings.FirstOrDefault(b => b.Action == row.Action);
            row.Chord = binding is null ? null : new HotkeyChord(binding.Modifiers, binding.VirtualKey).ToString();
        }
        baselineFingerprint = Fingerprint();
        external = null;
        feedback = "";
        loading = false;
        Refresh();
    }

    private string Fingerprint() => $"{gapText}|{edges}|" + string.Join("|", Rows.Select(r => r.Chord ?? "-"));

    public PaneShiftSettings CreateCandidate()
    {
        if (!int.TryParse(gapText, NumberStyles.None, CultureInfo.InvariantCulture, out int gap))
            throw new ArgumentException("Enter a non-negative whole number of pixels (0–2147483647).");
        var map = ImmutableSortedDictionary.CreateBuilder<string, string?>(StringComparer.Ordinal);
        foreach (var row in Rows) map[row.Definition.Id] = row.Chord;
        var candidate = baseline with { GapPixels = gap, ApplyGapToScreenEdges = edges, Hotkeys = map.ToImmutable() };
        candidate.Validate();
        return candidate;
    }

    private void Edited()
    {
        if (loading) return;
        feedback = "";
        Refresh();
    }

    private void Refresh()
    {
        try { _ = CreateCandidate(); validation = ""; }
        catch (ArgumentException ex) { validation = ex.Message; }
        Changed(nameof(GapText)); Changed(nameof(GapValue)); Changed(nameof(ApplyGapToScreenEdges));
        Changed(nameof(HasChanges)); Changed(nameof(HasExternalChange)); Changed(nameof(CanApply));
        Changed(nameof(Validation)); Changed(nameof(Feedback));
        ApplyCommand.Refresh();
    }

    public bool Apply()
    {
        if (!CanApply) return !HasChanges && !HasExternalChange;
        var candidate = CreateCandidate();
        var error = apply(candidate);
        if (error is not null) { feedback = error; Changed(nameof(Feedback)); return false; }
        Load(candidate);
        feedback = "✓ Settings applied";
        Changed(nameof(Feedback));
        return true;
    }

    public void ResetShortcuts()
    {
        var defaults = PaneShiftConfiguration.Default.Hotkeys;
        loading = true;
        foreach (var row in Rows)
        {
            var binding = defaults.FirstOrDefault(b => b.Action == row.Action);
            row.Chord = binding is null ? null : new HotkeyChord(binding.Modifiers, binding.VirtualKey).ToString();
        }
        loading = false;
        Edited();
    }

    public void RestoreDefaults() { gapText = "0"; edges = false; ResetShortcuts(); }

    public void ExternalReload(PaneShiftSettings settings)
    {
        if (!HasChanges) Load(settings);
        else { external = settings; Refresh(); }
    }

    public void ReloadDraft() { if (external is { } settings) Load(settings); }
}
