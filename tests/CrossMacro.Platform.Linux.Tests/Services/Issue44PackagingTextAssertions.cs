
namespace CrossMacro.Platform.Linux.Tests.Services;

internal static class Issue44PackagingTextAssertions
{
    public static void AssertMentionsCrossmacroGroupRemediation(string text)
    {
        Assert.Contains("crossmacro", text, StringComparison.Ordinal);
        Assert.Contains("group", text, StringComparison.OrdinalIgnoreCase);
    }
}
