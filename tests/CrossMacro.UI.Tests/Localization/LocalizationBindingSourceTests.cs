using System.Collections.Generic;
using CrossMacro.UI.Localization;
using FluentAssertions;
using Xunit;

namespace CrossMacro.UI.Tests.Localization;

public class LocalizationBindingSourceTests
{
    [Fact]
    public void Initialize_RaisesIndexerChangeNotifications()
    {
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        var changedProperties = new List<string?>();
        source.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        source.Initialize(service);

        changedProperties.Should().Contain("Item");
        changedProperties.Should().Contain("Item[]");
    }

    [Fact]
    public void CultureChanged_RaisesIndexerChangeNotifications()
    {
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        source.Initialize(service);

        var changedProperties = new List<string?>();
        source.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        service.SetCulture("tr");

        changedProperties.Should().Contain("Item");
        changedProperties.Should().Contain("Item[]");
    }
}
