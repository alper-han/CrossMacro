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

internal sealed class DesignMacroFileManager : IMacroFileManager
{
    public Task SaveAsync(MacroSequence macro, string filePath) => Task.CompletedTask;

    public Task<MacroSequence?> LoadAsync(string filePath) => Task.FromResult<MacroSequence?>(DesignPreviewSamples.CreateMacro("Loaded Nightly Export Retry"));
}
