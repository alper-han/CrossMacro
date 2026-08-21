namespace CrossMacro.Mcp;

public sealed partial class CrossMacroMcpTools
{
    private static McpProfilesResult CreateProfilesResult(string action, CliCommandExecutionResult result)
    {
        var profiles = new List<McpProfile>();
        string? activeProfileId = null;
        if (result.Data is ProfileListData list)
        {
            activeProfileId = list.ActiveProfileId;
            profiles.AddRange(list.Profiles.Select(static profile => new McpProfile(profile.Id, profile.Name, profile.CreatedAt, profile.IsActive)));
        }
        else if (result.Data is ProfileData profile)
        {
            activeProfileId = profile.IsActive ? profile.Id : null;
            profiles.Add(new McpProfile(profile.Id, profile.Name, profile.CreatedAt, profile.IsActive));
        }

        return new McpProfilesResult(action, McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result), profiles, activeProfileId);
    }

    private static McpProfilesResult CreateProfilesResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Profiles: new List<McpProfile>(), ActiveProfileId: null);

    private static McpTextExpansionsResult CreateTextExpansionsResult(string action, CliCommandExecutionResult result)
    {
        var expansions = new List<McpTextExpansion>();
        string? profileId = null;
        var found = false;
        if (result.Data is TextExpansionListData list)
        {
            profileId = list.ProfileId;
            expansions.AddRange(list.Expansions.Select(ToTextExpansion));
        }
        else if (result.Data is TextExpansionData expansion)
        {
            found = true;
            expansions.Add(ToTextExpansion(expansion));
        }
        else if (result.Data is TextExpansionTestData test)
        {
            found = test.Found;
            if (test.Expansion is not null)
            {
                expansions.Add(ToTextExpansion(test.Expansion));
            }
        }

        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (result.Data is TextExpansionTestData { Found: true } && outcome.Success)
        {
            outcome = outcome with { Message = "Text expansion resolved." };
        }

        return new(action, outcome, expansions, profileId, found);
    }

    private static McpTextExpansionsResult CreateTextExpansionsResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Expansions: new List<McpTextExpansion>(), ProfileId: null, Found: false);

    private static McpTextExpansion ToTextExpansion(TextExpansionData expansion) =>
        new(expansion.Trigger, expansion.Replacement, expansion.IsEnabled, expansion.Method, expansion.InsertionMode, expansion.DirectTypingMethod);

    private static McpSetupResult CreateSetupResult(string action, QuickSetupStatus status, McpToolOutcome outcome, bool executed) =>
        new(action, outcome, status.Applicable, status.Provider, status.ShouldPrompt, executed);


    private static McpScheduleResult CreateScheduleResult(string action, CliCommandExecutionResult result)
    {
        var tasks = new List<McpScheduleTask>();
        McpScheduleTaskRun? run = null;
        McpScheduleTask? task = null;
        switch (result.Data)
        {
            case TaskListData<ScheduleTaskData> list:
                tasks.AddRange(list.Tasks.Select(ToScheduleTask));
                break;
            case ScheduleTaskRunData runData:
                run = ToScheduleTaskRun(runData);
                break;
            case ScheduleTaskData taskData:
                task = ToScheduleTask(taskData);
                break;
        }

        return new(action, McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result), tasks, run, task);
    }

    private static McpScheduleResult CreateScheduleResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: new List<McpScheduleTask>(), Run: null, Task: null);

    private static McpShortcutResult CreateShortcutResult(string action, CliCommandExecutionResult result)
    {
        var tasks = new List<McpShortcutTask>();
        McpShortcutTaskRun? run = null;
        McpShortcutTask? task = null;
        switch (result.Data)
        {
            case TaskListData<ShortcutTaskData> list:
                tasks.AddRange(list.Tasks.Select(ToShortcutTask));
                break;
            case ShortcutTaskRunData runData:
                run = ToShortcutTaskRun(runData);
                break;
            case ShortcutTaskData taskData:
                task = ToShortcutTask(taskData);
                break;
        }

        return new(action, McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result), tasks, run, task);
    }

    private static McpShortcutResult CreateShortcutResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: new List<McpShortcutTask>(), Run: null, Task: null);

    private static McpTriggerResult CreateTriggerResult(string action, CliCommandExecutionResult result)
    {
        var tasks = new List<McpTriggerTask>();
        McpTriggerTask? task = null;
        if (result.Data is TaskListData<TriggerTaskData> list)
        {
            tasks.AddRange(list.Tasks.Select(ToTriggerTask));
        }
        else if (result.Data is TriggerTaskData taskData)
        {
            task = ToTriggerTask(taskData);
        }

        return new(action, McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result), tasks, task);
    }

    private static McpTriggerResult CreateTriggerResult(string action, McpToolOutcome outcome) =>
        new(Action: action, Outcome: outcome, Tasks: new List<McpTriggerTask>(), Task: null);

    private static McpScheduleTask ToScheduleTask(ScheduleTaskData task) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.Enabled,
        Type = task.Type,
        MacroFilePath = task.MacroFilePath,
        PlaybackSpeed = task.PlaybackSpeed,
        IntervalValue = task.IntervalValue,
        IntervalUnit = task.IntervalUnit,
        ScheduledDateTime = task.ScheduledDateTime,
        WeeklyDays = task.WeeklyDays,
        WeeklyTime = task.WeeklyTime,
        NextRunTime = task.NextRunTime,
        LastRunTime = task.LastRunTime,
        LastStatus = task.LastStatus,
    };

    private static McpScheduleTaskRun ToScheduleTaskRun(ScheduleTaskRunData task) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.Enabled,
        MacroFilePath = task.MacroFilePath,
        LastRunTime = task.LastRunTime,
        LastStatus = task.LastStatus,
    };

    private static McpShortcutTask ToShortcutTask(ShortcutTaskData task) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.Enabled,
        Hotkey = task.Hotkey,
        MacroFilePath = task.MacroFilePath,
        PlaybackSpeed = task.PlaybackSpeed,
        LoopEnabled = task.LoopEnabled,
        RunWhileHeld = task.RunWhileHeld,
        RepeatCount = task.RepeatCount,
        RepeatDelayMs = task.RepeatDelayMs,
        RandomRepeatDelay = task.RandomRepeatDelay,
        RepeatDelayMinMs = task.RepeatDelayMinMs,
        RepeatDelayMaxMs = task.RepeatDelayMaxMs,
        WindowRules = task.WindowRules.Select(static rule => new McpShortcutWindowRule
        {
            Field = rule.Field.ToString(),
            MatchMode = rule.MatchMode.ToString(),
            Value = rule.Value,
        }).ToArray(),
        LastTriggeredTime = task.LastTriggeredTime,
        LastStatus = task.LastStatus,
    };

    private static McpShortcutTaskRun ToShortcutTaskRun(ShortcutTaskRunData task) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.Enabled,
        Hotkey = task.Hotkey,
        MacroFilePath = task.MacroFilePath,
        LastTriggeredTime = task.LastTriggeredTime,
        LastStatus = task.LastStatus,
    };

    private static McpTriggerTask ToTriggerTask(TriggerTaskData task) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Enabled = task.Enabled,
        Field = task.Field,
        MatchMode = task.MatchMode,
        Value = task.Value,
        Action = task.Action,
        TargetProfileId = task.TargetProfileId,
        MacroFilePath = task.MacroFilePath,
        FireMode = task.FireMode,
        CooldownMs = task.CooldownMs,
        DebounceMs = task.DebounceMs,
        LastTriggeredTime = task.LastTriggeredTime,
        LastStatus = task.LastStatus,
    };
    private static McpSettingsResult CreateSettingsResult(string action, McpToolOutcome outcome, object? data)
    {
        var settings = new List<McpSettingEntry>();
        var keys = new List<string>();
        if (data is IReadOnlyDictionary<string, object?> values)
        {
            settings.AddRange(values.Select(static pair => ToSettingEntry(pair.Key, pair.Value)));
        }
        else if (data is SettingsValueData value)
        {
            settings.Add(ToSettingEntry(value.Key, value.Value));
        }
        else if (data is SettingsMutationData mutation)
        {
            settings.Add(ToSettingEntry(mutation.Key, mutation.NewValue));
        }
        else if (data is IEnumerable<string> keyValues)
        {
            keys.AddRange(keyValues);
        }

        return new McpSettingsResult(action, outcome, settings.AsReadOnly(), keys.AsReadOnly());
    }

    private static McpSettingEntry ToSettingEntry(string key, object? value)
    {
        var redacted = IsSensitiveSettingKey(key);
        return new McpSettingEntry(
            key,
            redacted ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            redacted);
    }

    private static bool IsSensitiveSettingKey(string key) =>
        key.Contains("token", StringComparison.OrdinalIgnoreCase)
        || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static CallToolResult CreateToolResult(McpMacroInspectResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpMacroInspectResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpMacroListResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpMacroListResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpMacroValidateResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpMacroValidateResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpAutomationStartResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = GetAutomationResultMessage(result.Outcome.Message, result.Operation) }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpAutomationStartResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpAutomationGetResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = GetAutomationResultMessage(result.Outcome.Message, result.Operation) }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpAutomationGetResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpAutomationStopResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = GetAutomationStopResultMessage(result.Outcome.Message, result.Operation, result.CancellationInitiated) }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpAutomationStopResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpClipboardTextResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpClipboardTextResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpClipboardSetTextResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpClipboardSetTextResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpClipboardSetImageResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpClipboardSetImageResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpClipboardImageResult result, ReadOnlyMemory<byte>? pngBytes)
    {
        return new CallToolResult
        {
            Content = CreateImageContent(result.Outcome.Message, result.ImageIncluded, pngBytes),
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpClipboardImageResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpWindowQueryResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpWindowQueryResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpScreenReadResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpScreenReadResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpCursorPositionResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpCursorPositionResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpScreenImageSearchResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpScreenImageSearchResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpScreenshotCaptureResult result, ReadOnlyMemory<byte>? pngBytes)
    {
        return new CallToolResult
        {
            Content = CreateImageContent(result.Outcome.Message, result.ImageIncluded, pngBytes),
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpScreenshotCaptureResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpImageReadResult result, ReadOnlyMemory<byte>? pngBytes)
    {
        return new CallToolResult
        {
            Content = CreateImageContent(result.Outcome.Message, result.ImageIncluded, pngBytes),
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpImageReadResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static string GetAutomationResultMessage(string message, McpAutomationOperation? operation)
    {
        if (operation is null)
        {
            return message;
        }

        var outcome = operation.Outcome is null
            ? string.Empty
            : $" Outcome: {operation.Outcome.Message} (code {operation.Outcome.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}).";
        return $"{message} Operation ID: {operation.OperationId}; state: {operation.State}; completedAt: {operation.CompletedAt?.ToString("O") ?? "pending"}.{outcome}";
    }

    private static string GetAutomationStopResultMessage(
        string message,
        McpAutomationOperation? operation,
        bool cancellationInitiated) =>
        $"{GetAutomationResultMessage(message, operation)} Cancellation initiated: {cancellationInitiated}.";

    private static IList<ContentBlock> CreateImageContent(
        string message,
        bool imageIncluded,
        ReadOnlyMemory<byte>? pngBytes)
    {
        IList<ContentBlock> content = [new TextContentBlock { Text = message }];
        if (imageIncluded && pngBytes is { } image)
        {
            content.Add(new ImageContentBlock
            {
                Data = System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(image.Span)),
                MimeType = "image/png",
            });
        }

        return content;
    }
}
