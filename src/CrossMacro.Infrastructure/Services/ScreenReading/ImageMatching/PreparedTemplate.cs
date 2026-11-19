namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

internal readonly record struct PreparedTemplate(TemplateCacheKey Key, RgbImage Image, TemplateStatistics Statistics);
