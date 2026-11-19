
namespace CrossMacro.UI.Localization;

public sealed class LocalizationBindingSource : ObservableObject, IDisposable
{
    public ILocalizationService? Service => _service;

    private LocalizationService? _service;

    public string this[string key] => _service?[key] ?? key;

    public FlowDirection FlowDirection =>
        _service?.CurrentCulture.TextInfo.IsRightToLeft is true ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public IObservable<string> Observe(string key)
    {
        return new LocalizationValueObservable<string>(this, source => source[key]);
    }

    public IObservable<FlowDirection> ObserveFlowDirection()
    {
        return new LocalizationValueObservable<FlowDirection>(this, static source => source.FlowDirection);
    }

    public void Initialize(LocalizationService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (ReferenceEquals(_service, service))
        {
            return;
        }

        _service?.CultureChanged -= OnCultureChanged;

        _service = service;
        _service.CultureChanged += OnCultureChanged;
        NotifyLocalizedValuesChanged();
    }

    public void Dispose()
    {
        _service?.CultureChanged -= OnCultureChanged;
        _service = null;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        NotifyLocalizedValuesChanged();
    }

    private void NotifyLocalizedValuesChanged()
    {
        // Avalonia refreshes indexer bindings on "Item" notifications.
        OnPropertyChanged("Item");
        OnPropertyChanged(nameof(FlowDirection));
    }

    private sealed class LocalizationValueObservable<T>(LocalizationBindingSource source, Func<LocalizationBindingSource, T> select) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(select(source));

            void Handler(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName is "Item")
                {
                    observer.OnNext(select(source));
                }
            }

            source.PropertyChanged += Handler;
            return new Subscription(source, Handler);
        }

        private sealed class Subscription(LocalizationBindingSource source, PropertyChangedEventHandler handler) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                source.PropertyChanged -= handler;
            }
        }
    }
}
