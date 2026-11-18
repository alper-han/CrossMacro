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
    }

    [Fact]
    public void Initialize_WhenServiceIsNull_PreservesExistingSubscription()
    {
        using var cultureScope = new LocalizationCultureScope();
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        source.Initialize(service);

        var changedProperties = new List<string?>();
        source.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        var act = () => source.Initialize(null!);

        _ = act.Should().Throw<ArgumentNullException>();
        changedProperties.Clear();

        service.SetCulture("tr");

        _ = changedProperties.Should().Contain("Item");
    }

    [Theory]
    [InlineData("ar", FlowDirection.RightToLeft)]
    [InlineData("en", FlowDirection.LeftToRight)]
    [InlineData("fr", FlowDirection.LeftToRight)]
    [InlineData("ja", FlowDirection.LeftToRight)]
    public void FlowDirection_FollowsTheReadingDirectionOfTheCulture(string culture, FlowDirection expected)
    {
        using var cultureScope = new LocalizationCultureScope();
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        source.Initialize(service);

        service.SetCulture(culture);

        _ = source.FlowDirection.Should().Be(expected);
    }

    [Fact]
    public void FlowDirection_BeforeInitialization_IsLeftToRight()
    {
        var source = new LocalizationBindingSource();

        _ = source.FlowDirection.Should().Be(FlowDirection.LeftToRight);
    }

    [Fact]
    public void ObserveFlowDirection_EmitsCurrentAndChangedDirections()
    {
        using var cultureScope = new LocalizationCultureScope();
        var source = new LocalizationBindingSource();
        var service = new LocalizationService();
        source.Initialize(service);
        service.SetCulture("en");
        var emitted = new List<FlowDirection>();

        using var subscription = source.ObserveFlowDirection().Subscribe(new DirectionObserver(emitted));
        service.SetCulture("ar");
        service.SetCulture("tr");

        _ = emitted.Should().Equal(FlowDirection.LeftToRight, FlowDirection.RightToLeft, FlowDirection.LeftToRight);
    }

    private sealed class DirectionObserver(List<FlowDirection> values) : IObserver<FlowDirection>
    {
        public void OnCompleted()
        {
        }

        public void OnError(System.Exception error)
        {
            throw error;
        }

        public void OnNext(FlowDirection value)
        {
            values.Add(value);
        }
    }
}
