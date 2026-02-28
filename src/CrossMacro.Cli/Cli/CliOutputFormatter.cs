using System;
using System.IO;
using System.Text.Json;

namespace CrossMacro.Cli;

public static class CliOutputFormatter
{
    public static void Write(CliCommandExecutionResult result, bool jsonOutput)
    {
        if (jsonOutput)
        {
            WriteJson(result);
            return;
        }

        WriteText(result);
    }

    private static void WriteText(CliCommandExecutionResult result)
    {
        var writer = result.Success ? Console.Out : Console.Error;

        writer.WriteLine($"Status: {(result.Success ? "ok" : "error")}");
        writer.WriteLine($"Code: {result.ExitCode}");
        writer.WriteLine($"Message: {result.Message}");

        if (result.Data != null)
        {
            writer.WriteLine("Data:");
            var dataElement = JsonSerializer.SerializeToElement(result.Data);
            WriteTextData(writer, dataElement, indentLevel: 1);
        }

        if (result.Warnings.Count > 0)
        {
            writer.WriteLine("Warnings:");
            foreach (var warning in result.Warnings)
            {
                writer.WriteLine($"  - {warning}");
            }
        }

        if (result.Errors.Count > 0)
        {
            writer.WriteLine("Errors:");
            foreach (var error in result.Errors)
            {
                writer.WriteLine($"  - {error}");
            }
        }
    }

    private static void WriteJson(CliCommandExecutionResult result)
    {
        var envelope = new
        {
            status = result.Success ? "ok" : "error",
            code = result.ExitCode,
            message = result.Message,
            data = result.Data,
            warnings = result.Warnings,
            errors = result.Errors
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        Console.Out.WriteLine(json);
    }

    private static void WriteTextData(TextWriter writer, JsonElement element, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (IsScalar(property.Value))
                    {
                        writer.WriteLine($"{indent}{property.Name}: {FormatScalar(property.Value)}");
                        continue;
                    }

                    writer.WriteLine($"{indent}{property.Name}:");
                    WriteTextData(writer, property.Value, indentLevel + 1);
                }

                break;

            case JsonValueKind.Array:
            {
                var hasItems = false;
                foreach (var item in element.EnumerateArray())
                {
                    hasItems = true;
                    if (IsScalar(item))
                    {
                        writer.WriteLine($"{indent}- {FormatScalar(item)}");
                        continue;
                    }

                    writer.WriteLine($"{indent}-");
                    WriteTextData(writer, item, indentLevel + 1);
                }

                if (!hasItems)
                {
                    writer.WriteLine($"{indent}[]");
                }

                break;
            }

            default:
                writer.WriteLine($"{indent}{FormatScalar(element)}");
                break;
        }
    }

    private static bool IsScalar(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
    }

    private static string FormatScalar(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }
}
