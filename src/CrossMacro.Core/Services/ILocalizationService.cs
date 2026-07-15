
namespace CrossMacro.Core.Services;

public interface ILocalizationService
{
    public CultureInfo CurrentCulture { get; }

    public string this[string key] { get; }

    public event EventHandler? CultureChanged;

    public void SetCulture(string? cultureName);
}
