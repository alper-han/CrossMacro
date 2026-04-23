using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CrossMacro.UI.Localization;

public sealed class LocalizationBindingSource : ObservableObject
{
    public static LocalizationBindingSource Instance { get; } = new();

    private LocalizationService? _service;

    public string this[string key] => _service?[key] ?? key;

    public IObservable<string> Observe(string key)
    {
        return new LocalizationValueObservable(this, key);
    }

    public void Initialize(LocalizationService service)
    {
        if (ReferenceEquals(_service, service))
        {
            return;
        }

        if (_service != null)
        {
            _service.CultureChanged -= OnCultureChanged;
        }

        _service = service ?? throw new ArgumentNullException(nameof(service));
        _service.CultureChanged += OnCultureChanged;
        NotifyLocalizedValuesChanged();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        NotifyLocalizedValuesChanged();
    }

    private void NotifyLocalizedValuesChanged()
    {
        // Avalonia indexer bindings are refreshed by "Item" notifications.
        // Keep "Item[]" as well for compatibility with existing consumers/tests.
        OnPropertyChanged("Item");
        OnPropertyChanged("Item[]");
    }

    private sealed class LocalizationValueObservable(LocalizationBindingSource source, string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            observer.OnNext(source[key]);

            void Handler(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName is "Item" or "Item[]")
                {
                    observer.OnNext(source[key]);
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
