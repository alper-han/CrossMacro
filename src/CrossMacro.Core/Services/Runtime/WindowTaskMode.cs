namespace CrossMacro.Core.Services.Runtime;

/// <summary>Explicit macOS editor/scheduler host for external window executors.</summary>
public static class WindowTaskMode
{
    public static bool Enabled => OperatingSystem.IsMacOS()
        && string.Equals(Environment.GetEnvironmentVariable("CROSSMACRO_WINDOW_TASKS_ONLY"), "1", StringComparison.Ordinal);
}
