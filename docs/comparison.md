# CrossMacro Comparisons

CrossMacro combines a visual recorder and editor with a CLI, scheduler,
screen-reading actions, profiles, and a local MCP server. AutoHotkey and
Hammerspoon instead center on scripting; xdotool and ydotool are command-line
input tools with different desktop integration boundaries.

## Scope and platform caveats

These tables compare built-in features and commands, distinguishing ready-made
workflows from capabilities that require scripting. Desktop features and CLI
commands are listed separately; platform requirements still apply.

**Legend:**
- **Built-in:** included with the tool, but not necessarily graphical.
- **GUI:** a ready-made graphical workflow.
- **Script API:** available by writing code, not a ready-made workflow.
- **ND:** not documented in the cited sources; it may still be possible through
  scripts or extensions.
- **No native support:** not supported directly on that platform.

Numbered references point to the sources in **Sources and review details** below.

### CrossMacro v1.5.0 limits

- CrossMacro's GUI-less runtime still needs an active desktop session; it is not
  display-less server automation. Input, cursor position, screen capture, tray,
  and window control have separate platform/backend requirements [1].
- On Linux, window control requires **Hyprland, Sway, Niri, KDE Plasma, or
  GNOME** with the relevant backend available. There is no registered window
  backend for desktops outside this list, including COSMIC and Wayfire. Window
  and workspace rows do not promise identical operations on all platforms [1].
- Wayland input requires the applicable daemon/direct-device permissions;
  absolute positioning also depends on a usable cursor provider. Recording can
  fall back to relative coordinates. Screen reading needs an available capture
  provider; Flatpak Wayland uses only Portal ScreenCast, with its permission and
  monitor-selection requirements. macOS input needs Input Monitoring and
  Accessibility permissions; screen reading needs Screen Recording [1].
- Image templates must be **native 8-bit PNG**. Multi-monitor gaps are not
  searchable pixels. The run language provides integer arithmetic and explicit
  comparisons, not a general-purpose scripting language; it has no implicit
  truthiness or `&&`/`||` expressions. `--dry-run` validates supported workflows,
  but does not prove that live input, capture, or a target application will work [1].

Consult the [Linux](linux.md), [Windows](windows.md), and [macOS](macos.md)
guides before choosing a CrossMacro workflow for a particular environment.
AutoHotkey input is also subject to Windows integrity/UAC restrictions [2].
Hammerspoon requires the applicable macOS permissions, and Secure Input prevents
keyboard interception by event taps [4].

## Desktop Automation Products

| Native desktop automation | CrossMacro [1] | AutoHotkey v2 [2, 3] | Hammerspoon [4, 5] |
| --- | --- | --- | --- |
| Windows | Supported; active desktop | Native platform | No native support |
| macOS | Supported; macOS 14+ and permissions | No native support | Native platform; permissions |
| Linux X11 | Supported; backend limits above | No native support | No native support |
| Linux Wayland | Supported; backend/permission-dependent | No native support | No native support |
| Mouse, keyboard, and scroll automation | GUI + CLI | Script API: `Send`, `Click`, `MouseMove` | Script API: `hs.eventtap`, mouse/event APIs |
| Mouse and keyboard recording | GUI + CLI recorder | Input hooks/APIs; ready-made recorder ND | Event-tap capture/serialization APIs; ready-made recorder ND |
| Ready-made visual macro editor | GUI | ND; `Gui` is a GUI-building API | ND; Lua configuration |
| Visual undo/redo and multi-action editing | GUI editor | ND as a macro-editor feature | ND as a macro-editor feature |
| Saved automation files or scripts | `.macro` files and run scripts | `.ahk` scripts | Lua scripts |
| Variables, arithmetic, loops, and conditions | GUI + run language; integer arithmetic | Scripting language | Lua scripting language |
| Pixel and color automation | Built-in; capture-dependent | Script API: `PixelGetColor`, `PixelSearch` | Script API: snapshot + `hs.image:colorAt`; search logic is custom |
| Built-in image search and click | GUI + CLI; PNG templates | Script API: `ImageSearch` then `Click` | Image-template search ND; custom/external matching needed |
| Screenshot capture API | Built-in screenshot action/CLI | Dedicated capture function ND; custom Win32 code via `DllCall` | Script API: `hs.screen:snapshot` |
| Clipboard API | Built-in actions + CLI | Script API: `A_Clipboard`, `ClipboardAll` | Script API: `hs.pasteboard` |
| Scheduled automation | GUI schedules + CLI management | Script API: `SetTimer`; calendar logic is custom | Script API: `hs.timer`, including `doAt` |
| Global hotkeys | GUI bindings + runtime | Built-in script hotkeys | Script API: `hs.hotkey` |
| Window or application triggers | GUI active-window/workspace rules; backend-dependent | Script waits/polling, e.g. `WinWaitActive`; not a rule-builder GUI | Script API: application watcher / window-filter subscriptions |
| Built-in text expansion | GUI entries + runtime | Built-in script hotstrings | Core text-expansion feature ND; custom scripts/Spoons outside scope |
| Named profiles | GUI + CLI; isolated user data | Built-in profile manager ND; separate scripts possible | Built-in profile manager ND; Lua configurations possible |
| Local MCP server | Built-in local stdio server | ND in core documentation | ND in core documentation |
| Ready-to-use cross-platform GUI | GUI on Linux, Windows, macOS; capabilities differ | No cross-platform GUI; Windows scripting tool | No cross-platform GUI; macOS app configured in Lua |

