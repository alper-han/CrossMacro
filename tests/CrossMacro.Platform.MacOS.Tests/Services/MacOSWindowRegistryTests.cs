namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSWindowRegistryTests
{
    [Fact]
    public void Register_UsesRetainedElementIdentityInsteadOfMutableMetadata()
    {
        var retained = new List<IntPtr>();
        var released = new List<IntPtr>();
        using var registry = new MacOSWindowRegistry(
            element =>
            {
                retained.Add(element);
                return element;
            },
            element =>
            {
                released.Add(element);
                return true;
            },
            (left, right) => left == right);

        var fallback = MacOSWindowAddress.FromWindow(123, 10, "Title", new ScreenRect(1, 2, 3, 4));
        var firstAddress = registry.Register(new IntPtr(10), fallback);
        var sameAddress = registry.Register(new IntPtr(10), fallback with { Title = "Renamed" });
        var secondAddress = registry.Register(new IntPtr(11), fallback);

        Assert.Equal(firstAddress, sameAddress);
        Assert.False(string.Equals(firstAddress, secondAddress, StringComparison.Ordinal));
        Assert.Equal([new IntPtr(10), new IntPtr(11)], retained);
        Assert.StartsWith("ax2-", firstAddress, StringComparison.Ordinal);

        registry.Remove(firstAddress);

        Assert.False(registry.TryUse(firstAddress, static (_, _) => true, out _));
        Assert.True(registry.WasIssuedByThisRegistry(firstAddress));
        Assert.Equal([new IntPtr(10)], released);
    }

    [Fact]
    public void PruneExcept_ReleasesWindowsThatAreNoLongerVisible()
    {
        var released = new List<IntPtr>();
        using var registry = new MacOSWindowRegistry(
            static element => element,
            element =>
            {
                released.Add(element);
                return true;
            },
            (left, right) => left == right);
        var fallback = MacOSWindowAddress.FromWindow(123, 10, "Title", new ScreenRect(1, 2, 3, 4));
        var keep = registry.Register(new IntPtr(10), fallback);
        var remove = registry.Register(new IntPtr(11), fallback);

        registry.PruneExcept(new HashSet<string>([keep], StringComparer.Ordinal));

        Assert.True(registry.TryUse(keep, static (_, _) => true, out var keepResult));
        Assert.True(keepResult);
        Assert.False(registry.TryUse(remove, static (_, _) => true, out _));
        Assert.True(registry.WasIssuedByThisRegistry(remove));
        Assert.Equal([new IntPtr(11)], released);
    }
}
