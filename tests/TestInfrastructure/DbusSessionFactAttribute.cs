using System.Runtime.CompilerServices;

namespace CrossMacro.TestInfrastructure;

internal sealed class DbusSessionFactAttribute : FactAttribute
{
    private const string IntegrationEnvironmentVariable = "CROSSMACRO_DBUS_INTEGRATION_TESTS";

    public DbusSessionFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!(OperatingSystem.IsLinux() &&
              string.Equals(
                  Environment.GetEnvironmentVariable(IntegrationEnvironmentVariable),
                  "1",
                  StringComparison.Ordinal) &&
              HasExecutableOnPath("dbus-daemon")))
        {
            Skip = ConditionalSkipMessage.For("Linux + CROSSMACRO_DBUS_INTEGRATION_TESTS=1 + dbus-daemon");
        }
    }

    private static bool HasExecutableOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(Path.Combine(directory, fileName)))
            {
                return true;
            }
        }

        return false;
    }
}
