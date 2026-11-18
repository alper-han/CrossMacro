using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrossMacro.Cli.Tests;

/// <summary>
/// Pins the exact <c>data</c> member emitted by <c>--json</c> for every payload type:
/// camelCase property names, declaration order, string enums, and omitted optional members.
/// </summary>
public sealed class CliJsonPayloadContractTests
{
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly Guid TaskId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static TheoryData<string, object, string> Payloads => new()
    {
        { "quick-setup", new QuickSetupCommandData("flatpak", Applicable: true, Applied: false), """{"provider":"flatpak","applicable":true,"applied":false}""" },
        { "doctor", new DoctorCommandData([new DoctorCheckOutput("display", "pass", "ok", Details: null)]), """{"checks":[{"name":"display","status":"pass","message":"ok","details":null}]}""" },
        {
            "run-script",
            new RunScriptExecutionData(2, 3, 400, 5, 6, true, 7, 8, 9, 10, "absolute", new Dictionary<string, string> { ["found"] = "true" }),
            """{"stepCount":2,"eventCount":3,"totalDurationMs":400,"initialDelayMicroseconds":5,"initialDelayMs":6,"initialHasRandomDelay":true,"initialRandomDelayMinMs":7,"initialRandomDelayMaxMs":8,"trailingDelayMicroseconds":9,"trailingDelayMs":10,"coordinateMode":"absolute","runtimeVariables":{"found":"true"}}"""
        },
        { "macro-summary", new MacroSummaryData("/m.macro", "m", 4, 500, "absolute", IsAbsoluteCoordinates: true), """{"macroPath":"/m.macro","macroName":"m","eventCount":4,"totalDurationMs":500,"coordinateMode":"absolute","isAbsoluteCoordinates":true}""" },
        { "macro-validation", new MacroValidationData("/m.macro", 4), """{"macroPath":"/m.macro","eventCount":4}""" },
        {
            "macro-info",
            new MacroInfoData("/m.macro", "m", Stamp, 4, 500, "absolute", true, false, 11, 12, true, 13, 14, new MacroEventBreakdownData(1, 2, 3, 4, 5, 6)),
            """{"macroPath":"/m.macro","macroName":"m","createdAt":"2026-01-02T03:04:05Z","eventCount":4,"totalDurationMs":500,"coordinateMode":"absolute","isAbsoluteCoordinates":true,"skipInitialZeroZero":false,"trailingDelayMicroseconds":11,"trailingDelayMs":12,"hasTrailingRandomDelay":true,"trailingDelayMinMs":13,"trailingDelayMaxMs":14,"eventBreakdown":{"mouseMove":1,"buttonPress":2,"buttonRelease":3,"click":4,"keyPress":5,"keyRelease":6}}"""
        },
        { "record", new RecordExecutionData("/r.macro", 4, 500, true, false, "auto", "absolute", SkipInitialZero: true), """{"outputPath":"/r.macro","eventCount":4,"totalDurationMs":500,"recordMouse":true,"recordKeyboard":false,"requestedMode":"auto","actualMode":"absolute","skipInitialZero":true}""" },
        { "headless", new HeadlessRuntimeData(true, false, true, false, true), """{"globalHotkeys":true,"scheduler":false,"shortcuts":true,"textExpansion":false,"hotkeyActions":true}""" },
        {
            "schedule-list",
            new TaskListData<ScheduleTaskData>(1, [new ScheduleTaskData(TaskId, "daily", true, "Interval", "/m.macro", 1.5, 5, "Minutes", null, null, null, Stamp, null, "ok")]),
            """{"count":1,"tasks":[{"id":"11111111-2222-3333-4444-555555555555","name":"daily","enabled":true,"type":"Interval","macroFilePath":"/m.macro","playbackSpeed":1.5,"intervalValue":5,"intervalUnit":"Minutes","nextRunTime":"2026-01-02T03:04:05Z","lastRunTime":null,"lastStatus":"ok"}]}"""
        },
        { "schedule-run", new ScheduleTaskRunData(TaskId, "daily", true, "/m.macro", Stamp, null), """{"id":"11111111-2222-3333-4444-555555555555","name":"daily","enabled":true,"macroFilePath":"/m.macro","lastRunTime":"2026-01-02T03:04:05Z","lastStatus":null}""" },
        {
            "shortcut-list",
            new TaskListData<ShortcutTaskData>(1, [new ShortcutTaskData(
                TaskId, "browser", true, ["Ctrl+B"], "/m.macro", 1, LoopEnabled: false, RunWhileHeld: false, 0, 0, RandomRepeatDelay: false, RepeatDelayMinMs: null, RepeatDelayMaxMs: null,
                [new ShortcutWindowRule { Field = TriggerField.WindowClass, MatchMode = TriggerMatchMode.Equals, Value = "firefox" }], null, null)]),
            """{"count":1,"tasks":[{"id":"11111111-2222-3333-4444-555555555555","name":"browser","enabled":true,"hotkeys":["Ctrl+B"],"macroFilePath":"/m.macro","playbackSpeed":1,"loopEnabled":false,"runWhileHeld":false,"repeatCount":0,"repeatDelayMs":0,"randomRepeatDelay":false,"windowRules":[{"field":"WindowClass","matchMode":"Equals","value":"firefox"}],"lastTriggeredTime":null,"lastStatus":null,"runOnPressAndRelease":false,"releaseMacroFilePath":""}]}"""
        },
        { "shortcut-run", new ShortcutTaskRunData(TaskId, "browser", true, ["F7"], "/m.macro", null, "ok"), """{"id":"11111111-2222-3333-4444-555555555555","name":"browser","enabled":true,"hotkeys":["F7"],"macroFilePath":"/m.macro","lastTriggeredTime":null,"lastStatus":"ok","runOnPressAndRelease":false,"releaseMacroFilePath":""}""" },
        {
            "trigger-list",
            new TaskListData<TriggerTaskData>(1, [new TriggerTaskData(TaskId, "focus", true, "WindowClass", "Equals", "firefox", "RunMacro", null, "/m.macro", "Once", 5, null, null, null)]),
            """{"count":1,"tasks":[{"id":"11111111-2222-3333-4444-555555555555","name":"focus","enabled":true,"field":"WindowClass","matchMode":"Equals","value":"firefox","action":"RunMacro","macroFilePath":"/m.macro","fireMode":"Once","cooldownMs":5,"lastTriggeredTime":null,"lastStatus":null}]}"""
        },
        { "clipboard-text", new ClipboardTextData("hello"), """{"value":"hello"}""" },
        { "clipboard-set", new ClipboardSetData(5, "text"), """{"length":5,"source":"text"}""" },
        { "settings-value", new SettingsValueData("playback.speed", 1.5), """{"key":"playback.speed","value":1.5}""" },
        { "settings-mutation", new SettingsMutationData("playback.loop", false, true), """{"key":"playback.loop","oldValue":false,"newValue":true}""" },
        {
            "window-info",
            new WindowInfoData("0x1", "Title", "firefox", 42, "1", true, false, true, false, false, false, 1, 2, 3, 4),
            """{"address":"0x1","title":"Title","class":"firefox","pid":42,"workspace":"1","isFocused":true,"isFullscreen":false,"isMaximized":true,"isFloating":false,"isPinned":false,"isHidden":false,"x":1,"y":2,"width":3,"height":4}"""
        },
        { "window-list", new WindowListData([], 0), """{"windows":[],"count":0}""" },
        { "window-wait", new WindowWaitData(false, null, 5000), """{"found":false,"window":null,"timeoutMs":5000}""" },
        { "window-mutation", new WindowMutationData("focus", true), """{"operation":"focus","result":true}""" },
        { "workspace", new WorkspaceData("2"), """{"workspace":"2"}""" },
        { "screen-pixel", new ScreenPixelData(1, 2, "AABBCC", "x11", Relative: false), """{"x":1,"y":2,"color":"AABBCC","providerName":"x11","relative":false}""" },
        { "screen-wait-color", new ScreenWaitColorData(1, 2, "AABBCC", "AABBCD", "x11", Matched: true, TimeoutMs: null), """{"x":1,"y":2,"expectedColor":"AABBCC","actualColor":"AABBCD","providerName":"x11","matched":true,"timeoutMs":null}""" },
        { "screen-search-color", new ScreenSearchColorData(true, 1, 2, "AABBCC", "AABBCC", 0, 0, 10, 20, 3, "x11"), """{"found":true,"x":1,"y":2,"color":"AABBCC","expectedColor":"AABBCC","regionX":0,"regionY":0,"regionWidth":10,"regionHeight":20,"tolerance":3,"providerName":"x11"}""" },
        { "screen-search-image", new ScreenSearchImageData(false, null, null, null, "/i.png", null, null, null, null, 0.9, "Best", "x11"), """{"found":false,"x":null,"y":null,"score":null,"imagePath":"/i.png","regionX":null,"regionY":null,"regionWidth":null,"regionHeight":null,"similarity":0.9,"matchMode":"Best","providerName":"x11"}""" },
        { "screen-image-click", new ScreenImageClickData(1, 2, 0.95, "/i.png", null, null, null, null, 0.9, "Best", "Left", "x11"), """{"x":1,"y":2,"score":0.95,"imagePath":"/i.png","regionX":null,"regionY":null,"regionWidth":null,"regionHeight":null,"similarity":0.9,"matchMode":"Best","button":"Left","providerName":"x11"}""" },
        { "screenshot", new ScreenshotData("/s.png", 10, 20, "png", "x11", IsRegion: false, CopiedToClipboard: true), """{"outputPath":"/s.png","width":10,"height":20,"format":"png","providerName":"x11","isRegion":false,"copiedToClipboard":true}""" },
        { "profile", new ProfileData("p1", "Work", Stamp, IsActive: true), """{"id":"p1","name":"Work","createdAt":"2026-01-02T03:04:05Z","isActive":true}""" },
        { "profile-list", new ProfileListData([new ProfileData("p1", "Work", Stamp, IsActive: true)], "p1"), """{"profiles":[{"id":"p1","name":"Work","createdAt":"2026-01-02T03:04:05Z","isActive":true}],"activeProfileId":"p1"}""" },
        {
            "text-expansion-list",
            new TextExpansionListData([new TextExpansionData(":sig", "Regards", true, "Paste", "Clipboard", "Auto")], "p1", 1),
            """{"expansions":[{"trigger":":sig","replacement":"Regards","isEnabled":true,"method":"Paste","insertionMode":"Clipboard","directTypingMethod":"Auto"}],"profileId":"p1","count":1}"""
        },
        { "text-expansion-test", new TextExpansionTestData(false, null), """{"found":false,"expansion":null}""" },
    };

