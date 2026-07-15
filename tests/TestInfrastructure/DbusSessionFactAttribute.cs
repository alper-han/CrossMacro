
namespace CrossMacro.TestInfrastructure;

public sealed class DbusSessionFactAttribute : ConditionalFactAttribute
{
    public DbusSessionFactAttribute()
        : base(
            () => OperatingSystem.IsLinux() &&
                  HasExecutableOnPath("dbus-daemon"),
            "Linux + dbus-daemon")
    {
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
