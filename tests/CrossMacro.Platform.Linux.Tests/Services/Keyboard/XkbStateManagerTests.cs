namespace CrossMacro.Platform.Linux.Tests.Services.Keyboard;

public sealed class XkbStateManagerTests
{
    [LinuxTheory]
    [InlineData(null, 'i')]
    [InlineData("", 'i')]
    [InlineData(" ", 'i')]
    [InlineData("\t", 'i')]
    [InlineData("us", 'i')]
    [InlineData("tr", 'ı')]
    public void Initialize_UsesUsForMissingLayout_AndPreservesDetectedLayout(string? layout, char expected)
    {
        Assert.SkipUnless(NativeLibrary.TryLoad("libxkbcommon.so.0", out var library), "The native XKB library is required.");
        NativeLibrary.Free(library);

        using var manager = new XkbStateManager();
        manager.Initialize(layout);

        Assert.Equal(expected, manager.GetCharFromKeyCode(InputEventCode.KEY_I, shift: false, altGr: false, capsLock: false));
    }
}
