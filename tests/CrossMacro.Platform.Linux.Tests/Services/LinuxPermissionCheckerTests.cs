namespace CrossMacro.Platform.Linux.Tests.Services;

using System;
using CrossMacro.Platform.Linux.Services;

public class LinuxPermissionCheckerTests
{
    [Fact]
    public void IsSupported_ShouldAlwaysBeTrue()
    {
        var checker = new LinuxPermissionChecker();

        Assert.True(checker.IsSupported);
    }

    [Fact]
    public void IsAccessibilityTrusted_ShouldDelegateToUInputAccessCheck()
    {
        var checker = new LinuxPermissionChecker();

        var accessibilityTrusted = checker.IsAccessibilityTrusted();
        var directCheck = checker.CheckUInputAccess();

        Assert.Equal(directCheck, accessibilityTrusted);
    }

    [Fact]
    public void OpenAccessibilitySettings_WhenXdgOpenUnavailable_ShouldNotThrow()
    {
        using var pathScope = new EnvironmentVariableScope("PATH", "/nonexistent");
        var checker = new LinuxPermissionChecker();

        var ex = Record.Exception(checker.OpenAccessibilitySettings);

        Assert.Null(ex);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
