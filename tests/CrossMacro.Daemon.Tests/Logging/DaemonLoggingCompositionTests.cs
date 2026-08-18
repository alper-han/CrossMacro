namespace CrossMacro.Daemon.Tests.Logging;

public sealed class DaemonLoggingCompositionTests
{
    [Fact]
    public void DaemonProjectUsesCoreSerilogOnly()
    {
        var project = File.ReadAllText(FindRepositoryFile("src/CrossMacro.Daemon/CrossMacro.Daemon.csproj"));

        Assert.Contains("PackageReference Include=\"Serilog\"", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Serilog.Sinks", project, StringComparison.Ordinal);
        Assert.DoesNotContain("CrossMacro.Infrastructure", project, StringComparison.Ordinal);
        Assert.DoesNotContain("CrossMacro.Platform.Linux\\CrossMacro.Platform.Linux.csproj", project, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeProjectHasNoPackageReferences()
    {
        var project = File.ReadAllText(FindRepositoryFile("src/CrossMacro.Platform.Linux.Native/CrossMacro.Platform.Linux.Native.csproj"));

        Assert.DoesNotContain("PackageReference", project, StringComparison.Ordinal);
        Assert.Contains("CrossMacro.Core/CrossMacro.Core.csproj", project, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreConsoleLoggerRendersPropertiesAndExceptionsAndHonorsRuntimeLevel()
    {
        var originalOut = Console.Out;
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetOut(output);

        try
        {
            DaemonLoggerSetup.Initialize("Debug");
            Serilog.Log.Information("daemon event {Value}", 42);
            Serilog.Log.Error(new InvalidOperationException("boom"), "daemon failure");
            DaemonLoggerSetup.SetLogLevel("Information");
            Serilog.Log.Debug("hidden debug event");
            Serilog.Log.Information("visible information event");
            Serilog.Log.CloseAndFlush();

            var rendered = output.ToString();
            Assert.Contains("daemon event 42", rendered, StringComparison.Ordinal);
            Assert.Contains("daemon failure", rendered, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException: boom", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain("hidden debug event", rendered, StringComparison.Ordinal);
            Assert.Contains("visible information event", rendered, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
