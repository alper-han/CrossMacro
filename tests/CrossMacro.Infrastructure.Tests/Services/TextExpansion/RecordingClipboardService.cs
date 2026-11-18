using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

/// <summary>Serves backup, verification and restore-guard reads from a fixed script and logs every access.</summary>
internal sealed class RecordingClipboardService(
    string? backupValue,
    string? verificationValue,
    string? restoreGuardValue) : IClipboardService
{
    private readonly Queue<string?> _readValues = new Queue<string?>([backupValue, verificationValue, restoreGuardValue]);

    public List<string> Events { get; } = [];

    public bool IsSupported => true;

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        Events.Add($"clipboard:set:{text}");
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        var readNumber = Events.Count(static item => item.StartsWith("clipboard:get:", StringComparison.Ordinal)) + 1;
        Events.Add(string.Create(CultureInfo.InvariantCulture, $"clipboard:get:{readNumber}"));
        return Task.FromResult(_readValues.Count > 0 ? _readValues.Dequeue() : null);
    }
}
