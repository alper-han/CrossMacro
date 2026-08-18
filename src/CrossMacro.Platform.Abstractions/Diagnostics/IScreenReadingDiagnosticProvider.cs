namespace CrossMacro.Platform.Abstractions.Diagnostics;

public interface IScreenReadingDiagnosticProvider
{
    public ScreenReadingDiagnosticSnapshot GetSnapshot();
}
