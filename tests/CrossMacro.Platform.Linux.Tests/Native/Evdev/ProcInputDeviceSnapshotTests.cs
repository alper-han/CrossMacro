namespace CrossMacro.Platform.Linux.Tests.Native.Evdev;

public sealed class ProcInputDeviceSnapshotTests
{
    [Theory]
    [InlineData("event1", "USB Mouse", false)]
    [InlineData("event10", "USB Mouse", false)]
    [InlineData("event100", "USB Mouse", false)]
    [InlineData("event10", "USB Mouse Pro", true)]
    public void HasHandler_RequiresTheCompleteEventTokenAndDeviceName(string eventName, string name, bool expected)
    {
        var snapshot = ProcInputDeviceSnapshot.Parse("N: Name=\"USB Mouse Pro\"\nH: Handlers=mouse0 event10\n");

        Assert.Equal(expected, snapshot.HasHandler("/dev/input/" + eventName, name, InputDeviceHandlers.Mouse));
    }

    [Theory]
    [InlineData("mouse0", true, false)]
    [InlineData("mouse123", true, false)]
    [InlineData("mouse", false, false)]
    [InlineData("mouse0extra", false, false)]
    [InlineData("notmouse0", false, false)]
    [InlineData("kbd", false, true)]
    [InlineData("notkbd", false, false)]
    [InlineData("kbdextra", false, false)]
    public void HasHandler_RecognizesOnlyCompleteTypedHandlers(string handler, bool mouse, bool keyboard)
    {
        var snapshot = ProcInputDeviceSnapshot.Parse("N: Name=\"Device\"\nH: Handlers=" + handler + " event1\n");

        Assert.Equal(mouse, snapshot.HasHandler("/dev/input/event1", "Device", InputDeviceHandlers.Mouse));
        Assert.Equal(keyboard, snapshot.HasHandler("/dev/input/event1", "Device", InputDeviceHandlers.Keyboard));
    }

    [Fact]
    public void Parse_SeparatesSameNamedDevicesAndSupportsWhitespaceAndFinalUnterminatedBlock()
    {
        var snapshot = ProcInputDeviceSnapshot.Parse(
            "N: Name=\"Device\"\r\nH: Handlers=  mouse0\tevent1  \r\n \t\r\n" +
            "H: Handlers=sysrq\tkbd event10\nN: Name=\"Device\"");

        Assert.True(snapshot.HasHandler("/dev/input/event1", "Device", InputDeviceHandlers.Mouse));
        Assert.False(snapshot.HasHandler("/dev/input/event1", "Device", InputDeviceHandlers.Keyboard));
        Assert.True(snapshot.HasHandler("/dev/input/event10", "Device", InputDeviceHandlers.Keyboard));
        Assert.False(snapshot.HasHandler("/dev/input/event10", "Device", InputDeviceHandlers.Mouse));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("H: Handlers=mouse0 event1\n")]
    [InlineData("N: Name=\"Device\"\n")]
    [InlineData("N: Name=Device\nH: Handlers=mouse0 event1\n")]
    [InlineData("N: Name=\"Device\"\nH: Handlers=mouse0 event1extra\n")]
    public void Parse_MissingOrMalformedIdentityDoesNotInventAHandler(string? content)
    {
        var snapshot = ProcInputDeviceSnapshot.Parse(content);

        Assert.False(snapshot.HasHandler("/dev/input/event1", "Device", InputDeviceHandlers.Mouse));
    }
}
