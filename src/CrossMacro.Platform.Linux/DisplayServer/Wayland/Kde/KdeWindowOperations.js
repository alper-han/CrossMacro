// Window operations that run inside KWin's scripting engine.
//
// The host loads this file as a one-shot KWin script, substitutes the __PLACEHOLDERS__ below and
// appends exactly one call: crossmacroRun('<operation>', [<arguments>]).
// Every operation reports its result to the host through sendCallback(), so the host knows what
// happened instead of assuming that a script that loaded must also have done its work:
//   - queries answer with their data, or null when there is nothing to report;
//   - mutations answer true when they acted on a window or desktop, and false when the target
//     does not exist.

function sendCallback(data) {
    callDBus('__SERVICE_NAME__', '__OBJECT_PATH__', '__INTERFACE__', 'ReportWindowData', '__CORRELATION_ID__', JSON.stringify(data));
}

function crossmacroWindowList() {
    return (typeof workspace.windowList === 'function') ? workspace.windowList() : workspace.clientList();
}

function crossmacroActiveWindow() {
    return workspace.activeWindow || workspace.activeClient;
}

function crossmacroAddress(window, fallback) {
    return (window.internalId || window.windowId || fallback).toString();
}

function crossmacroDescribe(window, fallbackAddress, workspaceName, isFocused) {
    var g = window.frameGeometry;
    return {
        Address: crossmacroAddress(window, fallbackAddress),
        Title: window.caption || '',
        Class: window.resourceClass || '',
        Pid: window.pid || 0,
        Workspace: workspaceName,
        IsFocused: isFocused,
        IsMaximized: window.maximizeMode !== 0,
        IsFullscreen: window.fullScreen || false,
        IsFloating: window.tile == null,
        IsPinned: window.onAllDesktops || false,
        IsHidden: window.minimized || false,
        X: g ? Math.round(g.x) : 0,
        Y: g ? Math.round(g.y) : 0,
        Width: g ? Math.round(g.width) : 0,
        Height: g ? Math.round(g.height) : 0
    };
}

function crossmacroFindByAddress(address) {
    var list = crossmacroWindowList();
    for (var i = 0; i < list.length; i++) {
        if (crossmacroAddress(list[i], i) === address) {
            return list[i];
        }
    }

    return null;
}

// Case-insensitive "contains" on a window property; the needle arrives already upper-cased.
function crossmacroFindByText(property, upperCaseNeedle) {
    var list = crossmacroWindowList();
    for (var i = 0; i < list.length; i++) {
        var text = list[i][property];
        if (text && text.toUpperCase().indexOf(upperCaseNeedle) !== -1) {
            return list[i];
        }
    }

    return null;
}

function crossmacroFindDesktop(name) {
    var desktops = workspace.desktops;
    for (var i = 0; i < desktops.length; i++) {
        if (desktops[i].name === name) {
            return desktops[i];
        }
    }

    return null;
}

function crossmacroFocus(window) {
    if (!window) {
        return false;
    }

    workspace.activeWindow = window;
    return true;
}

function crossmacroClose(window) {
    if (!window) {
        return false;
    }

    if (typeof window.closeWindow === 'function') {
        window.closeWindow();
    } else {
        window.close();
    }

    return true;
}

function crossmacroSetGeometry(x, y, width, height) {
    var w = crossmacroActiveWindow();
    if (!w) {
        return false;
    }

    var g = w.frameGeometry;
    w.frameGeometry = {
        x: x === null ? g.x : x,
        y: y === null ? g.y : y,
        width: width === null ? g.width : width,
        height: height === null ? g.height : height
    };
    return true;
}

function crossmacroMoveToDesktop(window, desktopName) {
    var desktop = crossmacroFindDesktop(desktopName);
    if (!window || !desktop) {
        return false;
    }

    window.desktops = [desktop];
    return true;
}

var crossmacroOperations = {
    activeWindow: function () {
        var w = crossmacroActiveWindow();
        if (!w) {
            return null;
        }

        var desktop = workspace.currentDesktop;
        return crossmacroDescribe(w, 0, (desktop && desktop.name) ? desktop.name : '', true);
    },

    windows: function () {
        var list = crossmacroWindowList();
        var active = workspace.activeWindow;
        var out = [];
        for (var i = 0; i < list.length; i++) {
            var w = list[i];
            out.push(crossmacroDescribe(w, i, (w.desktops && w.desktops.length > 0) ? w.desktops[0].name : '', active === w));
        }

        return out;
    },

    activeWorkspace: function () {
        var d = workspace.currentDesktop;
        return { name: (d && d.name) ? d.name : '' };
    },

    focusByAddress: function (address) {
        return crossmacroFocus(crossmacroFindByAddress(address));
    },

    focusByTitle: function (upperCaseTitle) {
        return crossmacroFocus(crossmacroFindByText('caption', upperCaseTitle));
    },

    focusByClass: function (upperCaseClass) {
        return crossmacroFocus(crossmacroFindByText('resourceClass', upperCaseClass));
    },

    closeByAddress: function (address) {
        return crossmacroClose(crossmacroFindByAddress(address));
    },

    closeByTitle: function (upperCaseTitle) {
        return crossmacroClose(crossmacroFindByText('caption', upperCaseTitle));
    },

    moveActive: function (x, y) {
        return crossmacroSetGeometry(x, y, null, null);
    },

    resizeActive: function (width, height) {
        return crossmacroSetGeometry(null, null, width, height);
    },

    fullscreenActive: function () {
        var w = crossmacroActiveWindow();
        if (!w) {
            return false;
        }

        w.fullScreen = !w.fullScreen;
        return true;
    },

    maximizeActive: function () {
        var w = crossmacroActiveWindow();
        if (!w) {
            return false;
        }

        w.setMaximize(w.maximizeMode === 0, w.maximizeMode === 0);
        return true;
    },

    centerActive: function () {
        var w = crossmacroActiveWindow();
        var screen = workspace.activeScreen;
        if (!w || !screen) {
            return false;
        }

        var sg = screen.geometry || workspace.clientArea(0, screen, workspace.currentDesktop);
        var wg = w.frameGeometry;
        w.frameGeometry = {
            x: Math.round(sg.x + (sg.width - wg.width) / 2),
            y: Math.round(sg.y + (sg.height - wg.height) / 2),
            width: wg.width,
            height: wg.height
        };
        return true;
    },

    switchWorkspace: function (name) {
        var desktop = crossmacroFindDesktop(name);
        if (!desktop) {
            return false;
        }

        workspace.currentDesktop = desktop;
        return true;
    },

    moveActiveToWorkspace: function (name) {
        return crossmacroMoveToDesktop(crossmacroActiveWindow(), name);
    },

    moveToWorkspaceByAddress: function (address, name) {
        return crossmacroMoveToDesktop(crossmacroFindByAddress(address), name);
    }
};

function crossmacroRun(operation, args) {
    var result = null;
    try {
        result = crossmacroOperations[operation].apply(null, args);
    } catch (error) {
        print('CrossMacro: KWin window operation ' + operation + ' failed: ' + error);
    }

    sendCallback(result);
}
