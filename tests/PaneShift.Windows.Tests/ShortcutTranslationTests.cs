using PaneShift.Core;

namespace PaneShift.Windows.Tests;

public sealed class ShortcutTranslationTests
{
    private sealed class Input : IShortcutInputBackend
    {
        public HashSet<ushort> Down { get; } = [];
        public List<(ushort Key, ushort[] Modifiers)> TargetDown { get; } = [];
        public int? AcceptFirst { get; init; }
        public int Calls { get; private set; }
        public bool IsDown(ushort key) => Down.Contains(key);
        public int Send(IReadOnlyList<ShortcutKeyInput> inputs)
        {
            int accepted = Calls++ == 0 ? AcceptFirst ?? inputs.Count : inputs.Count;
            foreach (var input in inputs.Take(accepted))
            {
                if (input.Up) Down.Remove(input.Key);
                else
                {
                    if (input.Key is >= 0x41 and <= 0x5A or 0x73)
                        TargetDown.Add((input.Key, Down.Order().ToArray()));
                    Down.Add(input.Key);
                }
            }
            return accepted;
        }
    }

    [Fact]
    public void EveryTargetHasOnlyItsRequiredModifiersAndRestoresPhysicalKeys()
    {
        foreach (var mapping in MacStyleShortcuts.All)
        foreach (ushort win in new ushort[] { 0x5B, 0x5C })
        {
            var input = new Input(); input.Down.Add(win);
            bool shift = mapping.Source.Modifiers.HasFlag(HotkeyModifiers.Shift);
            if (shift) input.Down.Add(0xA1);
            var before = input.Down.Order().ToArray();
            Assert.True(new ShortcutInputSender(input).Send(mapping));
            var target = Assert.Single(input.TargetDown);
            Assert.Equal(mapping.Target.VirtualKey, (uint)target.Key);
            ushort[] expected = mapping.Target.Modifiers.HasFlag(HotkeyModifiers.Control)
                ? shift ? [0xA0, 0xA2] : [0xA2] : [0xA4];
            Assert.Equal(expected, target.Modifiers);
            Assert.Equal(before, input.Down.Order().ToArray());
        }
    }

    [Fact]
    public void EveryPartialPrefixRecoversOriginalModifierState()
    {
        foreach (var mapping in MacStyleShortcuts.All)
        {
            ushort[] held = [0x5B, 0x5C, 0xA0, 0xA1];
            int length = ShortcutInputSender.CreateSequence(mapping, held).Count;
            for (int prefix = 0; prefix < length; prefix++)
            {
                var input = new Input { AcceptFirst = prefix }; input.Down.UnionWith(held);
                Assert.False(new ShortcutInputSender(input).Send(mapping));
                Assert.Equal(held.Order(), input.Down.Order());
                Assert.Equal(prefix == 0 ? 1 : 2, input.Calls);
            }
        }
    }

    [Fact]
    public void FilterConsumesOnlyExactWhitelistIncludingMatchingUpsAndNoRepeat()
    {
        var filter = new ShortcutTranslationFilter(); int sent = 0;
        bool Send(ShortcutTranslation _) { sent++; return true; }
        foreach (var mapping in MacStyleShortcuts.All)
        {
            var source = mapping.Source;
            Assert.False(filter.Process(source.VirtualKey, false, true, source.Modifiers, Send));
            Assert.False(filter.Process(source.VirtualKey, false, false, source.Modifiers | HotkeyModifiers.Alt, Send));
            Assert.True(filter.Process(source.VirtualKey, false, false, source.Modifiers, Send));
            Assert.True(filter.Process(source.VirtualKey, false, false, source.Modifiers, Send));
            Assert.True(filter.Process(source.VirtualKey, true, false, HotkeyModifiers.None, Send));
            Assert.False(filter.Process(source.VirtualKey, true, false, HotkeyModifiers.None, Send));
        }
        Assert.Equal(12, sent);
        Assert.False(filter.Process(0x45, false, false, HotkeyModifiers.Windows, Send));
        Assert.False(filter.Process(0x5B, false, false, HotkeyModifiers.None, Send));
    }

    [Fact]
    public void KeyHeldBeforeWinRetainsItsNativeRepeatAndRelease()
    {
        var filter = new ShortcutTranslationFilter();
        Assert.False(filter.Process(0x51, false, false, HotkeyModifiers.None, _ => throw new Exception()));
        Assert.False(filter.Process(0x51, false, false, HotkeyModifiers.Windows, _ => throw new Exception(), alreadyDown: true));
        Assert.False(filter.Process(0x51, true, false, HotkeyModifiers.Windows, _ => throw new Exception(), alreadyDown: true));
    }

    [Fact]
    public void SyntheticTargetCannotRecurseAndFailedSendStillSuppressesSource()
    {
        var filter = new ShortcutTranslationFilter();
        Assert.True(filter.Process(0x51, false, false, HotkeyModifiers.Windows, _ => false));
        Assert.True(filter.Process(0x51, true, false, HotkeyModifiers.None, _ => throw new Exception()));
        foreach (var mapping in MacStyleShortcuts.All)
            foreach (var input in ShortcutInputSender.CreateSequence(mapping, new ushort[] { 0x5B }))
                Assert.False(filter.Process(input.Key, input.Up, true, HotkeyModifiers.Windows, _ => throw new Exception()));
    }
}
