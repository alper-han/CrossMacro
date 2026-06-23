using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux.DependencyInjection;
using CrossMacro.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

[Collection("EnvironmentVariableSensitive")]
public sealed class LiveWaylandScreenReadingSmokeTests
{
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(4);

    private readonly ITestOutputHelper _output;

    public LiveWaylandScreenReadingSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [WaylandLiveSmokeFact]
    public async Task Smoke_WhenEnabled_ReportsBackendDiagnostics_AndRunsBoundedPixelChecks()
    {
        using var services = BuildServices();
        using var smokeCts = new CancellationTokenSource(SmokeTimeout);

        var diagnosticProvider = services.GetRequiredService<IScreenReadingDiagnosticProvider>();
        var pixelReader = services.GetRequiredService<IScreenPixelReader>();

        var diagnostics = diagnosticProvider.GetSnapshot();
        _output.WriteLine(DescribeDiagnostics(diagnostics));

        Assert.True(pixelReader.IsSupported, DescribeDiagnostics(diagnostics));

        var point = new ScreenPoint(0, 0);

        var options = new ScreenReadOptions(
            timeout: OperationTimeout,
            pollInterval: TimeSpan.FromMilliseconds(50),
            cancellationToken: smokeCts.Token);

        var pixelResult = await pixelReader.GetPixelAsync(point, options).WaitAsync(OperationTimeout, smokeCts.Token);
        _output.WriteLine(DescribePixelResult("GetPixel", point, pixelResult));
        Assert.True(pixelResult.IsSuccess, DescribeFailure(diagnostics, "GetPixel", point, pixelResult.ErrorKind, pixelResult.ErrorMessage));

        var color = Assert.IsType<ScreenPixelColor>(pixelResult.Value);
        var secondPixelResult = await pixelReader.GetPixelAsync(point, options).WaitAsync(OperationTimeout, smokeCts.Token);
        _output.WriteLine(DescribePixelResult("GetPixel second", point, secondPixelResult));
        Assert.True(secondPixelResult.IsSuccess, DescribeFailure(diagnostics, "GetPixel second", point, secondPixelResult.ErrorKind, secondPixelResult.ErrorMessage));

        var region = new ScreenRect(point.X, point.Y, 1, 1);
        var searchResult = await pixelReader.SearchPixelAsync(region, color, 0, options).WaitAsync(OperationTimeout, smokeCts.Token);
        _output.WriteLine(DescribeSearchResult(region, color, searchResult));
        Assert.True(searchResult.IsSuccess, DescribeSearchFailure(diagnostics, region, color, searchResult.ErrorKind, searchResult.ErrorMessage));
        Assert.Equal(new ScreenPixelSearchMatch(point, color), searchResult.Value);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCommonRuntimeServices();
        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);
        services.AddCrossMacroSharedPostPlatformRuntimeServices(_ => null);
        return services.BuildServiceProvider();
    }

    private static string DescribeDiagnostics(ScreenReadingDiagnosticSnapshot diagnostics)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"SessionKind: {diagnostics.SessionKind}");
        builder.AppendLine($"PolicyName: {diagnostics.PolicyName}");
        builder.AppendLine($"PolicyOrder: {string.Join(", ", diagnostics.PolicyOrder)}");
        builder.AppendLine($"SelectedBackend: {diagnostics.SelectedBackend ?? "<none>"}");
        builder.AppendLine($"Failure: {DescribeFailure(diagnostics.FailureBackend, diagnostics.FailureKind, diagnostics.FailureMessage)}");
        builder.AppendLine("Backends:");

        foreach (var backend in diagnostics.Backends)
        {
            builder.AppendLine($"- {backend.Backend}: {(backend.IsAvailable ? "available" : $"unavailable ({backend.ErrorKind}: {backend.ErrorMessage})")}");
        }

        builder.AppendLine($"Remediation: {diagnostics.Remediation ?? "<none>"}");
        return builder.ToString();
    }

    private static string DescribePixelResult(string operation, ScreenPoint point, ScreenReadResult<ScreenPixelColor> result) =>
        result.IsSuccess
            ? $"{operation} at {point}: {result.Value}"
            : $"{operation} at {point}: {DescribeFailure(null, result.ErrorKind, result.ErrorMessage)}";

    private static string DescribeSearchResult(ScreenRect region, ScreenPixelColor color, ScreenReadResult<ScreenPixelSearchMatch> result) =>
        result.IsSuccess
            ? $"SearchPixel in {region} for {color}: {result.Value}"
            : $"SearchPixel in {region} for {color}: {DescribeFailure(null, result.ErrorKind, result.ErrorMessage)}";

    private static string DescribeFailure(ScreenReadingDiagnosticSnapshot? diagnostics, string operation, ScreenPoint point, ScreenReadErrorKind? errorKind, string? errorMessage) =>
        $"{operation} at {point} failed: {DescribeFailure(diagnostics?.FailureBackend, errorKind, errorMessage)}";

    private static string DescribeSearchFailure(ScreenReadingDiagnosticSnapshot? diagnostics, ScreenRect region, ScreenPixelColor color, ScreenReadErrorKind? errorKind, string? errorMessage) =>
        $"SearchPixel in {region} for {color} failed: {DescribeFailure(diagnostics?.FailureBackend, errorKind, errorMessage)}";

    private static string DescribeFailure(string? backend, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        var builder = new StringBuilder();
        builder.Append(errorKind?.ToString() ?? "<none>");
        builder.Append(": ");
        builder.Append(errorMessage ?? "<none>");

        if (!string.IsNullOrWhiteSpace(backend))
        {
            builder.Append(" (backend: ");
            builder.Append(backend);
            builder.Append(')');
        }

        return builder.ToString();
    }
}
