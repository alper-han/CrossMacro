using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Abstractions;
using static CrossMacro.Infrastructure.Services.Playback.WindowCommandHelpers;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowActiveCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "active";

    public string? Validate(string[] parts)
    {
        if (parts.Length != 4) return "Syntax: window active title|class|address|fullscreen|float|pinned|hidden $variable";
        var field = parts[2].ToLowerInvariant();
        if (field is not ("title" or "class" or "address" or "fullscreen" or "float" or "pinned" or "hidden")) 
            return $"Unknown field '{parts[2]}'. Expected: title, class, address, fullscreen, float, pinned, hidden.";
        if (!IsValidVarName(StripDollar(parts[3]))) return $"Invalid variable name '{parts[3]}'.";
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var field = parts[2].ToLowerInvariant();
        var varName = StripDollar(parts[3]);
        var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
        var val = field switch {
            "title" => info?.Title ?? string.Empty,
            "class" => info?.Class ?? string.Empty,
            "address" => info?.Address ?? string.Empty,
            "fullscreen" => (info?.IsFullscreen ?? false) ? "true" : "false",
            "float" => (info?.IsFloating ?? false) ? "true" : "false",
            "pinned" => (info?.IsPinned ?? false) ? "true" : "false",
            "hidden" => (info?.IsHidden ?? false) ? "true" : "false",
            _ => string.Empty
        };
        StoreVariable(variables, varName, val, stepNumber);
    }
}

internal sealed class WindowSearchCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "search";

    public string? Validate(string[] parts)
    {
        if (parts.Length < 4) return "Syntax: window search title|class \"<term>\" $variable";
        var field = parts[2].ToLowerInvariant();
        if (field is not ("title" or "class")) return $"Unknown field '{parts[2]}'. Expected: title, class.";
        var remaining = string.Join(' ', parts[3..]);
        if (!TryExtractTermAndVar(remaining, out _, out _, out var error)) return error;
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var field = parts[2].ToLowerInvariant();
        var remaining = string.Join(' ', parts[3..]);
        TryExtractTermAndVar(remaining, out var term, out var varName, out _);
        
        var windows = await query.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
        var match = field == "title" ? FindByTitle(windows, term!) : FindByClass(windows, term!);
        StoreVariable(variables, varName!, match?.Address ?? string.Empty, stepNumber);
    }
}

internal sealed class WindowFocusCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "focus";

    public string? Validate(string[] parts)
    {
        if (parts.Length < 3) return "Syntax: window focus active|title|class|address <value>";
        var field = parts[2].ToLowerInvariant();
        if (field == "active") return parts.Length == 3 ? null : "Syntax: window focus active";
        if (field is not ("title" or "class" or "address")) return $"Unknown field '{parts[2]}'. Expected: active, title, class, address.";
        var term = Unquote(string.Join(' ', parts[3..]));
        if (string.IsNullOrWhiteSpace(term)) return $"Missing value for 'window focus {field}'.";
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var field = parts[2].ToLowerInvariant();
        if (field == "active")
        {
            var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
            if (info != null) await mutator.FocusWindowByAddressAsync(info.Address, cancellationToken).ConfigureAwait(false);
            return;
        }
        var term = Unquote(string.Join(' ', parts[3..]));
        _ = field switch {
            "title" => await mutator.FocusWindowByTitleAsync(term, cancellationToken).ConfigureAwait(false),
            "class" => await mutator.FocusWindowByClassAsync(term, cancellationToken).ConfigureAwait(false),
            "address" => await mutator.FocusWindowByAddressAsync(term, cancellationToken).ConfigureAwait(false),
            _ => false
        };
    }
}