A recording API is not a turnkey recorder: Hammerspoon supplies event taps,
`asData`, `newEventFromData`, and event posting, but assembling timed capture and
replay remains script work. AutoHotkey's `InputHook` collects keyboard input;
this alone is not a mouse-and-keyboard macro recorder. Likewise, AutoHotkey's
image search returns a location for a subsequent click; it is not a single
image-click workflow. Its lack of a dedicated screenshot function does not
preclude capture through Windows APIs. None of these distinctions imply that
third-party recorders, editors, libraries, or Spoons cannot fill a gap [2–5].

## Command-Line Automation

The CLI comparison is job-based: commands do not need identical names. xdotool
uses X11/XTEST and window-manager protocols; ydotool injects Linux input through
`ydotoold` and `/dev/uinput`, independently of X11 or Wayland. Neither a Linux
input injector nor an X11 client implies native Windows/macOS desktop support.
xdotool is packaged for macOS, but that is not native macOS desktop automation.
Its upstream documentation explicitly warns that Wayland use does not work
correctly; Xwayland is not full Wayland desktop support [6, 7].

| CLI job | CrossMacro CLI [1] | xdotool [6] | ydotool [7] |
| --- | --- | --- | --- |
| Windows | Supported; active desktop | No native support; X11 tool | No native support; Linux uinput |
| macOS native desktop | Supported; permissions | No native support; X11 only | No native support; Linux uinput |
| Linux X11 | Supported | Built-in; X11/XTEST | Input injection via daemon/uinput |
| Linux Wayland | Supported; backend/permission-dependent | No native Wayland support | Input injection via daemon/uinput |
| Absolute mouse move | `move abs`; position/backend-dependent | `mousemove` | `mousemove --absolute`; acceleration caveat below |
| Relative mouse move | `move rel-raw` / `rel-logical` | `mousemove_relative` | `mousemove` |
| Click and button down/up | `click`, `down`, `up` | `click`, `mousedown`, `mouseup` | `click` with down/up bit masks |
| Vertical scroll | `scroll up/down` | Wheel button clicks, normally 4/5 | `mousemove --wheel`, Y axis |
| Horizontal scroll | `scroll left/right` | Via mapped X button clicks | `mousemove --wheel`, X axis |
| Key down/up and hotkey chords | `key`, `tap` | `keydown`, `keyup`, `key` | `key` with numeric keycode press/release sequences |
| Type text | `type` | `type` | `type`; keyboard-layout caveat |
| Built-in delay command | `delay`; fixed or random | `sleep`; event delays | No standalone `sleep`; event/no-op delays available |
| Query and control windows | `window`; supported backends only | Built-in; window-manager/application-dependent | ND; injected shortcuts are not a window-query API |
| Query and change workspaces | `window workspace`; backend-dependent | Desktop commands; window-manager support required | ND; injected shortcuts are not a workspace-query API |
| Read and write clipboard text | `clipboard` | ND | ND |
| Read pixels and search colors | `screen`; capture-dependent | ND | ND |
| Search, wait for, and click images | `screen`; PNG/capture limits above | ND | ND |
| Capture screenshots | `screenshot`; capture-dependent | ND | ND |
| Record input to an automation file | `record`; coordinate fallback possible | ND | Recorder removed in v1.0.0 |
| Play a saved automation file | `play` / `run --file` | Executes command scripts; not recorded macros | Saved macro/script playback ND; `type --file` types text |
| Variables | Run-local variables | SCRIPT positional/environment expansion; not a mutable run language | Workflow variables ND; shell expansion is external |
| Built-in loops and conditions | `repeat`, `for`, `while`, `if` | General control flow ND; click repeat/event callbacks available | General control flow ND; `click --repeat` available |
| Run shell commands as workflow steps | `shell`; package policy applies | `exec` runs a program; invoke a shell explicitly for shell syntax | Workflow execution ND; caller's shell is external |
| Validate without sending input | `--dry-run` / `macro validate`; scoped validation | Workflow validation-only mode ND | Workflow validation-only mode ND |
| Standardized JSON results and exit codes | `--json` envelope + documented exit-code categories | Text/exit status; common JSON result schema ND | Text/exit status; common JSON result schema ND |
| Manage schedules, shortcuts, and triggers | Built-in management commands | Persisted managers ND; `behave` / edge callbacks are narrower | ND |
| Start a local MCP server | `mcp`; local stdio | ND | ND |

