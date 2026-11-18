namespace CrossMacro.UI.Tests.Localization;

/// <summary>Localization substitutes with deterministic, assertion-friendly output.</summary>
internal static partial class TestLocalization
{
    /// <summary>
    /// Makes every real resource key visible: a known key resolves to <c>[Key]</c> followed by one
    /// <c>{n}</c> placeholder per argument of the real English format; an unknown key resolves to itself.
    /// </summary>
    /// <param name="overrides">Exact values for keys whose text matters to the test.</param>
    /// <param name="resolver">Resolves key families that are not worth listing; return <see langword="null"/> to defer to the resource lookup.</param>
    /// <param name="culture">Culture reported by the service; invariant when omitted.</param>
    public static ILocalizationService Bracketed(
        IReadOnlyDictionary<string, string>? overrides = null,
        Func<string, string?>? resolver = null,
        CultureInfo? culture = null) =>
        Create(overrides, resolver, culture, bracketKnownKeys: true);

    /// <summary>Resolves every key to itself except the <paramref name="overrides"/> and whatever <paramref name="resolver"/> returns.</summary>
    public static ILocalizationService Echo(
        IReadOnlyDictionary<string, string>? overrides = null,
        Func<string, string?>? resolver = null,
        CultureInfo? culture = null) =>
        Create(overrides, resolver, culture, bracketKnownKeys: false);

    /// <summary>Resolver that renders <c>Editor_ActionType_*</c> keys as the bare action type name.</summary>
    public static string? EditorActionTypeName(string key) =>
        key.StartsWith("Editor_ActionType_", StringComparison.Ordinal) ? key["Editor_ActionType_".Length..] : null;

    [GeneratedRegex(@"\{(?<index>\d+)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlaceholderPattern { get; }

    private static ILocalizationService Create(
        IReadOnlyDictionary<string, string>? overrides,
        Func<string, string?>? resolver,
        CultureInfo? culture,
        bool bracketKnownKeys)
    {
        var service = Substitute.For<ILocalizationService>();
        _ = service.CurrentCulture.Returns(culture ?? CultureInfo.InvariantCulture);
        _ = service[Arg.Any<string>()].Returns(call => Resolve(call.Arg<string>(), overrides, resolver, bracketKnownKeys));
        return service;
    }

    private static string Resolve(
        string key,
        IReadOnlyDictionary<string, string>? overrides,
        Func<string, string?>? resolver,
        bool bracketKnownKeys)
    {
        if (overrides is not null && overrides.TryGetValue(key, out var exact))
        {
            return exact;
        }

        var custom = resolver?.Invoke(key);
        if (custom is not null)
        {
            return custom;
        }

        var format = bracketKnownKeys ? Resources.ResourceManager.GetString(key, CultureInfo.InvariantCulture) : null;
        if (format is null)
        {
            return key;
        }

        var arguments = 0;
        foreach (Match placeholder in PlaceholderPattern.Matches(format))
        {
            arguments = Math.Max(arguments, int.Parse(placeholder.Groups["index"].Value, CultureInfo.InvariantCulture) + 1);
        }

        return arguments is 0
            ? $"[{key}]"
            : $"[{key}] {string.Join(' ', Enumerable.Range(0, arguments).Select(index => string.Create(CultureInfo.InvariantCulture, $"{{{index}}}")))}";
    }
}