internal sealed class WindowCloseCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "close";

    public string? Validate(string[] parts)
    {
        if (parts.Length < 3) return "Syntax: window close active|title|address <value>";
        var field = parts[2].ToLowerInvariant();
        if (field == "active") return parts.Length == 3 ? null : "Syntax: window close active";
        if (field is not ("title" or "address")) return $"Unknown field '{parts[2]}'. Expected: active, title, address.";
        var term = Unquote(string.Join(' ', parts[3..]));
        if (string.IsNullOrWhiteSpace(term)) return $"Missing value for 'window close {field}'.";
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var field = parts[2].ToLowerInvariant();
        if (field == "active")
        {
            var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
            if (info != null) await mutator.CloseWindowByAddressAsync(info.Address, cancellationToken).ConfigureAwait(false);
            return;
        }
        var term = Unquote(string.Join(' ', parts[3..]));
        _ = field switch {
            "title" => await mutator.CloseWindowByTitleAsync(term, cancellationToken).ConfigureAwait(false),
            "address" => await mutator.CloseWindowByAddressAsync(term, cancellationToken).ConfigureAwait(false),
            _ => false
        };
    }
}

internal sealed class WindowMoveCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "move";

    public string? Validate(string[] parts)
    {
        if (parts.Length != 4) return "Syntax: window move <x> <y>";
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || 
            !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return $"'window move' requires integer coordinates. Got '{parts[2]}' '{parts[3]}'.";
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var x = int.Parse(parts[2], CultureInfo.InvariantCulture);
        var y = int.Parse(parts[3], CultureInfo.InvariantCulture);
        await mutator.MoveActiveWindowAsync(x, y, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class WindowResizeCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "resize";

    public string? Validate(string[] parts)
    {
        if (parts.Length != 4) return "Syntax: window resize <width> <height>";
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) || 
            !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
            return $"'window resize' requires integer dimensions. Got '{parts[2]}' '{parts[3]}'.";
        if (w <= 0 || h <= 0) return $"'window resize' dimensions must be positive. Got {w}x{h}.";
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var w = int.Parse(parts[2], CultureInfo.InvariantCulture);
        var h = int.Parse(parts[3], CultureInfo.InvariantCulture);
        await mutator.ResizeActiveWindowAsync(w, h, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class WindowWaitCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "wait";

    public string? Validate(string[] parts)
    {
        if (parts.Length < 4) return "Syntax: window wait title|class \"<term>\" [timeout_ms] $variable";
        var field = parts[2].ToLowerInvariant();
        if (field is not ("title" or "class")) return $"Unknown field '{parts[2]}'. Expected: title, class.";
        
        var remaining = string.Join(' ', parts[3..]).Trim();
        var lastSpace = remaining.LastIndexOf(' ');
        if (lastSpace < 0) return "Syntax: window wait title|class \"<term>\" [timeout_ms] $variable";

        var rawVar = remaining[(lastSpace + 1)..].Trim();
        if (!IsValidVarName(StripDollar(rawVar))) return $"Invalid variable name '{rawVar}'.";
        
        var beforeVar = remaining[..lastSpace].Trim();
        var beforeVarLastSpace = beforeVar.LastIndexOf(' ');
        string rawTerm = beforeVar;
        if (beforeVarLastSpace >= 0)
        {
            var maybeTimeout = beforeVar[(beforeVarLastSpace + 1)..].Trim();
            if (int.TryParse(maybeTimeout, NumberStyles.None, CultureInfo.InvariantCulture, out var t) && t > 0)
                rawTerm = beforeVar[..beforeVarLastSpace].Trim();
        }
        
        if (string.IsNullOrWhiteSpace(Unquote(rawTerm))) return "Search term cannot be empty.";
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var field = parts[2].ToLowerInvariant();
        var remaining = string.Join(' ', parts[3..]).Trim();
        var lastSpace = remaining.LastIndexOf(' ');
        var rawVar = remaining[(lastSpace + 1)..].Trim();
        var varName = StripDollar(rawVar);
        
        var beforeVar = remaining[..lastSpace].Trim();
        var beforeVarLastSpace = beforeVar.LastIndexOf(' ');
        
        int timeoutMs = 5000;
        string rawTerm = beforeVar;
        if (beforeVarLastSpace >= 0)
        {
            var maybeTimeout = beforeVar[(beforeVarLastSpace + 1)..].Trim();
            if (int.TryParse(maybeTimeout, NumberStyles.None, CultureInfo.InvariantCulture, out var t) && t > 0)
            {
                timeoutMs = t;
                rawTerm = beforeVar[..beforeVarLastSpace].Trim();
            }
        }
        
        var term = Unquote(rawTerm);
        var deadline = Environment.TickCount64 + timeoutMs;
        WindowInfo? found = null;

        while (Environment.TickCount64 < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windows = await query.GetWindowsAsync(cancellationToken).ConfigureAwait(false);
            found = field == "title" ? FindByTitle(windows, term) : FindByClass(windows, term);
            if (found != null) break;
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        StoreVariable(variables, varName, found?.Address ?? string.Empty, stepNumber);
    }
}

internal sealed class WindowStateCommandHandler : IWindowCommandHandler
{
    private readonly string _state;
    public WindowStateCommandHandler(string state) => _state = state;
    public string SubCommand => _state;

    public string? Validate(string[] parts)
    {
        if (parts.Length >= 3 && !parts[2].Equals("active", StringComparison.OrdinalIgnoreCase))
            return $"Syntax: window {_state} [active]";
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        _ = _state switch {
            "fullscreen" => await mutator.FullscreenActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            "float" => await mutator.FloatActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            "center" => await mutator.CenterActiveWindowAsync(cancellationToken).ConfigureAwait(false),
            _ => false
        };
    }
}

internal sealed class WindowWorkspaceCommandHandler : IWindowCommandHandler
{
    private readonly string _cmd;
    public WindowWorkspaceCommandHandler(string cmd) => _cmd = cmd;
    public string SubCommand => _cmd;

    public string? Validate(string[] parts)
    {
        if (_cmd == "getdesktop")
        {
            if (parts.Length != 3) return "Syntax: window getdesktop $variable";
            if (!IsValidVarName(StripDollar(parts[2]))) return $"Invalid variable name '{parts[2]}'.";
        }
        else if (_cmd == "setdesktop")
        {
            if (parts.Length != 3) return "Syntax: window setdesktop <workspace>";
            if (string.IsNullOrWhiteSpace(parts[2])) return "Workspace cannot be empty.";
        }
        else if (_cmd == "setdesktopforwindow")
        {
            if (parts.Length < 4) return "Syntax: window setdesktopforwindow active|address <addr> <workspace>";
            var field = parts[2].ToLowerInvariant();
            if (field == "active") return parts.Length == 4 ? null : "Syntax: window setdesktopforwindow active <workspace>";
            if (field == "address") return parts.Length == 5 ? null : "Syntax: window setdesktopforwindow address <addr> <workspace>";
            return $"Unknown field '{parts[2]}'. Expected: active, address.";
        }
        return null;
    }

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        if (_cmd == "getdesktop")
        {
            var ws = await workspace.GetActiveWorkspaceAsync(cancellationToken).ConfigureAwait(false);
            StoreVariable(variables, StripDollar(parts[2]), ws ?? string.Empty, stepNumber);
        }
        else if (_cmd == "setdesktop")
        {
            await workspace.SwitchWorkspaceAsync(parts[2], cancellationToken).ConfigureAwait(false);
        }
        else if (_cmd == "setdesktopforwindow")
        {
            var field = parts[2].ToLowerInvariant();
            if (field == "active")
                await workspace.MoveActiveWindowToWorkspaceAsync(parts[3], cancellationToken).ConfigureAwait(false);
            else if (field == "address")
                await workspace.MoveWindowToWorkspaceByAddressAsync(parts[3], parts[4], cancellationToken).ConfigureAwait(false);
        }
    }
}
