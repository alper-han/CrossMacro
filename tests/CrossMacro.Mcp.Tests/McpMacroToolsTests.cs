namespace CrossMacro.Mcp.Tests;

public sealed class McpMacroToolsTests
{
    [Fact]
    public void ListMacros_ShouldReturnSortedRegularMacroFilesAndIgnoreOtherEntries()
    {
        var directory = McpTestData.CreateTemporaryDirectory();
        try
        {
            var alphaPath = Path.Combine(directory, "alpha.macro");
            var betaPath = Path.Combine(directory, "beta.macro");
            File.WriteAllText(alphaPath, "alpha");
            File.WriteAllText(betaPath, "beta");
            File.WriteAllText(Path.Combine(directory, "ignored.txt"), "ignored");

            var result = McpToolTestFactory.CreateMacroTools().ListMacros(directory, CancellationToken.None);

            Assert.NotEqual(true, result.IsError);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.True(structured.GetProperty("outcome").GetProperty("success").GetBoolean());
            Assert.Equal(Path.GetFullPath(directory), structured.GetProperty("directoryPath").GetString());
            Assert.False(structured.GetProperty("isTruncated").GetBoolean());
            Assert.Equal(
                ["alpha.macro", "beta.macro"],
                structured.GetProperty("macros").EnumerateArray().Select(static macro => macro.GetProperty("fileName").GetString()),
                StringComparer.Ordinal);
            Assert.Equal(new FileInfo(alphaPath).Length, structured.GetProperty("macros")[0].GetProperty("sizeBytes").GetInt64());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    public void ListMacros_ShouldReturnStructuredInvalidArgumentsForInvalidDirectoryPaths(string directoryPath)
    {
        var result = McpToolTestFactory.CreateMacroTools().ListMacros(directoryPath, CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal((int)CliExitCode.InvalidArguments, structured.GetProperty("outcome").GetProperty("exitCode").GetInt32());
        Assert.Equal("invalid_arguments", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void ListMacros_ShouldReturnStructuredFileErrorForMissingDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = McpToolTestFactory.CreateMacroTools().ListMacros(directoryPath, CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal((int)CliExitCode.FileError, structured.GetProperty("outcome").GetProperty("exitCode").GetInt32());
        Assert.Equal("file_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task InspectMacroAsync_ShouldReturnMacroInfoAndPreserveValidationWarnings()
    {
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            var service = new TestMacroExecutionService
            {
                InfoResult = new MacroExecutionResult
                {
                    Success = true,
                    ExitCode = CliExitCode.Success,
                    Message = "Macro info loaded.",
                    Warnings = ["Position provider unavailable."],
                    Data = new MacroInfoData(
                        macroPath,
                        "Demo",
                        DateTime.UnixEpoch,
                        4,
                        300,
                        "relative",
                        IsAbsoluteCoordinates: false,
                        SkipInitialZeroZero: true,
                        TrailingDelayMicroseconds: 50,
                        TrailingDelayMs: 0,
                        HasTrailingRandomDelay: false,
                        TrailingDelayMinMs: 0,
                        TrailingDelayMaxMs: 0,
                        new MacroEventBreakdownData(1, 1, 0, 0, 1, 1)),
                },
            };
            var tools = McpToolTestFactory.CreateMacroTools(service);

            var result = await tools.InspectMacroAsync(macroPath, CancellationToken.None);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal("Macro info loaded.", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.Equal("Demo", structured.GetProperty("macro").GetProperty("macroName").GetString());
            Assert.Equal(1, structured.GetProperty("macro").GetProperty("eventBreakdown").GetProperty("mouseMove").GetInt32());
            Assert.Equal("Position provider unavailable.", structured.GetProperty("outcome").GetProperty("warnings")[0].GetString());
            Assert.Equal(1, service.GetInfoCallCount);
            Assert.Equal(macroPath, service.LastMacroPath);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task InspectMacroAsync_ShouldReturnToolErrorForInvalidMacroPathWithoutInvokingCliService()
    {
        var service = new TestMacroExecutionService();
        var tools = McpToolTestFactory.CreateMacroTools(service);

        var result = await tools.InspectMacroAsync("relative.macro", CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("invalid_arguments", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, service.GetInfoCallCount);
    }

    [Fact]
    public async Task ValidateMacroAsync_ShouldReturnToolErrorAndCliValidationResult()
    {
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            var service = new TestMacroExecutionService
            {
                ValidationResult = new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.ValidationError,
                    Message = "Macro validation failed.",
                    Errors = ["Macro is empty."],
                    Warnings = ["Position provider unavailable."],
                    Data = new MacroValidationData(macroPath, 0),
                },
            };
            var tools = McpToolTestFactory.CreateMacroTools(service);

            var result = await tools.ValidateMacroAsync(macroPath, CancellationToken.None);

            Assert.Equal(true, result.IsError);
            Assert.Equal("Macro validation failed.", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.Equal(4, structured.GetProperty("outcome").GetProperty("exitCode").GetInt32());
            Assert.Equal("validation_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.Equal(JsonValueKind.Null, structured.GetProperty("macro").ValueKind);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }
}
