namespace CrossMacro.UI.Tests.Localization;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationGlobalStateGroup
{
    private LocalizationGlobalStateGroup()
    {
    }

    public const string Name = "LocalizationGlobalState";
}