### CLI details that affect equivalence

- **xdotool scripts** accept positional arguments (`$1`, `$2`, …) and environment
  variables. The manual describes expansion followed by command chaining, not a
  general variables/arithmetic/branching language. `exec` executes a program;
  shell pipelines and shell syntax require explicitly running a shell. Command
  scripts stop on command failure. Window/workspace commands depend on the
  window manager's protocol support, and applications may reject synthetic
  input. Button mappings determine the effect of scroll-button clicks [6].
- **ydotool delays and scrolling:** standalone `sleep` and command chaining were
  removed in v1.0.0, but key/type/click delay options remain. The manual documents
  delay-only key values and a no-op click for extra sleeps. The command source
  implements both `REL_WHEEL` and `REL_HWHEEL` through `mousemove --wheel`, even
  though the reviewed manpage omits that option. `--absolute` uses relative
  homing/movement rather than querying the cursor; its help warns that mouse
  acceleration must be disabled for correct absolute movement. `type` does not
  automatically accommodate a custom keyboard layout. The daemon needs access
  to `/dev/uinput`; these permissions and device recognition still matter [7].

See the [feature guide](features.md) for CrossMacro's visual workflows, the
[CLI reference](cli.md) for supported commands and runtime behavior, and the
[MCP reference](mcp.md) for local agent integration.

<a id="sources"></a>

<details>
<summary>Sources and review details</summary>

**Documentation-based comparison, reviewed 2026-09-26.** This is not runtime
benchmarking or a claim that every feature was exercised on every platform.
The desktop table distinguishes ready-made product features from scripting APIs.
The CLI table compares jobs exposed through each tool's own command surface,
not everything that can be assembled with a shell or third-party packages.
A desktop capability does not by itself imply a CLI command, or vice versa.

Research scope:

- **CrossMacro v1.5.0:** the repository's feature, CLI, MCP, and platform
  references [1].
- **AutoHotkey v2:** official documentation identifying version **2.0.28**,
  documentation commit `eddaa8c499f31fbea982e67538076515b666073a` [2, 3].
- **Hammerspoon:** official online API documentation and Getting Started guide
  retrieved on the review date; these are rolling documentation, not a
  version-pinned release snapshot [4, 5].
- **xdotool:** upstream README and manual at commit
  `14ca26a2c95035f9c635a6a11d075e8d7d0d6529` [6].
- **ydotool:** upstream README, manual, and command help/implementation at commit
  `708e96ff27e381a8c549418a9d34cdde12305317` [7]. The README warns that its manpage
  can lag command help; the scrolling entries in the CLI table use the command source.

The following primary documentation/source pages were retrieved for this review.
The readable AutoHotkey pages can change; their adjacent snapshot links are
pinned to the reviewed commit. “ND” applies to these core surfaces, not to an
exhaustive search of community extensions or all programs callable through a
foreign-function interface.

