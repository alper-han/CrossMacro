namespace CrossMacro.Mcp.Services;

public sealed class McpRequestGuard(
    IMcpCapabilityPolicy capabilityPolicy,
    IApprovalService approvalService,
    IMcpAuditStore auditStore,
    ISettingsService settingsService,
    TimeProvider timeProvider)
{
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;
    private readonly IApprovalService _approvalService = approvalService;
    private readonly IMcpAuditStore _auditStore = auditStore;
    private readonly ISettingsService _settingsService = settingsService;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async ValueTask<CallToolResult> InvokeAsync(
        string toolName,
        Func<ValueTask<CallToolResult>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(next);

        var definition = CrossMacroMcpToolCatalog.V1.FirstOrDefault(
            tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal));
        if (definition is null)
        {
            RecordUnknownTool();
            return CreateErrorResult(McpToolOutcomeMapper.ToolNotAllowed());
        }

        var approval = "not_required";
        if (definition.Access is McpToolAccess.Effectful && !IsCapabilityAllowed(definition))
        {
            var denied = _capabilityPolicy.Require(definition.Capabilities[0]);
            Record(definition, "not_requested", "denied", operationId: null);
            return CreateErrorResult(denied);
        }

        if (definition.Access is McpToolAccess.Effectful)
        {
            var timeoutSeconds = McpSecuritySettings.NormalizeApprovalTimeoutSeconds(
                _settingsService.Current.McpSecurity?.ApprovalTimeoutSeconds
                ?? McpSecuritySettings.DefaultApprovalTimeoutSeconds);
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            ApprovalResult approvalResult;
            using var timeoutCancellation = new CancellationTokenSource(timeout, _timeProvider);
            using var approvalCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);
            try
            {
                approvalResult = await _approvalService.RequestAsync(
                        new ApprovalRequest(
                            definition.Name,
                            definition.Description,
                            timeout,
                            TargetSummary: GetSafeTargetSummary(definition),
                            CapabilityNames: definition.Capabilities
                                .Select(static capability => capability.ToString())
                                .ToArray()),
                        approvalCancellation.Token)
                    .WaitAsync(approvalCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
            {
                approvalResult = ApprovalResult.TimedOut;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                approval = "unavailable";
                Record(definition, approval, "approval_unavailable", operationId: null);
                Log.Warning(exception, "MCP approval service was unavailable for {ToolName}", definition.Name);
                return CreateErrorResult(McpToolOutcomeMapper.ApprovalUnavailable());
            }
            approval = approvalResult switch
            {
                ApprovalResult.Approved => "approved",
                ApprovalResult.Denied => "denied",
                ApprovalResult.TimedOut => "timed_out",
                _ => "denied",
            };

            if (approvalResult is not ApprovalResult.Approved)
            {
                var denied = approvalResult is ApprovalResult.TimedOut
                    ? McpToolOutcomeMapper.ApprovalTimedOut()
                    : McpToolOutcomeMapper.ApprovalDenied();
                Record(definition, approval, "denied", operationId: null);
                return CreateErrorResult(denied);
            }
        }

        try
        {
            var result = await next().ConfigureAwait(false);
            Record(definition, approval, result.IsError is true ? "failed" : "success", GetOperationId(result));
            return result;
        }
        catch (OperationCanceledException)
        {
            Record(definition, approval, "cancelled", operationId: null);
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Record(definition, approval, "failed", operationId: null);
            throw;
        }
    }

    private void RecordUnknownTool()
    {
        try
        {
            _auditStore.Record(new McpAuditEntry(
                DateTimeOffset.UtcNow,
                "unregistered",
                "unknown",
                "not_requested",
                "denied",
                capabilityNames: []));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log.Warning(exception, "Failed to record MCP audit entry for an unregistered tool.");
        }
    }

    private bool IsCapabilityAllowed(McpToolDefinition definition) =>
        definition.CapabilityRequirement is McpCapabilityRequirement.Any
            ? _capabilityPolicy.IsAnyAllowed([.. definition.Capabilities])
            : definition.Capabilities.All(_capabilityPolicy.IsAllowed);

    private void Record(McpToolDefinition definition, string approval, string result, string? operationId)
    {
        try
        {
            _auditStore.Record(new McpAuditEntry(
                DateTimeOffset.UtcNow,
                definition.Name,
                definition.Access.ToString(),
                approval,
                result,
                operationId,
                definition.Capabilities
                    .Select(static capability => capability.ToString())
                    .ToArray(),
                RuntimeIdentity: "mcp",
                RedactedTarget: GetSafeTargetSummary(definition)));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log.Warning(exception, "Failed to record MCP audit entry for {ToolName}", definition.Name);
        }
    }

    private static string? GetOperationId(CallToolResult result)
    {
        if (result.StructuredContent is not JsonElement structured
            || structured.ValueKind is not JsonValueKind.Object
            || !structured.TryGetProperty("operation", out var operation)
            || operation.ValueKind is not JsonValueKind.Object
            || !operation.TryGetProperty("operationId", out var operationId)
            || operationId.ValueKind is not JsonValueKind.String)
        {
            return null;
        }

        return operationId.GetString();
    }

    private static string GetSafeTargetSummary(McpToolDefinition definition) =>
        definition.Name switch
        {
            "command.execute" => "A permitted CrossMacro command.",
            "automation.start" => "A bounded CrossMacro automation operation.",
            "automation.stop" => "An active CrossMacro automation operation.",
            "clipboard.set_text" => "The system text clipboard.",
            "clipboard.set_image" => "The system image clipboard.",
            "window.control" => "A selected desktop window.",
            "screenshot.capture" => "The current desktop screen or requested region.",
            _ => "A CrossMacro effectful operation.",
        };

    private static CallToolResult CreateErrorResult(McpToolOutcome outcome) => new()
    {
        Content = [new TextContentBlock { Text = outcome.Message }],
        IsError = true,
    };

}
