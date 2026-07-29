
namespace CrossMacro.TestInfrastructure;

internal sealed class DbusSessionFactAttribute : FactAttribute
{
    public DbusSessionFactAttribute()
    {
        if (!(OperatingSystem.IsLinux() &&
              HasExecutableOnPath("dbus-daemon")))
        {
            Skip = ConditionalSkipMessage.For("Linux + dbus-daemon");
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
