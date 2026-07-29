import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import Shell from 'gi://Shell';
import Meta from 'gi://Meta';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import { Extension } from 'resource:///org/gnome/shell/extensions/extension.js';

const MouseInterface = `
<node>
  <interface name="io.github.alper_han.crossmacro.Tracker">
    <method name="GetPosition">
      <arg type="i" direction="out" name="x"/>
      <arg type="i" direction="out" name="y"/>
    </method>
    <method name="GetResolution">
      <arg type="i" direction="out" name="width"/>
      <arg type="i" direction="out" name="height"/>
    </method>
    <method name="CaptureArea">
      <arg type="i" direction="in" name="x"/>
      <arg type="i" direction="in" name="y"/>
      <arg type="i" direction="in" name="width"/>
      <arg type="i" direction="in" name="height"/>
      <arg type="s" direction="out" name="base64Data"/>
      <arg type="i" direction="out" name="stride"/>
      <arg type="b" direction="out" name="hasAlpha"/>
    </method>
    <method name="GetWindows">
      <arg type="s" direction="out" name="json"/>
    </method>
    <method name="GetActiveWindow">
      <arg type="s" direction="out" name="json"/>
    </method>
    <method name="FocusWindow">
      <arg type="s" direction="in" name="address"/>
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="CloseWindow">
      <arg type="s" direction="in" name="address"/>
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="MoveActiveWindow">
      <arg type="i" direction="in" name="x"/>
      <arg type="i" direction="in" name="y"/>
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="ResizeActiveWindow">
      <arg type="i" direction="in" name="width"/>
      <arg type="i" direction="in" name="height"/>
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="MaximizeActiveWindow">
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="FullscreenActiveWindow">
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="CenterActiveWindow">
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="GetActiveWorkspace">
      <arg type="s" direction="out" name="name"/>
    </method>
    <method name="SwitchWorkspace">
      <arg type="s" direction="in" name="name"/>
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="MoveActiveWindowToWorkspace">
      <arg type="s" direction="in" name="name"/>
      <arg type="b" direction="out" name="success"/>
    </method>
    <method name="MoveWindowToWorkspaceByAddress">
      <arg type="s" direction="in" name="address"/>
      <arg type="s" direction="in" name="name"/>
      <arg type="b" direction="out" name="success"/>
    </method>
  </interface>
</node>`;

export default class CrossMacroExtension extends Extension {
    enable() {
        this._dbusImpl = Gio.DBusExportedObject.wrapJSObject(MouseInterface, this);
        this._dbusImpl.export(Gio.DBus.session, '/io/github/alper_han/crossmacro/Tracker');

        Gio.DBus.session.own_name(
            'io.github.alper_han.crossmacro.Tracker',
            Gio.BusNameOwnerFlags.NONE,
            null,
            null
        );
    }

    disable() {
        if (this._dbusImpl) {
            this._dbusImpl.unexport();
            this._dbusImpl = null;
        }
    }

    GetPosition() {
        let [x, y, mask] = global.get_pointer();
        return [x, y];
    }

    GetResolution() {
        let width = global.stage.get_width();
        let height = global.stage.get_height();
        return [width, height];
    }

    async CaptureArea(x, y, width, height) {
        try {
            let shooter = new Shell.Screenshot();
            let [content, scale] = await shooter.screenshot_stage_to_content();
            let texture = content.get_texture();
            let stream = Gio.MemoryOutputStream.new_resizable();
            let pixbuf = await Shell.Screenshot.composite_to_stream(
                texture, x, y, width, height,
                scale, null, 0, 0, 1.0, stream
            );
            stream.close(null);
            let pixels = pixbuf.get_pixels();
            let base64 = GLib.base64_encode(pixels);
            return [base64, pixbuf.get_rowstride(), pixbuf.get_has_alpha()];
        } catch (error) {
            console.error('CrossMacroExtension: CaptureArea failed:', error);
            throw error;
        }
    }

    _listWindows() {
        return global.get_window_actors()
            .map(a => a.meta_window)
            .filter(w => w && !w.is_override_redirect() && w.get_window_type() !== Meta.WindowType.DESKTOP);
    }
    
    _windowToJson(w) {
        if (!w) return null;
        let rect = w.get_frame_rect();
        let ws = w.get_workspace();
        
        let isMax = false;
        if (w.get_maximized) {
            isMax = w.get_maximized() === 3;
        } else if (w.is_maximized) {
            isMax = w.is_maximized();
        } else {
            isMax = w.maximized_horizontally && w.maximized_vertically;
        }

        let title = "";
        try { title = w.get_title() || ""; } catch (e) { /* Empty */ }
        
        let wmClass = "";
        try { wmClass = w.get_wm_class_instance() || w.get_wm_class() || ""; } catch (e) { /* Empty */ }

        return {
            Address: w.get_id().toString(),
            IsMaximized: isMax,
            Title: title,
            Class: wmClass,
            Pid: w.get_pid() || 0,
            Workspace: ws ? ws.index().toString() : "",
            IsFocused: w.has_focus(),
            IsFullscreen: w.is_fullscreen(),
            IsFloating: true,
            IsPinned: w.is_on_all_workspaces(),
            IsHidden: w.minimized,
            X: rect.x,
            Y: rect.y,
            Width: rect.width,
            Height: rect.height
        };
    }

