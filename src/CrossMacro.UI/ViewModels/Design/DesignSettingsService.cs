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

internal sealed class DesignSettingsService : ISettingsService
{
    public DesignSettingsService(AppSettings settings)
    {
        Current = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public AppSettings Current { get; }

    public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

    public AppSettings Load() => Current;

    public Task SaveAsync() => Task.CompletedTask;

    public void Save()
    {
    }
}
