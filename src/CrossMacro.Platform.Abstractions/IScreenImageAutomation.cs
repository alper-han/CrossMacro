using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

public interface IScreenImageAutomation
{
    string ProviderName { get; }

    bool IsSupported { get; }

    Task<ScreenImageAutomationResult> SearchAsync(
        ScreenImageAutomationRequest request,
        CancellationToken cancellationToken);

    Task<ScreenImageAutomationResult> WaitAsync(
        ScreenImageAutomationRequest request,
        CancellationToken cancellationToken);

    Task<ScreenImageAutomationResult> ClickAsync(
        ScreenImageAutomationRequest request,
        int buttonCode,
        CancellationToken cancellationToken);
}

public sealed record ScreenImageAutomationRequest(
    string ImagePath,
    ScreenRect? Region = null,
    double Similarity = 1.0,
    int Downsample = 1,
    ScreenImageMatchMode MatchMode = ScreenImageMatchMode.First,
    bool ScaleAware = false,
    TimeSpan? Timeout = null);

public enum ScreenImageMatchMode
{
    First,
    Best,
}

public readonly record struct ScreenImageAutomationResult(
    bool IsSuccess,
    bool Found,
    ScreenPoint? Point,
    double? Score,
    ScreenReadErrorKind? ErrorKind,
    string? ErrorMessage)
{
    public static ScreenImageAutomationResult FoundAt(ScreenPoint point, double score) =>
        new(IsSuccess: true, Found: true, point, score, ErrorKind: null, ErrorMessage: null);

    public static ScreenImageAutomationResult NotFound(string message) =>
        new(IsSuccess: false, Found: false, Point: null, Score: null, ScreenReadErrorKind.CaptureTimeout, message);

    public static ScreenImageAutomationResult Failure(ScreenReadErrorKind errorKind, string message) =>
        new(IsSuccess: false, Found: false, Point: null, Score: null, errorKind, message);
}
