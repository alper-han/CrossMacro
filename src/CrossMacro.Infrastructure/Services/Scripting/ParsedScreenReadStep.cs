namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>Validated screen syntax shared by editor projection and runtime execution.</summary>
internal abstract record ParsedScreenReadStep(RunScriptScreenReadingCommand Command)
{
    public sealed record PixelColor(bool Relative, int X, int Y, string? ResultVariable)
        : ParsedScreenReadStep(RunScriptScreenReadingCommand.PixelColor);

    public sealed record WaitColor(int X, int Y, string ColorToken, int? TimeoutMs, string? ResultVariable)
        : ParsedScreenReadStep(RunScriptScreenReadingCommand.WaitColor);

    public sealed record PixelSearch(int X1, int Y1, int X2, int Y2, string ColorToken,
        PixelSearchVariableLayout Variables, int? TimeoutMs, int Tolerance)
        : ParsedScreenReadStep(RunScriptScreenReadingCommand.PixelSearch);

    public sealed record Image(RunScriptScreenReadingCommand ImageCommand, string ImageName,
        ImageRegion? Region, PixelSearchVariableLayout Variables, double Similarity,
        EditorImageMatchMode MatchMode, bool MatchModeExplicit, int? TimeoutMs, MacroMouseButton Button)
        : ParsedScreenReadStep(ImageCommand);

    public abstract record ImageRegion(bool IsExplicit);
    public sealed record ExplicitRegion(string Left, string Top, string Width, string Height) : ImageRegion(IsExplicit: true);
    public sealed record LegacyRegion(int Left, int Top, int Right, int Bottom) : ImageRegion(IsExplicit: false);
}
