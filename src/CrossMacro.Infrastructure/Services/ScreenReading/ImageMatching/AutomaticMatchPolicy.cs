namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

/// <summary>Thresholds that decide how automatic (scale-tolerant) matching searches and accepts candidates.</summary>
internal static class AutomaticMatchPolicy
{
    internal const double ScaleScoreTieTolerance = 1e-12;
    internal const long AutomaticDirectScanWork = 4_000_000;
    internal const int AutomaticCandidateLimit = 8;
    internal const double AutomaticMinimumEvidenceMargin = 0.02;
    internal const int AutomaticSameTargetCenterTolerance = 2;
    internal const double MinimumAutomaticAppearanceEffectivePixels = 4.0;
    internal const double MaximumAutomaticPhotometricOffset = 64.0;
    internal const int MinimumSparseTemplateArea = 16;
    internal const int MinimumAutomaticScaledTemplateExtent = 4;
    internal const int PyramidRefinementRadius = 3;
    // Bound automatic pyramid setup on large desktops.
    internal const int MaximumPyramidLevels = 6;
    internal const int MinimumPyramidTemplateExtent = 4;
    internal const double MinimumAutomaticScale = 0.70;
    internal const double MaximumAutomaticScale = 1.50;
    internal const int LocalRefinementCandidateCount = 2;

    internal static readonly double[] AutomaticCoarseScales = [MinimumAutomaticScale, 0.80, 0.90, 1.10, 1.20, 1.30, 1.35, MaximumAutomaticScale];
    internal static readonly double[] LocalScaleOffsets = [-0.02, -0.01, 0.01, 0.02];
}
