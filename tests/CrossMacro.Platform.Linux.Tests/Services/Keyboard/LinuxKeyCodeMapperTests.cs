namespace CrossMacro.Platform.Linux.Tests.Services.Keyboard;

public sealed class LinuxKeyCodeMapperTests
{
    [Fact]
    public void GetKeyCode_WhenNameComesFromTheKeymap_ScansTheKeymapOnlyOnce()
    {
        var xkb = new FakeXkbStateManager { Initialized = true };
        xkb.Names[20 + X11InputCodes.KeycodeOffset] = "z";
        xkb.Names[21 + X11InputCodes.KeycodeOffset] = "y";
        var mapper = new LinuxKeyCodeMapper(xkb);

        Assert.Equal(20, mapper.GetKeyCode("Z"));
        var callsAfterFirstLookup = xkb.Utf8Calls;
        Assert.Equal(21, mapper.GetKeyCode("y"));
        Assert.Equal(20, mapper.GetKeyCode("z"));
        Assert.Equal(-1, mapper.GetKeyCode("NoSuchKey"));

        Assert.True(callsAfterFirstLookup > 0);
        Assert.Equal(callsAfterFirstLookup, xkb.Utf8Calls);
    }

    [Fact]
    public void GetKeyCode_WhenKeymapIsReplaced_ReflectsTheNewLayout()
    {
        var xkb = new FakeXkbStateManager { Initialized = true };
        xkb.Names[20 + X11InputCodes.KeycodeOffset] = "ö";
        var mapper = new LinuxKeyCodeMapper(xkb);
        Assert.Equal(20, mapper.GetKeyCode("Ö"));

        xkb.Names.Clear();
        xkb.Names[30 + X11InputCodes.KeycodeOffset] = "ö";
        xkb.Generation++;

        Assert.Equal(30, mapper.GetKeyCode("ö"));
    }

    [Fact]
    public void GetKeyCode_WhenXkbBecomesInitializedAfterAnEarlierLookup_UsesTheKeymapNames()
    {
        var xkb = new FakeXkbStateManager { Initialized = false };
        xkb.Names[20 + X11InputCodes.KeycodeOffset] = "ö";
        var mapper = new LinuxKeyCodeMapper(xkb);
        Assert.Equal(-1, mapper.GetKeyCode("Ö"));

        xkb.Initialized = true;

        Assert.Equal(20, mapper.GetKeyCode("Ö"));
    }

    [Fact]
    public void GetKeyCode_WhenSeveralKeysShareAName_ReturnsTheLowestKeyCode()
    {
        var xkb = new FakeXkbStateManager { Initialized = true };
        xkb.Names[40 + X11InputCodes.KeycodeOffset] = "ß";
        xkb.Names[35 + X11InputCodes.KeycodeOffset] = "ß";
        var mapper = new LinuxKeyCodeMapper(xkb);

        Assert.Equal(35, mapper.GetKeyCode("ß"));
    }

    private sealed class FakeXkbStateManager : IXkbStateManager
    {
        public bool Initialized { get; set; }

        public int Generation { get; set; }

        public int Utf8Calls { get; private set; }

        public Dictionary<uint, string> Names { get; } = [];

        public bool IsInitialized => Initialized;

        public void Initialize(string? layout) { /* Layout is driven through Generation. */ }

        public string? GetUtf8String(uint keycode)
        {
            Utf8Calls++;
            return Names.GetValueOrDefault(keycode);
        }

        public char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock) => null;

        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c) => null;

        public void Dispose() { /* Nothing to release. */ }
    }
}
