using CrossMacro.UI.Tests.Localization;

namespace CrossMacro.UI.Tests.Converters;

[Collection(LocalizationGlobalStateCollection.Name)]
public sealed class LocalizedEnumConverterTests
{
    public static TheoryData<Type, string> LocalizedEnums => new()
    {
        { typeof(EditorActionType), "Editor_ActionType_" },
        { typeof(ScriptOperandType), "Editor_ScriptOperand_" },
        { typeof(ScriptConditionOperator), "Editor_ScriptConditionOperator_" },
        { typeof(EditorActionScreenTargetColorSource), "Editor_TargetColorSource" },
        { typeof(EditorImageMatchMode), "Editor_ImageMatchMode" },
        { typeof(TextInsertionMode), "TextExpansion_Mode" },
        { typeof(DirectTypingMethod), "TextExpansion_DirectTypingMethod" },
    };

    [Theory]
    [MemberData(nameof(LocalizedEnums))]
    public void Convert_EveryEnumMemberHasAResourceInEveryLanguage(Type enumType, string keyPrefix)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        var converter = new LocalizedEnumConverter { KeyPrefix = keyPrefix };

        foreach (var language in new[] { "en", "ar", "es", "fr", "ja", "pt", "ru", "tr", "zh" })
        {
            using var cultureScope = new LocalizationCultureScope(language);

            foreach (var member in Enum.GetValues(enumType))
            {
                var text = Assert.IsType<string>(converter.Convert(member, typeof(string), parameter: null, CultureInfo.InvariantCulture));

                _ = text.Should().NotBe(keyPrefix + member, $"{enumType.Name}.{member} must have a resource in '{language}'");
            }
        }
    }

    [Fact]
    public void Convert_FollowsTheActiveLanguage()
    {
        var converter = new LocalizedEnumConverter { KeyPrefix = "TextExpansion_Mode" };

        using var english = new LocalizationCultureScope("en");
        _ = converter.Convert(TextInsertionMode.Paste, typeof(string), parameter: null, CultureInfo.InvariantCulture).Should().Be("Paste");

        using var french = new LocalizationCultureScope("fr");
        _ = converter.Convert(TextInsertionMode.Paste, typeof(string), parameter: null, CultureInfo.InvariantCulture).Should().Be("Coller");
    }

    [Fact]
    public void Convert_WhenMemberHasNoResource_ShowsTheKey()
    {
        var converter = new LocalizedEnumConverter { KeyPrefix = "Missing_Prefix_" };

        _ = converter.Convert(TextInsertionMode.Paste, typeof(string), parameter: null, CultureInfo.InvariantCulture).Should().Be("Missing_Prefix_Paste");
    }

    [Fact]
    public void Convert_WhenValueIsNotAnEnum_ReturnsEmptyText()
    {
        var converter = new LocalizedEnumConverter { KeyPrefix = "TextExpansion_Mode" };

        _ = converter.Convert(value: null, typeof(string), parameter: null, CultureInfo.InvariantCulture).Should().Be(string.Empty);
        _ = converter.Convert(value: 3, typeof(string), parameter: null, CultureInfo.InvariantCulture).Should().Be(string.Empty);
    }
}
