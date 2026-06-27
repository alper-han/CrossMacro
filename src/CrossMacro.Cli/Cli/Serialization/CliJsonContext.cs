using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
[JsonSerializable(typeof(ScheduleTaskData))]
[JsonSerializable(typeof(ShortcutTaskData))]
[JsonSerializable(typeof(ScheduleTaskRunData))]
[JsonSerializable(typeof(ShortcutTaskRunData))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
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
