
namespace CrossMacro.UI.Tests.Localization;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationGlobalStateCollection
{
    private LocalizationGlobalStateCollection()
    {
    }

    public const string Name = "LocalizationGlobalState";
}
