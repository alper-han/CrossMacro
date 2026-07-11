using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli.Serialization;

[JsonSerializable(typeof(CliOutputEnvelope))]
[JsonSerializable(typeof(DoctorCommandData))]
[JsonSerializable(typeof(DoctorCheckOutput))]
[JsonSerializable(typeof(RunScriptExecutionData))]
[JsonSerializable(typeof(MacroSummaryData))]
[JsonSerializable(typeof(MacroInfoData))]
[JsonSerializable(typeof(MacroEventBreakdownData))]
[JsonSerializable(typeof(RecordExecutionData))]
[JsonSerializable(typeof(HeadlessRuntimeData))]
[JsonSerializable(typeof(TaskListData<ScheduleTaskData>))]
[JsonSerializable(typeof(TaskListData<ShortcutTaskData>))]
[JsonSerializable(typeof(TaskListData<TriggerTaskData>))]
[JsonSerializable(typeof(ScheduleTaskData))]
[JsonSerializable(typeof(ShortcutTaskData))]
[JsonSerializable(typeof(TriggerTaskData))]
[JsonSerializable(typeof(ScheduleTaskRunData))]
[JsonSerializable(typeof(ShortcutTaskRunData))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(ClipboardTextData))]
[JsonSerializable(typeof(ClipboardSetData))]
[JsonSerializable(typeof(SettingsValueData))]
[JsonSerializable(typeof(SettingsMutationData))]
[JsonSerializable(typeof(WindowInfoData))]
[JsonSerializable(typeof(WindowListData))]
[JsonSerializable(typeof(WindowWaitData))]
[JsonSerializable(typeof(WindowMutationData))]
[JsonSerializable(typeof(WorkspaceData))]
[JsonSerializable(typeof(ScreenPixelData))]
[JsonSerializable(typeof(ScreenWaitColorData))]
[JsonSerializable(typeof(ScreenSearchColorData))]
[JsonSerializable(typeof(ScreenSearchImageData))]
[JsonSerializable(typeof(ScreenImageClickData))]
[JsonSerializable(typeof(ScreenshotData))]
[JsonSerializable(typeof(ProfileData))]
[JsonSerializable(typeof(ProfileListData))]
[JsonSerializable(typeof(TextExpansionData))]
[JsonSerializable(typeof(TextExpansionListData))]
[JsonSerializable(typeof(TextExpansionTestData))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class CliJsonContext : JsonSerializerContext
{
}
