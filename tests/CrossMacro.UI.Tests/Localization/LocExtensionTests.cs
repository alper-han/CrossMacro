using System.Collections.Generic;
using CrossMacro.UI.Localization;
using FluentAssertions;
using Xunit;

namespace CrossMacro.UI.Tests.Localization;

public class LocExtensionTests
{
    [Fact]
    public void Observe_EmitsLocalizedValuesAndFallsBackToKey()
    {
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        source.Initialize(service);

        var emittedValues = new List<string>();
        var subscription = source.Observe("Settings_Title").Subscribe(new ListObserver(emittedValues));

        emittedValues.Should().NotBeEmpty();
        emittedValues[0].Should().Be(service["Settings_Title"]);

        service.SetCulture("tr-TR");

        emittedValues.Should().Contain(service["Settings_Title"]);

        var missingValues = new List<string>();
        using var missingSubscription = source.Observe("__missing_key__").Subscribe(new ListObserver(missingValues));

        missingValues.Should().ContainSingle().Which.Should().Be("__missing_key__");

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
