using Xunit;

namespace CrossMacro.UI.Tests.Localization;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationGlobalStateCollection
{
    public const string Name = "LocalizationGlobalState";
}
