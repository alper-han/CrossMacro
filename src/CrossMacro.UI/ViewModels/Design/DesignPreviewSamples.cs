
namespace CrossMacro.UI.ViewModels;

internal static class DesignPreviewSamples
{
    public static readonly DateTime SampleNow = new(2026, 4, 16, 9, 30, 0, DateTimeKind.Local);

    public static MacroSequence CreateMacro(string name = "Invoice Form Fill")
    {
        var macro = new MacroSequence
        {
            Name = name,
            CreatedAt = SampleNow,
            RecordedAt = SampleNow.AddMinutes(-15),
            ActualDuration = TimeSpan.FromSeconds(2),
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            MouseMoveCount = 1,
            ClickCount = 1,
            EventsPerSecond = 3.0,
            TrailingDelayMs = 200,
            Events =
            [
                new MacroEvent { Type = EventType.MouseMove, X = 420, Y = 180, Timestamp = 0, DelayMs = 80 },
                new MacroEvent { Type = EventType.Click, X = 420, Y = 180, Button = MouseButton.Left, Timestamp = 80, DelayMs = 120 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, Timestamp = 200, DelayMs = 30 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30, Timestamp = 230, DelayMs = 30 },
                new MacroEvent { Type = EventType.KeyPress, KeyCode = 48, Timestamp = 260, DelayMs = 30 },
                new MacroEvent { Type = EventType.KeyRelease, KeyCode = 48, Timestamp = 290, DelayMs = 200 }
            ],
        };

        macro.CalculateDuration();
        return macro;
    }

    public static IReadOnlyList<TextExpansion> CreateTextExpansions()
    {
        return
        [
            new TextExpansion(":mail", "email@example.com", isEnabled: true, PasteMethod.CtrlV, TextInsertionMode.Paste),
            new TextExpansion(":retry-note", "Retry failed upload after reconnecting VPN", isEnabled: true, PasteMethod.CtrlShiftV, TextInsertionMode.Paste),
            new TextExpansion(":runbook", "Open dashboard and start the nightly export macro", isEnabled: true, PasteMethod.CtrlV, TextInsertionMode.DirectTyping),
        ];
    }

    public static IReadOnlyList<ScheduledTask> CreateScheduledTasks()
    {
        var intervalTask = new ScheduledTask
        {
            Name = "Refresh warehouse dashboard",
            Type = ScheduleType.Interval,
            MacroFilePath = "/home/demo/macros/refresh-dashboard.macro",
            PlaybackSpeed = 1.2,
            IntervalValue = 15,
            IntervalUnit = IntervalUnit.Minutes,
            LastRunTime = SampleNow.AddMinutes(-10),
            LastStatus = "Last run completed",
        };
        intervalTask.IsEnabled = true;
        intervalTask.NextRunTime = SampleNow.AddMinutes(5);

        var oneShotTask = new ScheduledTask
        {
            Name = "Run nightly export",
            Type = ScheduleType.SpecificTime,
            MacroFilePath = "/home/demo/macros/run-nightly-export.macro",
            PlaybackSpeed = 1.0,
            ScheduledDateTime = SampleNow.Date.AddDays(1).AddHours(1),
            LastStatus = "Queued for scheduled run",
        };
        oneShotTask.IsEnabled = true;
        oneShotTask.NextRunTime = SampleNow.Date.AddDays(1).AddHours(1);

        var weeklyTask = new ScheduledTask
        {
            Name = "Send weekday report",
            Type = ScheduleType.Weekly,
            MacroFilePath = "/home/demo/macros/send-weekday-report.macro",
            PlaybackSpeed = 1.0,
            WeeklyDays = ScheduleDays.Weekdays,
            WeeklyTime = new TimeSpan(9, 0, 0),
            LastStatus = "Waiting for next weekday",
        };
        weeklyTask.IsEnabled = true;
        weeklyTask.NextRunTime = SampleNow.Date.AddDays(1).AddHours(9);

        return [intervalTask, oneShotTask, weeklyTask];
    }

    public static IReadOnlyList<ShortcutTask> CreateShortcutTasks()
    {
        var loopShortcut = new ShortcutTask
        {
            Name = "Hold to repeat click",
            MacroFilePath = "/home/demo/macros/repeat-click.macro",
            HotkeyString = "Ctrl+Shift+1",
            PlaybackSpeed = 1.4,
            LoopEnabled = true,
            RepeatCount = 0,
            RepeatDelayMs = 120,
            LastTriggeredTime = SampleNow.AddMinutes(-3),
            LastStatus = "Loop running",
        };
        loopShortcut.IsEnabled = true;

        var singleShortcut = new ShortcutTask
        {
            Name = "Run invoice entry macro",
            MacroFilePath = "/home/demo/macros/invoice-entry.macro",
            HotkeyString = "Ctrl+Alt+H",
            PlaybackSpeed = 1.0,
            LastTriggeredTime = SampleNow.AddHours(-2),
            LastStatus = "Completed",
        };
        singleShortcut.IsEnabled = true;

        return [loopShortcut, singleShortcut];
    }

    public static IReadOnlyList<TriggerTask> CreateTriggerTasks()
    {
        var codeTrigger = new TriggerTask
        {
            Name = "Switch to dev profile in VS Code",
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Contains,
            Value = "Visual Studio Code",
            Action = TriggerAction.SwitchProfile,
            TargetProfileId = "dev",
            FireMode = TriggerFireMode.OnceOnChange,
            LastTriggeredTime = SampleNow.AddMinutes(-5),
            LastStatus = "Switched to dev",
        };
        codeTrigger.IsEnabled = true;

        var browserTrigger = new TriggerTask
        {
            Name = "Switch to gaming in Firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Equals,
            Value = "firefox",
            Action = TriggerAction.SwitchProfile,
            TargetProfileId = "gaming",
            FireMode = TriggerFireMode.OnceOnChange,
        };
        browserTrigger.IsEnabled = true;

        return [codeTrigger, browserTrigger];
    }

    public static IReadOnlyList<EditorAction> CreateEditorActions()
    {
        return
        [
            new EditorAction
            {
                Type = EditorActionType.SetVariable,
                ScriptVariableName = "retryCount",
                ScriptValueType = ScriptValueType.Number,
                ScriptValue = "0",
            },
            new EditorAction
            {
                Type = EditorActionType.IfBlockStart,
                ScriptLeftOperandType = ScriptOperandType.VariableReference,
                ScriptLeftOperand = "retryCount",
                ScriptConditionOperator = ScriptConditionOperator.LessThan,
                ScriptRightOperandType = ScriptOperandType.Number,
                ScriptRightOperand = "3",
            },
            new EditorAction
            {
                Type = EditorActionType.MouseClick,
                Button = MouseButton.Left,
                UseCurrentPosition = true,
                IsAbsolute = false,
            },
            new EditorAction
            {
                Type = EditorActionType.Delay,
                DelayMs = 250,
            },
            new EditorAction
            {
                Type = EditorActionType.TextInput,
                Text = "Export completed",
            },
            new EditorAction
            {
                Type = EditorActionType.BlockEnd
            },
        ];
    }

    public static IReadOnlyList<string> CreateEditorWarnings()
    {
        return
        [
            "Step 7: Unsupported script line was kept as raw text for preview",
        ];
    }
}
