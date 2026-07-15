using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

internal sealed class DesignThemeService : IThemeService
{
    public DesignThemeService(string initialTheme)
    {
        ThemeCatalog.TryResolve(initialTheme, out var descriptor);
        CurrentTheme = descriptor.Name;
    }

    public IReadOnlyList<string> AvailableThemes => ThemeCatalog.ThemeNames;

    public string CurrentTheme { get; private set; }

    public bool TryApplyTheme(string themeName, out string error)
    {
        ThemeCatalog.TryResolve(themeName, out var descriptor);
        CurrentTheme = descriptor.Name;
        error = string.Empty;
        return true;
    }
}