    GetWindows() {
        let wins = this._listWindows().map(w => this._windowToJson(w)).filter(w => w !== null);
        return JSON.stringify(wins);
    }

    GetActiveWindow() {
        let w = global.display.focus_window;
        return w ? JSON.stringify(this._windowToJson(w)) : "null";
    }

    FocusWindow(address) {
        let id = Number(address);
        let w = this._listWindows().find(win => win.get_id() === id);
        if (w) {
            if (w.minimized && typeof w.unminimize === 'function') w.unminimize();
            w.activate(global.get_current_time());
            return true;
        }
        return false;
    }

    CloseWindow(address) {
        let id = Number(address);
        let w = this._listWindows().find(win => win.get_id() === id);
        if (w) {
            w.delete(global.get_current_time());
            return true;
        }
        return false;
    }
    
    MoveActiveWindow(x, y) {
        let w = global.display.focus_window;
        if (w) {
            let rect = w.get_frame_rect();
            w.move_resize_frame(true, x, y, rect.width, rect.height);
            return true;
        }
        return false;
    }

    ResizeActiveWindow(width, height) {
        let w = global.display.focus_window;
        if (w) {
            let rect = w.get_frame_rect();
            w.move_resize_frame(true, rect.x, rect.y, width, height);
            return true;
        }
        return false;
    }

    MaximizeActiveWindow() {
        let w = global.display.focus_window;
        if (w) {
            let isMax = false;
            if (w.get_maximized) {
                isMax = w.get_maximized() === 3;
            } else if (w.is_maximized) {
                isMax = w.is_maximized();
            } else {
                isMax = w.maximized_horizontally && w.maximized_vertically;
            }

            if (isMax) {
                if (w.unmaximize) {
                    if (w.unmaximize.length === 0) {
                        w.unmaximize();
                    } else {
                        w.unmaximize(3);
                    }
                }
            } else {
                if (w.maximize) {
                    if (w.maximize.length === 0) {
                        w.maximize();
                    } else {
                        w.maximize(3);
                    }
                }
            }
            return true;
        }
        return false;
    }

    FullscreenActiveWindow() {
        let w = global.display.focus_window;
        if (w) {
            if (w.is_fullscreen()) {
                w.unmake_fullscreen();
            } else {
                w.make_fullscreen();
            }
            return true;
        }
        return false;
    }

    CenterActiveWindow() {
        let w = global.display.focus_window;
        if (w) {
            let monitorIndex = w.get_monitor();
            let ws = w.get_workspace();
            let workArea;
            if (ws && ws.get_work_area_for_monitor) {
                workArea = ws.get_work_area_for_monitor(monitorIndex);
            } else {
                workArea = global.display.get_monitor_geometry(monitorIndex);
            }
            
            let rect = w.get_frame_rect();
            let targetX = workArea.x + Math.floor((workArea.width - rect.width) / 2);
            let targetY = workArea.y + Math.floor((workArea.height - rect.height) / 2);
            
            w.move_resize_frame(true, targetX, targetY, rect.width, rect.height);
            return true;
        }
        return false;
    }

    GetActiveWorkspace() {
        let ws = global.workspace_manager.get_active_workspace();
        return ws ? ws.index().toString() : "";
    }

    SwitchWorkspace(name) {
        let index = Number(name);
        if (isNaN(index)) return false;
        let ws = global.workspace_manager.get_workspace_by_index(index);
        if (ws) {
            ws.activate(global.get_current_time());
            return true;
        }
        return false;
    }

    MoveActiveWindowToWorkspace(name) {
        let index = Number(name);
        if (isNaN(index)) return false;
        let w = global.display.focus_window;
        let ws = global.workspace_manager.get_workspace_by_index(index);
        if (w && ws) {
            w.change_workspace(ws);
            return true;
        }
        return false;
    }

    MoveWindowToWorkspaceByAddress(address, name) {
        let id = Number(address);
        let index = Number(name);
        if (isNaN(index)) return false;
        let w = this._listWindows().find(win => win.get_id() === id);
        let ws = global.workspace_manager.get_workspace_by_index(index);
        if (w && ws) {
            w.change_workspace(ws);
            return true;
        }
        return false;
    }
}
