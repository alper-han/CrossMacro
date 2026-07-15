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

internal sealed class DesignDialogService : IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message, string yesText = "Yes", string noText = "No")
    {
        return Task.FromResult(true);
    }

    public Task ShowMessageAsync(string title, string message, string buttonText = "OK")
    {
        return Task.CompletedTask;
    }

    public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, FileDialogFilter[] filters)
    {
        return Task.FromResult<string?>("/home/demo/macros/nightly-export-retry.macro");
    }

    public Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[] filters)
    {
        return Task.FromResult<string?>("/home/demo/macros/nightly-export-retry.macro");
    }
}
