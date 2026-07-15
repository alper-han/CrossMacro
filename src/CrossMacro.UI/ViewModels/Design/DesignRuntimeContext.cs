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

internal sealed class DesignRuntimeContext : IRuntimeContext
{
    public bool IsLinux => true;

    public bool IsWindows => false;

    public bool IsMacOS => false;

    public bool IsFlatpak => false;

    public string? SessionType => "x11";
}
