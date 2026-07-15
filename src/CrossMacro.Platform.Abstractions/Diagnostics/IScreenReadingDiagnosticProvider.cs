namespace CrossMacro.Platform.Abstractions.Diagnostics;

public interface IScreenReadingDiagnosticProvider
{
    ScreenReadingDiagnosticSnapshot GetSnapshot();
}
