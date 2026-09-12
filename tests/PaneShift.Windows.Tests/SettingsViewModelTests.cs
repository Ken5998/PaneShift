using PaneShift.App.Settings;
using PaneShift.Core;

namespace PaneShift.Windows.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void DraftEditsDoNotActivateUntilApply()
    {
        PaneShiftSettings? saved = null;
        var original = new PaneShiftSettings();
        var model = new SettingsViewModel(original, s => { saved = s; return null; });
        model.GapText = "12";
        Assert.Null(saved);
        Assert.Equal(0, original.GapPixels);
        Assert.True(model.CanApply);
        Assert.True(model.Apply());
        Assert.Equal(12, saved!.GapPixels);
        Assert.False(model.HasChanges);
    }

    [Fact]
    public void DuplicateBlocksApplyAndNamesBothActions()
    {
        var model = new SettingsViewModel(new(), _ => throw new Exception("Must not activate"));
        model.Rows.Single(r => r.Action == WindowAction.FirstThird).Chord = "Ctrl+Alt+Left";
        Assert.False(model.CanApply);
        Assert.Contains("Left Half", model.Validation);
        Assert.Contains("First Third", model.Validation);
        Assert.False(model.Apply());
    }

    [Fact]
    public void FailedActivationKeepsDraftAndFeedback()
    {
        var model = new SettingsViewModel(new(), _ => "Combination occupied");
        model.GapText = "12";
        Assert.False(model.Apply());
        Assert.True(model.HasChanges);
        Assert.Equal("12", model.GapText);
        Assert.Equal("Combination occupied", model.Feedback);
    }

    [Fact]
    public void ExternalReloadRefreshesCleanDraftButPreservesDirtyDraft()
    {
        var model = new SettingsViewModel(new(), _ => null);
        model.ExternalReload(new() { GapPixels = 12 });
        Assert.Equal("12", model.GapText);
        Assert.False(model.HasChanges);
        model.GapText = "20";
        model.ExternalReload(new() { GapPixels = 8 });
        Assert.Equal("20", model.GapText);
        Assert.True(model.HasExternalChange);
        Assert.False(model.CanApply);
        model.ReloadDraft();
        Assert.Equal("8", model.GapText);
        Assert.False(model.HasChanges);
    }

    [Fact]
    public void ClearAndResetOnlyEditTheDraft()
    {
        var model = new SettingsViewModel(new(), _ => throw new Exception("Must not activate"));
        model.Rows.Single(r => r.Action == WindowAction.LeftHalf).ClearCommand.Execute(null);
        Assert.DoesNotContain(HotkeySettings.Resolve(model.CreateCandidate()), b => b.Action == WindowAction.LeftHalf);
        model.ResetShortcuts();
        Assert.False(model.HasChanges);
        model.GapText = "12";
        model.ApplyGapToScreenEdges = true;
        model.RestoreDefaults();
        Assert.False(model.HasChanges);
    }

    [Theory]
    [InlineData("-1", false)]
    [InlineData("1.5", false)]
    [InlineData("", false)]
    [InlineData("2147483648", false)]
    [InlineData("128", true)]
    public void GapValidationPreservesBackendRange(string text, bool valid)
    {
        var model = new SettingsViewModel(new(), _ => null) { GapText = text };
        Assert.Equal(valid, model.CanApply);
    }
}
