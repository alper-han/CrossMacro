
namespace CrossMacro.UI.Tests.Localization;

[Collection(LocalizationGlobalStateCollection.Name)]
public sealed class LocalizationBindingSourceTests
{
    [Fact]
    public void Initialize_RaisesIndexerChangeNotifications()
    {
        using var cultureScope = new LocalizationCultureScope();
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        var changedProperties = new List<string?>();
        source.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        source.Initialize(service);

        _ = changedProperties.Should().Contain("Item");
        _ = changedProperties.Should().Contain("Item[]");
    }

    [Fact]
    public void CultureChanged_RaisesIndexerChangeNotifications()
    {
        using var cultureScope = new LocalizationCultureScope();
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        source.Initialize(service);

        var changedProperties = new List<string?>();
        source.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        service.SetCulture("tr");

        _ = changedProperties.Should().Contain("Item");
        _ = changedProperties.Should().Contain("Item[]");
    }
}