1. **CrossMacro v1.5.0:** [features](features.md), [CLI and runtime reference](cli.md),
   [platform limitations](cli.md#platform-limitations), [Linux window control](linux.md#linux-window-control),
   [Linux input/capture](linux.md), [Windows](windows.md), [macOS](macos.md), and [MCP](mcp.md).
2. **AutoHotkey core:** [quick reference](https://www.autohotkey.com/docs/v2/) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/index.htm)),
   [function index](https://www.autohotkey.com/docs/v2/lib/index.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/lib/index.htm)),
   [tutorial](https://www.autohotkey.com/docs/v2/Tutorial.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/Tutorial.htm)),
   [InputHook](https://www.autohotkey.com/docs/v2/lib/InputHook.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/lib/InputHook.htm)),
   [hotstrings](https://www.autohotkey.com/docs/v2/Hotstrings.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/Hotstrings.htm)),
   [SetTimer](https://www.autohotkey.com/docs/v2/lib/SetTimer.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/lib/SetTimer.htm)),
   [WinWaitActive](https://www.autohotkey.com/docs/v2/lib/WinWaitActive.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/lib/WinWaitActive.htm)),
   and [FAQ / UAC limitations](https://www.autohotkey.com/docs/v2/FAQ.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/FAQ.htm)).
3. **AutoHotkey screen operations:** [ImageSearch](https://www.autohotkey.com/docs/v2/lib/ImageSearch.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/lib/ImageSearch.htm)),
   [PixelGetColor](https://www.autohotkey.com/docs/v2/lib/PixelGetColor.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/lib/PixelGetColor.htm)),
   [PixelSearch](https://www.autohotkey.com/docs/v2/lib/PixelSearch.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/lib/PixelSearch.htm)),
   [DllCall](https://www.autohotkey.com/docs/v2/lib/DllCall.htm) ([reviewed snapshot](https://raw.githubusercontent.com/AutoHotkey/AutoHotkeyDocs/eddaa8c499f31fbea982e67538076515b666073a/docs/lib/DllCall.htm)),
   and Microsoft's [Win32 screen-capture example](https://learn.microsoft.com/en-us/windows/win32/gdi/capturing-an-image).
4. **Hammerspoon core:** [product/platform scope](https://www.hammerspoon.org/),
   [Getting Started](https://www.hammerspoon.org/go/), [API index](https://www.hammerspoon.org/docs/),
   [event taps and Secure Input](https://www.hammerspoon.org/docs/hs.eventtap.html),
   [event serialization/posting](https://www.hammerspoon.org/docs/hs.eventtap.event.html),
   [timers](https://www.hammerspoon.org/docs/hs.timer.html),
   [hotkeys](https://www.hammerspoon.org/docs/hs.hotkey.html),
   [clipboard](https://www.hammerspoon.org/docs/hs.pasteboard.html),
   [application watcher](https://www.hammerspoon.org/docs/hs.application.watcher.html),
   and [window-filter subscriptions](https://www.hammerspoon.org/docs/hs.window.filter.html).
5. **Hammerspoon screen operations:** [screen snapshots](https://www.hammerspoon.org/docs/hs.screen.html)
   and [images / colorAt](https://www.hammerspoon.org/docs/hs.image.html).
6. **xdotool:** [README and platform caveats](https://raw.githubusercontent.com/jordansissel/xdotool/14ca26a2c95035f9c635a6a11d075e8d7d0d6529/README.md)
   and [manual: mouse, windows, desktops, exec, SCRIPTS, and supported protocols](https://raw.githubusercontent.com/jordansissel/xdotool/14ca26a2c95035f9c635a6a11d075e8d7d0d6529/xdotool.pod).
7. **ydotool:** [README, v1.0.0 changes, daemon and layout requirements](https://raw.githubusercontent.com/ReimuNotMoe/ydotool/708e96ff27e381a8c549418a9d34cdde12305317/README.md),
   [manual: key, type, click and delay options](https://raw.githubusercontent.com/ReimuNotMoe/ydotool/708e96ff27e381a8c549418a9d34cdde12305317/manpage/ydotool.1.scd),
   and [mousemove help/implementation: absolute movement and both wheel axes](https://raw.githubusercontent.com/ReimuNotMoe/ydotool/708e96ff27e381a8c549418a9d34cdde12305317/Client/tool_mousemove.c).

</details>

[Back to documentation](README.md)
