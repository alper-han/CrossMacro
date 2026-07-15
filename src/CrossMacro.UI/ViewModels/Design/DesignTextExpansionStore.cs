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

internal sealed class DesignTextExpansionStore : ITextExpansionStore
{
    private readonly object _sync = new();
    private List<TextExpansion> _expansions = new();

    public Task<List<TextExpansion>> LoadAsync() => Task.FromResult(GetCurrent());

    public Task SaveAsync(IEnumerable<TextExpansion> expansions)
    {
        ArgumentNullException.ThrowIfNull(expansions);

        lock (_sync)
        {
            _expansions = expansions.Select(CloneExpansion).ToList();
        }

        return Task.CompletedTask;
    }

    public List<TextExpansion> GetCurrent()
    {
        lock (_sync)
        {
            return _expansions.Select(CloneExpansion).ToList();
        }
    }

    private static TextExpansion CloneExpansion(TextExpansion expansion)
    {
        return new TextExpansion(expansion.Trigger, expansion.Replacement, expansion.IsEnabled, expansion.Method, expansion.InsertionMode);
    }
}
