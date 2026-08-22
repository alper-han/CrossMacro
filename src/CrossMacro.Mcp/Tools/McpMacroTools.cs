namespace CrossMacro.Mcp.Tools;

public sealed class McpMacroTools(
    IMacroExecutionService macroExecutionService,
    McpToolAuthorization authorization,
    McpPathAuthorizer pathAuthorizer)
{
    private const int MaximumMacroListCount = 100;

    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;
    private readonly McpToolAuthorization _authorization = authorization;
    private readonly McpPathAuthorizer _pathAuthorizer = pathAuthorizer;

    [McpServerTool(Name = "macro.list", Title = "List macro files", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpMacroListResult))]
    [Description("Lists up to 100 regular .macro files directly within an absolute directory path.")]
    public CallToolResult ListMacros(string directoryPath, CancellationToken cancellationToken)
    {
        var capability = _authorization.Require(McpCapability.MacroRead);
        if (capability is not null)
        {
            return CreateListResult(directoryPath, [], isTruncated: false, capability);
        }

        if (!_pathAuthorizer.TryNormalizeDirectoryPath(directoryPath, out var normalizedDirectoryPath, out var error))
        {
            return CreateListResult(directoryPath, [], isTruncated: false, error);
        }

        var macros = new List<McpMacroFile>(MaximumMacroListCount + 1);
        try
        {
            foreach (var filePath in Directory.EnumerateFiles(normalizedDirectoryPath, "*.macro", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Attributes.HasFlag(FileAttributes.Directory) || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                macros.Add(new McpMacroFile(fileInfo.FullName, fileInfo.Name, fileInfo.Length, new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)));
                if (macros.Count > MaximumMacroListCount)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return CreateListResult(normalizedDirectoryPath, [], isTruncated: false, McpToolOutcomeMapper.FileError("Macro directory could not be listed."));
        }

        var ordered = macros.OrderBy(static macro => macro.FileName, StringComparer.Ordinal).Take(MaximumMacroListCount).ToArray();
        return CreateListResult(normalizedDirectoryPath, ordered, macros.Count > MaximumMacroListCount, McpToolOutcomeMapper.Success("Macro files listed."));
    }

    [McpServerTool(Name = "macro.inspect", Title = "Inspect a macro", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpMacroInspectResult))]
    [Description("Reads macro metadata and validation diagnostics without returning macro events, script steps, or embedded assets.")]
    public async Task<CallToolResult> InspectMacroAsync(string macroPath, CancellationToken cancellationToken)
    {
        var capability = _authorization.Require(McpCapability.MacroRead);
        if (capability is not null)
        {
            return CreateInspectResult(capability, macro: null);
        }

        if (!_pathAuthorizer.TryNormalizeMacroPath(macroPath, out var normalizedMacroPath, out var error))
        {
            return CreateInspectResult(error, macro: null);
        }

        var result = await _macroExecutionService.GetInfoAsync(normalizedMacroPath, cancellationToken).ConfigureAwait(false);
        return CreateInspectResult(McpToolOutcomeMapper.FromMacroResult(result), ToMacroInfo(result.Data));
    }

    [McpServerTool(Name = "macro.validate", Title = "Validate a macro", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpMacroValidateResult))]
    [Description("Validates a macro without playing it.")]
    public async Task<CallToolResult> ValidateMacroAsync(string macroPath, CancellationToken cancellationToken)
    {
        var capability = _authorization.Require(McpCapability.MacroRead);
        if (capability is not null)
        {
            return CreateValidateResult(capability, macro: null);
        }

        if (!_pathAuthorizer.TryNormalizeMacroPath(macroPath, out var normalizedMacroPath, out var error))
        {
            return CreateValidateResult(error, macro: null);
        }

        var result = await _macroExecutionService.ValidateAsync(normalizedMacroPath, cancellationToken).ConfigureAwait(false);
        return CreateValidateResult(McpToolOutcomeMapper.FromMacroResult(result), ToMacroSummary(result.Data));
    }

    private static McpMacroInfo? ToMacroInfo(object? data)
    {
        if (data is not MacroInfoData macro)
        {
            return null;
        }

        return new McpMacroInfo(
            macro.MacroPath,
            macro.MacroName,
            macro.CreatedAt,
            macro.EventCount,
            macro.TotalDurationMs,
            macro.CoordinateMode,
            macro.IsAbsoluteCoordinates,
            macro.SkipInitialZeroZero,
            macro.TrailingDelayMicroseconds,
            macro.TrailingDelayMs,
            macro.HasTrailingRandomDelay,
            macro.TrailingDelayMinMs,
            macro.TrailingDelayMaxMs,
            new McpMacroEventBreakdown(
                macro.EventBreakdown.MouseMove,
                macro.EventBreakdown.ButtonPress,
                macro.EventBreakdown.ButtonRelease,
                macro.EventBreakdown.Click,
                macro.EventBreakdown.KeyPress,
                macro.EventBreakdown.KeyRelease));
    }

    private static McpMacroSummary? ToMacroSummary(object? data) =>
        data is MacroSummaryData macro
            ? new McpMacroSummary(macro.MacroPath, macro.MacroName, macro.EventCount, macro.TotalDurationMs, macro.CoordinateMode, macro.IsAbsoluteCoordinates)
            : null;

    private static CallToolResult CreateListResult(string directoryPath, IReadOnlyList<McpMacroFile> macros, bool isTruncated, McpToolOutcome outcome) =>
        CreateResult(new McpMacroListResult(directoryPath, macros, isTruncated, outcome));

    private static CallToolResult CreateInspectResult(McpToolOutcome outcome, McpMacroInfo? macro) =>
        CreateResult(new McpMacroInspectResult(outcome, macro));

    private static CallToolResult CreateValidateResult(McpToolOutcome outcome, McpMacroSummary? macro) =>
        CreateResult(new McpMacroValidateResult(outcome, macro));

    private static CallToolResult CreateResult(McpMacroListResult result) =>
        CreateResult(result.Outcome, JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpMacroListResult));

    private static CallToolResult CreateResult(McpMacroInspectResult result) =>
        CreateResult(result.Outcome, JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpMacroInspectResult));

    private static CallToolResult CreateResult(McpMacroValidateResult result) =>
        CreateResult(result.Outcome, JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpMacroValidateResult));

    private static CallToolResult CreateResult(McpToolOutcome outcome, JsonElement structuredContent) =>
        new()
        {
            Content = [new TextContentBlock { Text = outcome.Message }],
            StructuredContent = structuredContent,
            IsError = !outcome.Success,
        };
}