    private static readonly JsonSerializerOptions CompactRelaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [Theory]
    [MemberData(nameof(Payloads))]
    public async Task Json_EmitsTheDocumentedPayloadShape(string name, object data, string expectedDataJson)
    {
        Assert.NotNull(name);

        var output = await CaptureJsonAsync(CliCommandExecutionResult.Ok("done", data));

        var envelope = JsonNode.Parse(output)!.AsObject();
        Assert.Equal(expectedDataJson, envelope["data"]!.ToJsonString(CompactRelaxed));
    }

    [Fact]
    public async Task Json_EnvelopeIsIndentedWithStableMemberOrder()
    {
        var output = await CaptureJsonAsync(CliCommandExecutionResult.Ok("Pixel 1,2: AABBCC", new ScreenPixelData(1, 2, "AABBCC", "x11", Relative: false)));

        Assert.Equal(
            """
            {
              "status": "ok",
              "code": 0,
              "message": "Pixel 1,2: AABBCC",
              "data": {
                "x": 1,
                "y": 2,
                "color": "AABBCC",
                "providerName": "x11",
                "relative": false
              },
              "warnings": [],
              "errors": []
            }
            """.ReplaceLineEndings(Environment.NewLine),
            output.TrimEnd());
    }

    [Fact]
    public async Task Json_KeepsNonAsciiTextReadable()
    {
        var output = await CaptureJsonAsync(CliCommandExecutionResult.Ok("`x` Zażółć <tag>", new WorkspaceData("é")));

        Assert.Contains("`x` Zażółć <tag>", output, StringComparison.Ordinal);
        Assert.Contains("\"workspace\": \"é\"", output, StringComparison.Ordinal);
    }

    private static async Task<string> CaptureJsonAsync(CliCommandExecutionResult result)
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var stdout = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            CliOutputFormatter.Write(result, jsonOutput: true);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return stdout.ToString();
    }
}
