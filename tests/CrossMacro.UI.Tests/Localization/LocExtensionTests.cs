
namespace CrossMacro.UI.Tests.Localization;

[Collection(LocalizationGlobalStateCollection.Name)]
public sealed class LocExtensionTests
{
    [Fact]
    public void ProvideValue_ReturnsBindingForConfiguredKey()
    {
        var extension = new LocExtension("Settings_Title");

        var result = extension.ProvideValue(serviceProvider: null!);

        _ = extension.Key.Should().Be("Settings_Title");
        _ = result.Should().NotBeNull();
        _ = result.GetType().Name.Should().Contain("Binding");
    }

    [Fact]
    public void Observe_EmitsLocalizedValuesAndFallsBackToKey()
    {
        using var cultureScope = new LocalizationCultureScope();
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        source.Initialize(service);

        var emittedValues = new List<string>();
        var subscription = source.Observe("Settings_Title").Subscribe(new ListObserver(emittedValues));

        _ = emittedValues.Should().NotBeEmpty();
        _ = emittedValues[0].Should().Be(service["Settings_Title"]);

        service.SetCulture("tr-TR");

        _ = emittedValues.Should().Contain(service["Settings_Title"]);

        var missingValues = new List<string>();
        using var missingSubscription = source.Observe("__missing_key__").Subscribe(new ListObserver(missingValues));

        _ = missingValues.Should().ContainSingle().Which.Should().Be("__missing_key__");

        subscription.Dispose();
    }

    private sealed class ListObserver(List<string> values) : IObserver<string>
    {
        public void OnCompleted()
        {
        }

        public void OnError(System.Exception error)
        {
            throw error;
        }

        public void OnNext(string value)
        {
            values.Add(value);
        }
    }
}
