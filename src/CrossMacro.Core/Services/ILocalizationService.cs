using System;
using System.Globalization;

namespace CrossMacro.Core.Services;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }

    string this[string key] { get; }

    event EventHandler? CultureChanged;

    void SetCulture(string? cultureName);
}
