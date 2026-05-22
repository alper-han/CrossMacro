using System.Collections.Generic;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS.Services;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public class KeyMapTests
{
    [Theory]
    [InlineData(InputEventCode.KEY_F1, 0x7A)]
    [InlineData(InputEventCode.KEY_F2, 0x78)]
    [InlineData(InputEventCode.KEY_F3, 0x63)]
    [InlineData(InputEventCode.KEY_F4, 0x76)]
    [InlineData(InputEventCode.KEY_F5, 0x60)]
    [InlineData(InputEventCode.KEY_F6, 0x61)]
    [InlineData(InputEventCode.KEY_F7, 0x62)]
    [InlineData(InputEventCode.KEY_F8, 0x64)]
    [InlineData(InputEventCode.KEY_F9, 0x65)]
    [InlineData(InputEventCode.KEY_F10, 0x6D)]
    [InlineData(InputEventCode.KEY_F11, 0x67)]
    [InlineData(InputEventCode.KEY_F12, 0x6F)]
    [InlineData(InputEventCode.KEY_F13, 0x69)]
    [InlineData(InputEventCode.KEY_F14, 0x6B)]
    [InlineData(InputEventCode.KEY_F15, 0x71)]
    [InlineData(InputEventCode.KEY_F16, 0x6A)]
    [InlineData(InputEventCode.KEY_F17, 0x40)]
    [InlineData(InputEventCode.KEY_F18, 0x4F)]
    [InlineData(InputEventCode.KEY_F19, 0x50)]
    [InlineData(InputEventCode.KEY_F20, 0x5A)]
    public void FunctionKeysF1ThroughF20_WhenMapped_RoundTripAuditedMacVirtualKeys(int inputEventCode, int macKeyCode)
    {
        Assert.Equal((ushort)macKeyCode, KeyMap.ToMacKey(inputEventCode));

        bool mapped = KeyMap.TryFromMacKey((ushort)macKeyCode, out var reversedInputEventCode);

        Assert.True(mapped);
        Assert.Equal(inputEventCode, reversedInputEventCode);
        Assert.Equal(inputEventCode, KeyMap.FromMacKey((ushort)macKeyCode));
    }

    [Theory]
    [InlineData(InputEventCode.KEY_KP0, 0x52)]
    [InlineData(InputEventCode.KEY_KP1, 0x53)]
    [InlineData(InputEventCode.KEY_KP2, 0x54)]
    [InlineData(InputEventCode.KEY_KP3, 0x55)]
    [InlineData(InputEventCode.KEY_KP4, 0x56)]
    [InlineData(InputEventCode.KEY_KP5, 0x57)]
    [InlineData(InputEventCode.KEY_KP6, 0x58)]
    [InlineData(InputEventCode.KEY_KP7, 0x59)]
    [InlineData(InputEventCode.KEY_KP8, 0x5B)]
    [InlineData(InputEventCode.KEY_KP9, 0x5C)]
    [InlineData(InputEventCode.KEY_KPDOT, 0x41)]
    [InlineData(InputEventCode.KEY_KPASTERISK, 0x43)]
    [InlineData(InputEventCode.KEY_KPPLUS, 0x45)]
    [InlineData(InputEventCode.KEY_KPMINUS, 0x4E)]
    [InlineData(InputEventCode.KEY_KPENTER, 0x4C)]
    [InlineData(InputEventCode.KEY_KPSLASH, 0x4B)]
    [InlineData(InputEventCode.KEY_KPEQUAL, 0x51)]
    [InlineData(InputEventCode.KEY_NUMLOCK, 0x47)]
    public void NumpadKeys_WhenMapped_RoundTripAuditedMacVirtualKeys(int inputEventCode, int macKeyCode)
    {
        Assert.Equal((ushort)macKeyCode, KeyMap.ToMacKey(inputEventCode));

        bool mapped = KeyMap.TryFromMacKey((ushort)macKeyCode, out var reversedInputEventCode);

        Assert.True(mapped);
        Assert.Equal(inputEventCode, reversedInputEventCode);
        Assert.Equal(inputEventCode, KeyMap.FromMacKey((ushort)macKeyCode));
    }

    [Theory]
    [InlineData(InputEventCode.KEY_HOME, 0x73)]
    [InlineData(InputEventCode.KEY_PAGEUP, 0x74)]
    [InlineData(InputEventCode.KEY_DELETE, 0x75)]
    [InlineData(InputEventCode.KEY_END, 0x77)]
    [InlineData(InputEventCode.KEY_PAGEDOWN, 0x79)]
    [InlineData(InputEventCode.KEY_LEFT, 0x7B)]
    [InlineData(InputEventCode.KEY_RIGHT, 0x7C)]
    [InlineData(InputEventCode.KEY_DOWN, 0x7D)]
    [InlineData(InputEventCode.KEY_UP, 0x7E)]
    public void NavigationAndEditingKeys_WhenMapped_RoundTripAuditedMacVirtualKeys(int inputEventCode, int macKeyCode)
    {
        Assert.Equal((ushort)macKeyCode, KeyMap.ToMacKey(inputEventCode));

        bool mapped = KeyMap.TryFromMacKey((ushort)macKeyCode, out var reversedInputEventCode);

        Assert.True(mapped);
        Assert.Equal(inputEventCode, reversedInputEventCode);
        Assert.Equal(inputEventCode, KeyMap.FromMacKey((ushort)macKeyCode));
    }

    [Theory]
    [InlineData(InputEventCode.KEY_102ND, 0x0A)]
    [InlineData(InputEventCode.KEY_YEN, 0x5D)]
    [InlineData(InputEventCode.KEY_KPJPCOMMA, 0x5F)]
    public void InternationalAndIsoKeys_WhenMapped_RoundTripAuditedMacVirtualKeys(int inputEventCode, int macKeyCode)
    {
        Assert.Equal((ushort)macKeyCode, KeyMap.ToMacKey(inputEventCode));

        bool mapped = KeyMap.TryFromMacKey((ushort)macKeyCode, out var reversedInputEventCode);

        Assert.True(mapped);
        Assert.Equal(inputEventCode, reversedInputEventCode);
        Assert.Equal(inputEventCode, KeyMap.FromMacKey((ushort)macKeyCode));
    }

    [Theory]
    [InlineData(InputEventCode.KEY_ZENKAKUHANKAKU)]
    [InlineData(InputEventCode.KEY_RO)]
    [InlineData(InputEventCode.KEY_KATAKANA)]
    [InlineData(InputEventCode.KEY_HIRAGANA)]
    [InlineData(InputEventCode.KEY_HENKAN)]
    [InlineData(InputEventCode.KEY_KATAKANAHIRAGANA)]
    [InlineData(InputEventCode.KEY_MUHENKAN)]
    [InlineData(InputEventCode.KEY_KPCOMMA)]
    public void UnsupportedInternationalAndImeKeys_WhenMappedToMacKey_AreUnsupported(int inputEventCode)
    {
        Assert.Equal(0xFFFF, KeyMap.ToMacKey(inputEventCode));
    }

    [Theory]
    [InlineData(0x5E)]
    [InlineData(0x66)]
    [InlineData(0x68)]
    public void UnsupportedInternationalAndImeNativeKeys_WhenReversed_ReturnNoMatch(int macKeyCode)
    {
        bool mapped = KeyMap.TryFromMacKey((ushort)macKeyCode, out var inputEventCode);

        Assert.False(mapped);
        Assert.Equal(default, inputEventCode);
        Assert.Throws<KeyNotFoundException>(() => KeyMap.FromMacKey((ushort)macKeyCode));
    }

    [Fact]
    public void UnsupportedInternationalAndImeKeys_WhenAnyNativeKeyIsReversed_AreNeverProduced()
    {
        var unsupportedInternationalKeys = new HashSet<int>
        {
            InputEventCode.KEY_ZENKAKUHANKAKU,
            InputEventCode.KEY_RO,
            InputEventCode.KEY_KATAKANA,
            InputEventCode.KEY_HIRAGANA,
            InputEventCode.KEY_HENKAN,
            InputEventCode.KEY_KATAKANAHIRAGANA,
            InputEventCode.KEY_MUHENKAN,
            InputEventCode.KEY_KPCOMMA,
        };

        for (var macKeyCode = 0; macKeyCode <= ushort.MaxValue; macKeyCode++)
        {
            bool mapped = KeyMap.TryFromMacKey((ushort)macKeyCode, out var inputEventCode);

            if (mapped)
            {
                Assert.DoesNotContain(inputEventCode, unsupportedInternationalKeys);
            }
        }
    }

    [Theory]
    [InlineData(InputEventCode.KEY_F21)]
    [InlineData(InputEventCode.KEY_F22)]
    [InlineData(InputEventCode.KEY_F23)]
    [InlineData(InputEventCode.KEY_F24)]
    public void FunctionKeysF21ThroughF24_WhenMappedToMacKey_AreUnsupported(int inputEventCode)
    {
        Assert.Equal(0xFFFF, KeyMap.ToMacKey(inputEventCode));
    }

    [Fact]
    public void FunctionKeysF21ThroughF24_WhenAnyNativeKeyIsReversed_AreNeverProduced()
    {
        var unsupportedFunctionKeys = new HashSet<int>
        {
            InputEventCode.KEY_F21,
            InputEventCode.KEY_F22,
            InputEventCode.KEY_F23,
            InputEventCode.KEY_F24,
        };

        for (var macKeyCode = 0; macKeyCode <= ushort.MaxValue; macKeyCode++)
        {
            bool mapped = KeyMap.TryFromMacKey((ushort)macKeyCode, out var inputEventCode);

            if (mapped)
            {
                Assert.DoesNotContain(inputEventCode, unsupportedFunctionKeys);
            }
        }
    }

    [Theory]
    [InlineData(0x53, InputEventCode.KEY_KP1, InputEventCode.KEY_F21)]
    [InlineData(0x54, InputEventCode.KEY_KP2, InputEventCode.KEY_F22)]
    [InlineData(0x55, InputEventCode.KEY_KP3, InputEventCode.KEY_F23)]
    [InlineData(0x52, InputEventCode.KEY_KP0, InputEventCode.KEY_F24)]
    public void FunctionKeysF21ThroughF24_WhenNearbyNumpadKeysAreReversed_DoNotSilentlyAlias(int macKeyCode, int expectedInputEventCode, int unsupportedInputEventCode)
    {
        bool mapped = KeyMap.TryFromMacKey((ushort)macKeyCode, out var reversedInputEventCode);

        Assert.True(mapped);
        Assert.Equal(expectedInputEventCode, reversedInputEventCode);
        Assert.NotEqual(unsupportedInputEventCode, reversedInputEventCode);
    }

    [Fact]
    public void TryFromMacKey_WhenNativeKeyIsUnknown_ReturnsNoMatch()
    {
        bool mapped = KeyMap.TryFromMacKey(0xFFFF, out var inputEventCode);

        Assert.False(mapped);
        Assert.Equal(default, inputEventCode);
    }

    [Fact]
    public void FromMacKey_WhenNativeKeyIsUnknown_ThrowsInsteadOfReturningZero()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => KeyMap.FromMacKey(0xFFFF));

        Assert.Contains("0xFFFF", exception.Message);
    }

    [Fact]
    public void FromMacKey_WhenNativeKeyIsKnown_PreservesExistingMapping()
    {
        bool mapped = KeyMap.TryFromMacKey(0x00, out var inputEventCode);

        Assert.True(mapped);
        Assert.Equal(InputEventCode.KEY_A, inputEventCode);
        Assert.Equal(InputEventCode.KEY_A, KeyMap.FromMacKey(0x00));
    }

    [Fact]
    public void HelpAndInsert_WhenMappedToMacKey_AreDocumentedLossyAliases()
    {
        Assert.Equal(0x72, KeyMap.ToMacKey(InputEventCode.KEY_HELP));
        Assert.Equal(0x72, KeyMap.ToMacKey(InputEventCode.KEY_INSERT));
    }

    [Fact]
    public void HelpAndInsert_WhenReversedFromNativeHelp_ResolveDeterministicallyToHelp()
    {
        bool mapped = KeyMap.TryFromMacKey(0x72, out var inputEventCode);

        Assert.True(mapped);
        Assert.Equal(InputEventCode.KEY_HELP, inputEventCode);
        Assert.NotEqual(InputEventCode.KEY_INSERT, inputEventCode);
    }
}
