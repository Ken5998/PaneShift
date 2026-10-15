namespace PaneShift.Windows.Tests;

public sealed class AtSignInputTests
{
    private sealed class Input(int? prefix = null) : IShortcutInputBackend
    {
        public HashSet<(ushort Key, bool Unicode)> Down { get; } = [];
        public List<(ushort Character, ushort[] Modifiers)> Text { get; } = [];
        public List<ShortcutKeyInput> Accepted { get; } = [];
        public int Calls;
        public int StateQueries;
        public bool IsDown(ushort key) { StateQueries++; return Down.Contains((key, false)); }
        public int Send(IReadOnlyList<ShortcutKeyInput> inputs)
        {
            int count = Calls++ == 0 ? prefix ?? inputs.Count : inputs.Count;
            foreach (var input in inputs.Take(count))
            {
                Accepted.Add(input);
                var state = (input.Key, input.Unicode);
                if (input.Up) Down.Remove(state);
                else
                {
                    if (input.Unicode) Text.Add((input.Key, Down.Where(k => !k.Unicode && k.Key != ShortcutInputSender.MenuMaskKey).Select(k => k.Key).Order().ToArray()));
                    Down.Add(state);
                }
            }
            return count;
        }
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void UnicodeAtSignNeverReleasesOrRestoresAltGrOrItsCompanionControl(bool syntheticControl)
    {
        var input = new Input(); input.Down.Add((0xA5, false));
        if (syntheticControl) input.Down.Add((0xA2, false));
        var before = input.Down.Order().ToArray();
        Assert.True(new ShortcutInputSender(input).SendAtSign());
        var text = Assert.Single(input.Text);
        Assert.Equal((ushort)'@', text.Character);
        Assert.Equal(before.Select(k => k.Key), text.Modifiers);
        Assert.Equal(before, input.Down.Order().ToArray());
        Assert.Equal(1, input.Calls);
        Assert.Equal(0, input.StateQueries);
        AssertNoModifierInjection(input);
    }

    [Fact]
    public void WindowGestureMaskNeverAltersHeldModifiersOrTypesTextEvenAfterPartialSend()
    {
        foreach (int count in new[] { 0, 1, 2 })
        {
            var input = new Input(count); input.Down.UnionWith(new (ushort, bool)[] { (0xA2, false), (0xA4, false) });
            var before = input.Down.Order().ToArray();
            Assert.Equal(count == 2, new ShortcutInputSender(input).MaskMenu());
            Assert.Empty(input.Text);
            Assert.Equal(before, input.Down.Order().ToArray());
        }
    }

    [Fact]
    public void EveryPartialUnicodePrefixReleasesPacketsWithoutTouchingAltGrOrRetryingText()
    {
        ushort[] held = [0xA5, 0xA2];
        int length = ShortcutInputSender.CreateTextSequence('@').Count;
        for (int prefix = 0; prefix < length; prefix++)
        {
            var input = new Input(prefix); input.Down.UnionWith(held.Select(k => (k, false)));
            var before = input.Down.Order().ToArray();
            Assert.False(new ShortcutInputSender(input).SendAtSign());
            Assert.Equal(before, input.Down.Order().ToArray());
            Assert.True(input.Text.Count <= 1);
            Assert.Equal(prefix == 0 ? 1 : 2, input.Calls);
            Assert.Equal(0, input.StateQueries);
            AssertNoModifierInjection(input);
        }
    }

    private static void AssertNoModifierInjection(Input input) =>
        Assert.All(input.Accepted, key => Assert.True(key.Unicode || key.Key == ShortcutInputSender.MenuMaskKey,
            "At-sign insertion and recovery must never manufacture Ctrl/Alt/Shift/Win key transitions."));
}
