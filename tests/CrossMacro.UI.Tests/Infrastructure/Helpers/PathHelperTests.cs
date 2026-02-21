using System;
using System.IO;
using CrossMacro.Core;
using CrossMacro.Infrastructure.Helpers;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.UI.Tests.Infrastructure.Helpers;

public class PathHelperTests
{
    [Fact]
    public void GetConfigDirectory_ReturnsPathContainingAppIdentifier()
    {
        // Act
        var result = PathHelper.GetConfigDirectory();

        // Assert
        Assert.Contains(AppConstants.AppIdentifier, result);
    }

    [Fact]
    public void GetConfigFilePath_CombinesDirectoryAndFileName()
    {
        // Arrange
        string fileName = "test.json";
        var configDir = PathHelper.GetConfigDirectory();
        var expected = Path.Combine(configDir, fileName);

        // Act
        var result = PathHelper.GetConfigFilePath(fileName);

        // Assert
        Assert.Equal(expected, result);
    }
    
    [LinuxFact]
    public void GetConfigDirectory_RespectsXDGConfigHome_WhenSetOnLinux()
    {
        // Note: Environment variables are process-wide. 
        // We set it, test, and perform cleanup in a try/finally block.
        // Parallel execution might be an issue, but xUnit runs classes in parallel, methods sequentially by default.
        // Use a unique value to be sure.
        
        string? originalValue = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string testPath = Path.Combine(Path.GetTempPath(), "CrossMacroTestXDG");
        
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", testPath);
            
            // Act
            var result = PathHelper.GetConfigDirectory();
            
            // Assert
            // Implementation: Path.Combine(xdgConfigHome, AppConstants.AppIdentifier)
            var expected = Path.Combine(testPath, AppConstants.AppIdentifier);
            Assert.Equal(expected, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalValue);
        }
    }
}
