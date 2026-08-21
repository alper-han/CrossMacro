# CLI Usage

Use the platform app executable as `crossmacro` when your install channel places
it on `PATH`. Portable builds may require running the executable directly from
its download folder.

For command syntax, run:

```bash
crossmacro --help
crossmacro <command> --help
```

The packaged manpage is also available at [`docs/man/crossmacro.1`](man/crossmacro.1).

Command syntax notation: `<value>` is required, `[value]` is optional, `a|b`
means choose one, and `...` marks a repeatable argument.

## Command overview

| Area | Commands |
| --- | --- |
| Help and startup | `--help`, `<command> --help`, `--version`, `--start-minimized` |
| Macro files | [`play`](#macro-files-playback-recording-and-diagnostics), [`record`](#macro-files-playback-recording-and-diagnostics), [`macro validate`](#macro-files-playback-recording-and-diagnostics), [`macro info`](#macro-files-playback-recording-and-diagnostics) |
| Inline automation | [`run --step ...`](#direct-run-examples), positional [`run <step> ...`](#direct-run-examples), [`run --file ...`](#direct-run-examples), [direct input commands](#direct-input-commands) |
| Runtime primitives | [`clipboard`](#clipboard-command), [`window`](#window-command), [`screen`](#screen-command), [`screenshot`](#screenshot-command) |
| User data | [`settings`](#settings-command), [`profile`](#profile-command), [`text-expansion`](#text-expansion-command), [`schedule`](#schedule-and-shortcut-commands), [`shortcut`](#schedule-and-shortcut-commands), [`trigger`](#trigger-command) |
| Diagnostics/runtime | [`doctor`](#macro-files-playback-recording-and-diagnostics), [`setup`](#temporary-wayland-input-setup), [`headless`](#gui-less-desktop-runtime), [`--headless`](#gui-less-desktop-runtime) |

Use the command-specific sections below for examples, option notes, and platform
behavior.

For desktop autostart, use `crossmacro --start-minimized`. When tray icon
support is available, CrossMacro starts hidden to tray; otherwise it starts as a
minimized window.

GUI startup also accepts platform launcher switches such as `--drm`, `--fbdev`,
`--tty`, `--display`, `--x11`, and `--wayland` (and macOS Finder `-psn_*`
tokens). These are startup switches, not options for a subcommand; unknown
commands and options still return a CLI parse error.

## GUI-less desktop runtime

The `headless` commands start CrossMacro's GUI-less desktop runtime for services
such as hotkeys, scheduler, shortcuts, and text expansion:

```bash
crossmacro headless
crossmacro --headless
```

This mode still requires a desktop session. It is not intended for display-less
server automation.

For MCP-capable hosts, `crossmacro mcp` starts the local stdio server. See
[`mcp.md`](mcp.md) for its tools, security model, and connection requirements.

## Temporary Wayland input setup

Flatpak and AppImage use direct device access on Wayland. Grant the current
session temporary access to `/dev/uinput` and
`/dev/input/event*` without opening the GUI:

```bash
crossmacro setup
```

The command reuses the GUI Quick Setup flow. Flatpak invokes the host helper
through `flatpak-spawn`; the host authorization command is selected as a usable
setuid `pkexec` and otherwise `run0`. AppImage uses the same `pkexec`/`run0`
selection directly. A GUI-launched setup requires a host graphical polkit agent;
Flatpak permissions do not provide one. When `pkexec` is selected, a setup run
from a terminal can use its textual authentication prompt. It never falls back to
an unvalidated `sudo` shell command. Use `--json` for scripts. The setup is
temporary and may need to be repeated after reboot or device re-enumeration.

`quick-setup` is accepted as an alias. On daemon-backed packages or sessions
where this setup is not applicable, the command returns environment error code
`5`; use `crossmacro doctor --verbose --json` for the active backend details.

## Direct input commands

The following one-step commands execute input directly, without a `run` wrapper:

| Group | Commands | Example |
| --- | --- | --- |
| Mouse | `move`, `click`, `down`, `up`, `scroll` | `crossmacro move abs 200 200` |
| Keyboard | `key`, `tap` | `crossmacro key down CTRL` |
| Text and timing | `type`, `delay` | `crossmacro type "hello world"` |

They use the same compiler, coordinate spaces, button/key names, and runtime
preflight as `run`. Each command accepts `--dry-run`, `--json`, and
`--log-level`. They do not support variables, conditions, loops, or multiple
steps; use [`run`](#direct-run-examples) for those workflows.

`mouse position <x_variable> <y_variable>` is not a top-level CLI command. It is
a `run` script step that stores the current cursor coordinates in run-local
variables.

### Mouse commands

`move` accepts `abs`, `rel`, `rel-logical`, or `rel-raw` coordinates. `click`,
`down`, and `up` accept a mouse button; adding `current` uses the live cursor
position. `scroll` accepts `up`, `down`, `left`, or `right` and an optional
count.

```bash
crossmacro move abs 200 200
crossmacro move rel-logical 10 0
crossmacro click current left
crossmacro scroll down 3
```

### Keyboard and text commands

`key down|up <key>` sends one key transition, `tap <combo>` sends a key
combination, and `type <text>` types text as one shell argument.

```bash
crossmacro key down CTRL
crossmacro key up CTRL
crossmacro tap CTRL+ALT+T
crossmacro type "hello world"
```

### Delay command

`delay <milliseconds>` waits for a fixed duration. `delay random <min> <max>`
or `delay random <min>..<max>` chooses a duration in the inclusive range.

```bash
crossmacro delay 50
crossmacro delay random 20 80
```

The `type` text must be one shell argument; quote it when it contains spaces.
Absolute and logical-relative moves require the same position/input capability
checks as equivalent `run` steps. `--dry-run` validates the command without
sending input.

## Macro files, playback, recording, and diagnostics

These commands do not open the editor:

```bash
crossmacro macro validate ./demo.macro
crossmacro macro info ./demo.macro --json
crossmacro play ./demo.macro --speed 1.25 --repeat 3
crossmacro play ./demo.macro --speed 10 --motion-mode strict-speed --motion-rate 600 --motion-error-px 2
crossmacro play ./demo.macro --dry-run
crossmacro record --output ./recorded.macro --mode auto --duration 10
crossmacro doctor --verbose --json
```

- `macro validate` reads the file and checks syntax and playback compatibility;
  it never sends input.
- `macro info` reports metadata such as event count, duration, coordinate mode,
  and validation warnings. A validation error is returned with exit code `4`.
- `play` supports `--speed`, `--motion-mode precision|strict-speed`,
  `--motion-rate`, `--precision-motion-rate`, `--motion-error-px`, `--loop`,
  `--repeat`, `--repeat-delay-ms`, `--countdown`, `--timeout`, and `--dry-run`.
  Precision is the default and reduces effective speed to retain every captured
  absolute point. Strict speed keeps the requested duration, uses the selected
  report ceiling, and emits a warning when the requested pixel-error budget
  cannot be achieved; `--motion-error-px` accepts `0.25..500` and defaults to `2`.
- `run` supports repeatable `--step`, positional step commands, `--file`,
  repeatable `--asset <name> <png-path>` options for image steps, `--speed`,
  `--countdown`, `--timeout`, and `--dry-run`.
- `record` supports `--output`/`-o`, `--mouse`, `--keyboard`, `--mode`,
  `--skip-initial-zero`, and `--duration`.
- `doctor` supports `--verbose` and checks the session, input injection, screen
  capture, daemon, and direct-device paths. On Linux, daemon readiness and
  direct-device readiness are reported separately; one can pass while the other
  fails.

## Clipboard command

The first-class clipboard command wraps the text clipboard service directly:

```bash
crossmacro clipboard get
crossmacro clipboard get --json
crossmacro clipboard set "hello"
crossmacro clipboard set --file ./message.txt
crossmacro clipboard clear
```

- `clipboard get` prints the clipboard text directly in text mode. JSON output
  includes the value in `data.value`.
- `clipboard set <text>` replaces clipboard text with the provided argument.
- `clipboard set --file <path>` reads UTF-8 text from a file and copies it.
- `clipboard clear` replaces clipboard text with an empty value.

Unsupported platforms or sessions return a non-zero environment error instead
of silently succeeding.

## Window command

The first-class window command wraps the current runtime window-management
capabilities:

```bash
crossmacro window active --json
crossmacro window list --json
crossmacro window search --title Firefox
crossmacro window search --class Code
crossmacro window wait --title "Download complete" --timeout-ms 10000
crossmacro window focus --address 0x1234
crossmacro window focus --title Firefox
crossmacro window focus --class Code
crossmacro window close --address 0x1234
crossmacro window close --title "Untitled"
crossmacro window move --active 100 100
crossmacro window resize --active 1280 720
crossmacro window center --active
crossmacro window maximize --active
crossmacro window fullscreen --active
crossmacro window float --active
crossmacro window workspace get
crossmacro window workspace switch 2
crossmacro window workspace move-active 2
crossmacro window workspace move-window --address 0x1234 2
```

Title and class selectors use case-insensitive substring matching. Address
selectors match the compositor/window-manager address exactly. Operations that
target a title/class use the first matching window returned by the active
backend; use `--address` when more than one match is possible. `window wait`
polls, and its CLI `--timeout-ms 0` behavior differs from the script form as
documented in the [Detailed CLI and Runtime Reference](#detailed-cli-and-runtime-reference).
Unsupported platforms return a clear non-zero environment error.

## Screen command

The first-class screen command is an ergonomic wrapper for pixel/color reads:

```bash
crossmacro screen pixel 500 300
crossmacro screen pixel --relative 0 0 --json
crossmacro screen wait-color 500 300 00FF00
crossmacro screen search-color 0 0 1920 1080 FF0000 --tolerance 26 --json
crossmacro screen search-image ./button.png --similarity 0.95
crossmacro screen wait-image ./ready.png --timeout-ms 10000
crossmacro screen image-click ./button.png --button right
```

- `screen pixel <x> <y>` reads one pixel and includes coordinates/color in JSON
  `data`.
- `screen pixel --relative <dx> <dy>` samples relative to the current cursor; it
  returns an unsupported error if no mouse position provider is available.
- `screen wait-color <x> <y> <RRGGBB> [--timeout-ms <n>]` retries a point
  read every 50 ms until the color appears or its total five-second default
  budget expires.
- `screen search-color <x1> <y1> <x2> <y2> <RRGGBB> [--timeout-ms <n>] [--tolerance <0..255>]`
  retries the end-exclusive region `[x1, x2) x [y1, y2)` on the same schedule.
- `screen search-image <image-path>` is a one-frame PNG search. It accepts
  `--region <x> <y> <width> <height>`, `--similarity <0..1>`, and
  `--matchmode <auto|first|best>`.
- `screen wait-image <image-path>` and `screen image-click <image-path>` retry
  for five seconds by default and accept `--timeout-ms <n>`; image-click also
  accepts `--button <left|right|middle>`. The default button is `left`.
- Image commands accept finite similarity values from `0.0` to `1.0`, defaulting
  to `0.95`. New commands use automatic matching: native weighted-SAD is tried
  first, then a bounded pyramid/correlation and scale search when needed.
  `--matchmode auto` makes that default explicit.
- `--matchmode first` and `--matchmode best` select deterministic row-major or
  complete-region SAD behavior while it fits the work budget. On a larger search,
  they transparently use the bounded automatic candidate pipeline instead of
  failing with a resource-limit error. They are optional advanced modes;
  automatic is the default.
- `search-image` returns after its one match attempt. `wait-image` and
  `image-click` require two consecutive compatible matches (centre within 2
  logical pixels and size within 1 pixel); a no-match or changed target resets
  the consensus. `--timeout-ms 0` performs one immediate check instead.
- Image files must be native 8-bit PNG files. Other formats, including JPG, are
  not imported.

The wait and repeated-search forms use the same 50 ms cadence. Their complete
timing, timeout, and no-match behavior is defined in the [Detailed CLI and
Runtime Reference](#detailed-cli-and-runtime-reference) below.

On macOS, screen capture requires Screen Recording permission. On Wayland,
capture and coordinates depend on the compositor/provider and use the global
virtual desktop; see the [platform details](#platform-limitations) before
automating across monitors.

## Screenshot command

The screenshot command captures the current screen frame and writes a PNG file:

```bash
crossmacro screenshot --output ./shot.png
crossmacro screenshot -o ./shot.png --json
crossmacro screenshot --clipboard
crossmacro screenshot --output ./shot.png --clipboard
crossmacro screenshot --output ./crop.png --region 100 100 800 600
```

- `--output`/`-o` writes a PNG file and overwrites the target file.
- `--clipboard` copies the captured PNG image to the system clipboard.
- At least one destination (`--output` or `--clipboard`) is required. Both can be
  used together from one capture.
- `--region <x> <y> <width> <height>` captures a positive-size region.
- JSON output includes output path, dimensions, format, provider, and whether a
  region or clipboard destination was requested.

Screenshot capture uses the same platform frame providers as screen pixel reads.
Unsupported platforms or sessions return a non-zero environment error.

## Profile command

The profile command manages the same profile registry used by the GUI:

```bash
crossmacro profile list
crossmacro profile list --json
crossmacro profile current
crossmacro profile create work
crossmacro profile switch work
crossmacro profile rename work office
crossmacro profile delete office --force
```

- `profile list` prints registered profiles and marks the active profile in JSON
  `data.profiles[].isActive`.
- `profile current` prints the active profile.
- `profile create <name>` creates a new profile with default config files.
- `profile switch <name-or-id>`, `rename`, and `delete` resolve either stable id
  or display name case-insensitively.
- `profile delete` requires `--force` and still respects backend protections such
  as refusing to delete the active or default profile.

Profile archive `export`/`import` is intentionally deferred until portable backup
and restore semantics are specified.

## Text expansion command

The text expansion command manages stored expansion entries without typing or
pasting into the desktop:

```bash
crossmacro text-expansion list
crossmacro text-expansion list --profile work --json
crossmacro text-expansion add ":mail" "me@example.com"
crossmacro text-expansion add ":sig" "Regards" --method CtrlShiftV
crossmacro text-expansion add ":sig" "Regards" --insertion-mode DirectTyping --direct-typing-method CompatibleKeyByKey
crossmacro text-expansion remove ":mail"
crossmacro text-expansion enable ":mail"
crossmacro text-expansion disable ":mail"
crossmacro text-expansion test ":mail"
```

- `--profile <name-or-id>` temporarily loads that profile's text-expansion
  storage for the operation and restores the active profile storage afterward;
  it does not switch the active profile.
- `add` rejects duplicate triggers case-insensitively.
- `test` only resolves and reports the matching expansion; it never sends input.
- `--method` accepts `CtrlV`, `CtrlShiftV`, or `ShiftInsert`.
- `--insertion-mode` accepts `Paste` or `DirectTyping`.
- `--direct-typing-method` accepts `FastBatch` or `CompatibleKeyByKey`.

`text` is accepted as a short alias for `text-expansion`.

## Schedule and shortcut commands

Schedule commands manage active-profile scheduled macro tasks:

```bash
crossmacro schedule list --json
crossmacro schedule run <task-id>
crossmacro schedule add --name Daily --macro ./demo.macro --interval 10m
crossmacro schedule add --name Once --macro ./demo.macro --at "2026-08-07T18:00:00"
crossmacro schedule add --name Weekly --macro ./demo.macro --weekly mon,wed --time 09:30
crossmacro schedule edit <task-id> --name Office --speed 1.25 --enabled true
crossmacro schedule remove <task-id>
crossmacro schedule enable <task-id>
crossmacro schedule disable <task-id>
crossmacro schedule next <task-id> --json
```

- `add` requires `--name` and `--macro`.
- `--speed` sets the playback speed for that scheduled macro.
- `next` reports the task's next run time and does not save changes.

Shortcut commands manage active-profile shortcut-bound macro tasks:

```bash
crossmacro shortcut list --json
crossmacro shortcut run <task-id>
crossmacro shortcut add --name Demo --macro ./demo.macro --hotkey Ctrl+Alt+D
crossmacro shortcut add --name Loop --macro ./loop.macro --hotkey F7 --loop --repeat 3
crossmacro shortcut add --name Browser --macro ./browser.macro --hotkey Ctrl+Alt+B --window-rule WindowClass Equals org.mozilla.firefox
crossmacro shortcut edit <task-id> --repeat-delay-ms 250
crossmacro shortcut edit <task-id> --random-repeat-delay 100 300
crossmacro shortcut edit <task-id> --window-rule ProcessName Contains chromium
crossmacro shortcut edit <task-id> --clear-window-rules
crossmacro shortcut bind <task-id> Ctrl+Shift+M
crossmacro shortcut remove <task-id>
crossmacro shortcut enable <task-id>
crossmacro shortcut disable <task-id>
```

- `add` requires `--name`, `--macro`, and `--hotkey`.
- `bind` is shorthand for replacing a shortcut task's hotkey.
- `--speed`, `--loop`, `--repeat`, `--repeat-delay-ms`,
  `--random-repeat-delay`, `--run-while-held`, and `--enabled` mirror the GUI
  shortcut playback/task options.
- Repeat `--window-rule <field> <match-mode> <value>` to limit the hotkey to
  focused windows. Supported fields are `WindowClass`, `WindowTitle`, and
  `ProcessName`; match modes are `Equals`, `Contains`, and `Regex`.
- Rules use OR semantics. An edit with `--window-rule` replaces the shortcut's
  complete rule list; `--clear-window-rules` removes all rules and restores
  global shortcut behavior. Explicit `shortcut run` ignores window rules.

## Trigger command

Trigger commands manage active-profile window-match trigger tasks:

```bash
crossmacro trigger list --json
crossmacro trigger add --name "VSCode Focus" --field WindowClass --match-mode Contains --value Code --action SwitchProfile --profile dev --fire-mode OnceOnChange
crossmacro trigger add --name "Firefox Focus" --field WindowTitle --match-mode Regex --value ".*Firefox.*" --action RunMacro --macro ./fx.macro --debounce-ms 200 --cooldown-ms 1000
crossmacro trigger edit <task-id> --debounce-ms 250
crossmacro trigger remove <task-id>
crossmacro trigger enable <task-id>
crossmacro trigger disable <task-id>
```

- `add` requires `--name`, `--field`, `--match-mode`, and `--action`.
- `--field` accepts `WindowClass`, `WindowTitle`, `Workspace`, `ProcessName`, or `None`.
- `--match-mode` accepts `Equals`, `Contains`, or `Regex`.
- `--action` accepts `SwitchProfile` or `RunMacro`.
- `--profile` sets the target profile to switch to, and `--macro` sets the macro path to run.
- `--fire-mode` accepts `OnceOnChange`, `EveryMatch`, `OnEnter`, or `OnExit`.
- `--cooldown-ms` and `--debounce-ms` are integer millisecond values; `0` clears
  the interval, while a positive value enables it.

## Settings command

Settings expose stable public keys rather than raw C# property names:

```bash
crossmacro settings get
crossmacro settings get --all --json
crossmacro settings get ui.theme
crossmacro settings set ui.theme Nord
crossmacro settings list-keys --json
crossmacro settings reset updates.checkForUpdates
```

- `settings get [key]` preserves the existing behavior. Without a key it prints
  all supported keys; `settings get --all` is the explicit all-settings form.
- `settings list-keys` prints supported public keys.
- `settings reset <key>` resets one supported key to its default value.
- Playback keys are `playback.speed`, `playback.loop`, `playback.loopCount`,
  `playback.loopDelayMs`, and `playback.countdownSeconds`. Recording keys are
  `recording.mouse`, `recording.keyboard`, `recording.forceRelative`, and
  `recording.skipInitialZeroZero`. Other keys are `logging.level`,
  `textExpansion.enabled`, `ui.theme`, `ui.language`, `ui.trayIcon`,
  `ui.startMinimized`, and `updates.checkForUpdates`.
- `screen.portalRestoreToken` is status/reset only. `get` reports `set` or
  `empty`, and `reset` clears it; the raw token is never printed.
- Boolean values accept `true`, `false`, `1`, `0`, `yes`, `no`, `on`, and `off`.
  Numeric timing values are milliseconds unless the key name says seconds;
  `logging.level` accepts `Debug`, `Information`, `Warning`, or `Error`.

## Direct run examples

`crossmacro run` executes inline steps without a `.macro` file. It accepts
repeatable `--step` arguments, a `--file`, or the legacy positional step form:

```bash
crossmacro run --step "move abs 800 400" --step "click left" --dry-run
crossmacro run --step "move abs 800 400" --step "click current left"
crossmacro run --step "delay random 40..90" --step "click left"
crossmacro run \
  --step "set n=3" \
  --step 'repeat $n {' \
  --step "click left" \
  --step "delay random 20 50" \
  --step "}"
crossmacro run --step 'shell "notify-send done" 1 250 5000'
crossmacro run --file ./steps.txt --json
crossmacro run move rel 100 0 delay 40 click left
crossmacro run --asset button ./button.png --step 'waitimage button found x y timeout 5000'
```

The examples above use Bash/Zsh quoting. Single quotes preserve `$variables`
and braces there. PowerShell also preserves `$` in single-quoted strings, so
the same `--step 'repeat $n {'` form can be used. In `cmd.exe`, `$` is not a
variable-expansion character; use double quotes to group a complete step, for
example:

```text
crossmacro run --step "move abs $found_x $found_y"
```

`notify-send` is a Linux-only example. Use a portable command such as
`shell "echo done" 1 250 5000` when the macro must also run on Windows or
macOS. Use `--step` when a step contains braces, `$variables`, or options that
would be ambiguous in the positional form.

Image steps use named assets supplied with repeatable `--asset <name> <png-path>`;
the name must match `[A-Za-z_][A-Za-z0-9_]*` and is referenced by
`imagesearch`, `imageclick`, or `waitimage`. Assets are loaded and validated
before playback and are not written into the user's macro store.

`shell "<command>" [retries] [backoff_ms] [timeout_ms]` runs a command through
the platform shell (`/bin/sh -c` on Unix, `cmd.exe /S /C` on Windows). `retries`
is the number of extra attempts after the first. `backoff_ms` is the delay before
retrying. `timeout_ms` applies to each attempt; `0` means no per-attempt timeout.
Variables inside command and stdin payloads are resolved before execution. Quote
command payloads when using numeric options so command arguments are not confused
with retry/backoff/timeout values.

Shell result capture and stdin are available with explicit forms:

```text
shell capture "<command>" exit_var stdout_var stderr_var [retries] [backoff_ms] [timeout_ms]
shell input "<stdin text>" "<command>" [retries] [backoff_ms] [timeout_ms]
shell capture-input "<stdin text>" "<command>" exit_var stdout_var stderr_var [retries] [backoff_ms] [timeout_ms]
```

Use `_` for any capture target you want to ignore. Normal `shell` and `shell
input` fail when the command exits non-zero after retries are exhausted. Capture
modes store the exit code, stdout, and stderr variables and continue even when the
exit code is non-zero, so scripts can branch on the captured code. Captured
stdout/stderr are capped at 65536 characters per stream. Shell steps execute
arbitrary commands, so only run trusted macros. Flatpak builds run each step in a
stricter nested sandbox: runtime and `/app` tools remain available, while the
parent application's host files, devices, D-Bus permissions, portals, and host
command channel are not inherited. Native and AppImage builds use the normal
platform shell. Use `$$NAME` when you want the shell to receive `$NAME` literally
instead of resolving a CrossMacro variable.

## Runtime clipboard, window, and screen steps

CrossMacro's screen-reading commands use the active platform capture provider.
Provider availability, permissions, and Wayland limitations are summarized in the
[Detailed CLI and Runtime Reference](#detailed-cli-and-runtime-reference) below;
Linux backend details are in [`docs/linux.md`](linux.md).

```bash
pixelcolor 500 300 mycolor
pixelcolor rel 0 0 underCursor
clipboard get clipText
clipboard set "new clipboard text"
screenshot output "./shot.png"
screenshot clipboard
screenshot region 100 100 800 600 output "./crop.png" clipboard
window active title activeTitle
window search title "Firefox" firefoxAddress
window focus address 0x1234
window close title "Untitled"
window move 100 100
window resize 1280 720
window maximize active
window getdesktop workspaceName
window setdesktop 2
window setdesktopforwindow address 0x1234 2
waitcolor 500 300 00FF00 5000 wait_ok
waitcolor 500 300 $mycolor 5000 wait_ok
pixelsearch 0 0 1920 1080 FF0000 found found_x found_y timeout 5000 tolerance 26
imagesearch button found found_x found_y similarity 0.95
imageclick button clicked click_x click_y button right similarity 0.95
waitimage ready found found_x found_y timeout 10000
mouse position mouse_x mouse_y
```

- `pixelcolor <x> <y> [var]` samples one pixel at an absolute position.
  `pixelcolor rel <dx> <dy> [var]` samples relative to the current cursor.
- `waitcolor <x> <y> <RRGGBB|$var> [timeout_ms] [result_var]` retries the
  point read every 50 ms. When `result_var` is present, timeout writes `false`
  and playback continues; without it, timeout is fail-fast.
- `pixelsearch <x1> <y1> <x2> <y2> <RRGGBB|$var> [found_var var_x var_y|var_x var_y] [timeout <milliseconds>] [tolerance <0..255>]`
  retries the end-exclusive region `[x1, x2) x [y1, y2)` every 50 ms. When
  `found_var` is present, a no-match/timeout writes `false` plus `-1, -1`
  coordinates and playback continues; the `var_x var_y` form is fail-fast.
- `clipboard get <var>` stores current clipboard text in a runtime variable.
- `clipboard set <text>` replaces clipboard text after variable substitution.
- `mouse position <x_var> <y_var>` stores the live global cursor coordinates
  as signed integer runtime variables. The two destination names must differ.
- `screenshot [region <x> <y> <width> <height>] [output <path>] [clipboard]`
  captures a screen frame. At least one destination, `output` or `clipboard`, is
  required; `output` overwrites the target PNG path.
- `window active title|class|address|fullscreen|maximize|float|pinned|hidden|geometry <var>`
  stores active-window fields.
- `window search title|class <term> <var>` stores the first matching window
  address, using substring matching.
- `window wait title|class <term> [timeout_ms] <var>` polls for a matching window
  and stores its address or an empty value.
- `window focus active|title|class|address <value>` and
  `window close active|title|address <value>` mutate matching windows.
- `window move <x> <y>`, `window resize <width> <height>`, `window center active`,
  `window maximize active`, `window fullscreen active`, and `window float active`
  mutate the active window.
- `window getdesktop <var>`, `window setdesktop <workspace>`, and
  `window setdesktopforwindow active|address <addr> <workspace>` manage workspaces.
- `imagesearch [<x1> <y1> <x2> <y2>] <ImageName> [found_var x_var y_var] [similarity <0..1>] [matchmode <auto|first|best>]`
  searches a named PNG asset once. Region bounds are end-exclusive.
- `<ImageName>` is the embedded macro asset name, not a filesystem path; for
  example, an imported `button.png` may be referenced as `button`.
- `imageclick [<x1> <y1> <x2> <y2>] <ImageName> [found_var x_var y_var] [button <left|right|middle>] [similarity <0..1>] [matchmode <auto|first|best>] [timeout <milliseconds>]`
  waits for two compatible matches, then clicks the centre. The default button
  is `left`.
- `waitimage [<x1> <y1> <x2> <y2>] <ImageName> [found_var x_var y_var] [timeout <milliseconds>] [similarity <0..1>] [matchmode <auto|first|best>]`
  waits for two compatible image matches.

Target colors can be a canonical six-digit `RRGGBB` value with no `#`, or a
`$var` reference to a color previously written by `pixelcolor`; bare variable
names are not accepted in target color positions. Hex values are written back in
uppercase. `pixelsearch` defaults to exact matching when tolerance is omitted;
non-zero tolerance allows that many shades of difference per RGB channel.
Image matching, timeout, polling, and result-variable behavior is defined in
the [Detailed CLI and Runtime Reference](#detailed-cli-and-runtime-reference)
below.

## Other run step commands

Additional direct-run steps include:

- `move abs <integer|$variable> <integer|$variable>`, `move rel <integer|$variable> <integer|$variable>`, `move rel-logical <integer|$variable> <integer|$variable>`, and `move rel-raw <integer|$variable> <integer|$variable>`
- `mouse position <x_variable> <y_variable>`
- `click <button>`, `down <button>`, and `up <button>`
- `click current <button>`, `down current <button>`, and `up current <button>`
- `scroll <up|down|left|right> [count]`
- `key down <key>`, `key up <key>`, and `tap <combo>`
- `type <text>`
- `delay <ms>`, `delay random <min> <max>`, and
  `delay random <min>..<max>`
- `shell "<command>" [retries] [backoff_ms] [timeout_ms]`
- `shell capture "<command>" exit_var stdout_var stderr_var [retries] [backoff_ms] [timeout_ms]`
- `shell input "<stdin text>" "<command>" [retries] [backoff_ms] [timeout_ms]`
- `shell capture-input "<stdin text>" "<command>" exit_var stdout_var stderr_var [retries] [backoff_ms] [timeout_ms]`
- `set <name> <value>` or `set <name>=<value>`
- `inc <name> [amount]` and `dec <name> [amount]`
- `mul <name> [amount]` and `div <name> [amount]`
- `repeat <count> { ... }`
- `if <left> <op> <right> { ... } else { ... }`
- `while <left> <op> <right> { ... }`
- `for <var> from <start> to <end> [step <n>] { ... }`
- `break`, `continue`, and `}`

The `screenshot [region ...] [output ...] [clipboard]` step is documented in
the Runtime clipboard, window, and screen section above.

Numeric arguments of `repeat`, `for` (`from`/`to`/`step`), `if`, and `while`
accept one binary arithmetic expression, for example `repeat $count / 2 {`.
Each operand is an integer literal or a `$variable`; operators are `+`, `-`,
`*`, and `/`. Arithmetic is integer-only, division truncates, division by zero
is an error, and expressions cannot be chained or parenthesized. For anything
more complex, compute into a temporary variable first (`set tmp <value>`, then
`inc`/`dec`/`mul`/`div` steps) and use `$tmp` as the block argument; this
set-then-use form is always available.

Move coordinates may be integer literals or `$variable` references. Variable
coordinates are resolved immediately before the move executes, so screen-reading
results such as `pixelsearch` coordinates can be used in subsequent moves. The
resolved values must be valid integers; absolute moves still require an input
provider with absolute-coordinate support.

```text
pixelsearch 0 0 1920 1080 FF0000 found found_x found_y tolerance 5
if $found == true {
  move abs $found_x $found_y
  click left
}
```

The macro editor accepts the same values in the X and Y fields for mouse move,
click, hold, and release actions. When invoking the CLI from a shell, quote steps
containing `$variable` references so the shell does not expand them first, for
example `--step 'move abs $found_x $found_y'`.

`mouse position <x_variable> <y_variable>` captures a single live global cursor
sample without moving the pointer. It can feed later moves, conditions, and
loops; for example, `move abs $mouse_x $mouse_y` restores a previously saved
position. It does not create a separate background cursor, so any later move or
click still affects the user's system pointer. The step fails when the active
platform session cannot provide a global cursor position.

The coordinate-space differences between `move rel`, `move rel-raw`, and
`move rel-logical` are defined in the [Detailed CLI and Runtime
Reference](#detailed-cli-and-runtime-reference) below.

Use `--dry-run` to parse, compile, and validate a direct-run command without
sending input.

## Detailed CLI and Runtime Reference

This section is the compact contract for scripting. Command-specific sections
above show the common workflows; `--help` remains the authoritative syntax for
the installed build.

### Global options and JSON output

The following options are available on CLI commands that produce a result:

```bash
crossmacro <command> --help
crossmacro <command> --json
crossmacro <command> --log-level Debug
crossmacro --version
```

- `--json` writes one envelope to stdout with `status`, `code`, `message`,
  `data`, `warnings`, and `errors`. `data` is command-specific and can be
  `null`; do not parse human-readable `message` text as a schema.
- In text mode, successful data is printed to stdout and failures to stderr.
  `clipboard get` is intentionally special: text mode prints only the clipboard
  value, without the status envelope.
- `--log-level` accepts `Verbose`, `Debug`, `Information`, `Warning`, `Error`,
  or `Fatal`. Keep logs separate from machine-readable JSON output.
- This runtime option intentionally has a wider value set than the persisted
  `logging.level` profile setting: the setting accepts only `Debug`,
  `Information`, `Warning`, or `Error`.
- `--help` and `--version` exit with code `0`. A standalone option such as
  `--json` without a command is an argument error.

- `crossmacro-git` development builds append their short source revision to
  `--version`; include that revision in bug reports.
Example JSON shape:

```json
{
  "status": "ok",
  "code": 0,
  "message": "Pixel 500,300: 1C7B41",
  "data": { "x": 500, "y": 300, "color": "1C7B41" },
  "warnings": [],
  "errors": []
}
```

### Exit codes

| Code | Meaning |
| ---: | --- |
| `0` | Success, including `--help`, `--version`, and a successful dry-run. |
| `2` | Invalid command-line arguments or parse error. |
| `3` | Macro/file read, write, or path error. |
| `4` | Macro or run-script validation error. |
| `5` | Environment, permission, backend, or runtime-readiness error. |
| `6` | Runtime playback, capture, matching, or mutation error. |
| `130` | Cancelled by Ctrl+C or another cancellation request. |

For automation, branch on `code` and use `errors`/`warnings` for diagnostics;
`message` is intended for a short human summary.

### Macro playback and recording

`play` and `run` have two different timeout layers:

```bash
crossmacro play ./demo.macro --timeout 30
crossmacro run --step 'waitcolor 500 300 00FF00 10000 found' --timeout 20
```

`--timeout` is in seconds and cancels the whole command. Screen/window step
timeouts inside the macro are independent and are in milliseconds. `--dry-run`
does not require input permissions and does not execute shell or input steps.

`record --duration` is in seconds. With `0`, recording continues until cancelled;
the output is saved only when at least one event was captured. `--mode auto` and
`--mode absolute` may fall back to relative recording when the current backend
cannot expose absolute coordinates; the result and warnings identify the mode
that was actually saved.

### Mouse button names and aliases

For script/`run` mouse steps, these aliases are equivalent:

| Canonical | Aliases |
| --- | --- |
| `left` | `l` |
| `right` | `r` |
| `middle` | `m` |
| `side1` | `side`, `back` |
| `side2` | `extra`, `forward` |

`scroll up|down|left|right [count]` is a separate wheel command. `click`,
`down`, and `up` use the last resolved mouse position; add `current` (for
example `click current left`) to force the live cursor position and ignore a
previous absolute move's coordinates. First-class `screen image-click` and the
script `imageclick` intentionally accept only `left`, `right`, and `middle`.

### Conditions and variable values

Variables use the pattern `[A-Za-z_][A-Za-z0-9_]*`:

```text
set found=false
set count=3
if $found == false {
  set count=0
}
if $count > 0 {
  click left
}
```

- Assign with `set name value` or `set name=value`; reference with `$name`.
  Use `$$NAME` when a literal dollar-prefixed value must be passed through.
- Supported operators are `==`, `!=`, `>`, `>=`, `<`, and `<=`.
- Equality compares colors, integers, booleans, and then exact strings. Relational
  operators require two integer values.
- There is no implicit truthiness and no `&&`/`||` expression syntax. Write
  `if $found == true` or `if $count > 0`; `if $found` is not a valid condition.
- Screen result-variable forms use `true`/`false`; a no-match coordinate is
  `-1,-1`. A missing variable or invalid numeric value is a runtime error.

### Screen search versus wait semantics

Search commands inspect one captured frame. Wait commands poll until a condition
is met or their total wait budget expires.

| Operation | Behavior | Default timeout |
| --- | --- | --- |
| `screen pixel`, `pixelcolor` | One point read with an internal five-second capture safety deadline. | Not user-configurable |
| `screen search-image`, `imagesearch` | One frame/template match with the same capture safety deadline. | Not user-configurable |
| `screen wait-color`, `waitcolor` | Poll every 50 ms until a color matches. | 5 s |
| `screen search-color`, `pixelsearch` | Poll every 50 ms until a region contains a match. | 5 s |
| `screen wait-image`, `waitimage` | Poll every 50 ms and require two compatible image matches. | 5 s |
| `screen image-click`, `imageclick` | Poll every 50 ms, require two compatible matches, then click once. | 5 s |
| `window wait` (CLI or script) | Poll window state every 200 ms. | 5 s |

### Result semantics

The following table is the single no-match/timeout reference for screen search
and wait operations. Repeating commands use a total timeout budget across all
attempts; only a normal no-match is retried.

| Surface | No match or timeout | Process/result behavior |
| --- | --- | --- |
| `screen search-color` | Error | Exit `6`; JSON uses the error envelope and text mode writes the failure to stderr. |
| `screen search-image` | Normal result | Exit `0`; JSON contains `data.found: false` (text mode reports no match). |
| `screen wait-color` | Error on total-budget expiry | Exit `6`; JSON uses the error envelope and text mode writes the failure to stderr. |
| `screen wait-image` | Normal result on total-budget expiry | Exit `0`; JSON contains `data.found: false` (text mode reports no match). |
| `screen image-click` | Error on total-budget expiry | Exit `6`; no click is sent when the template is not found. |
| Script search/wait with result variables | Continue with `false` and `-1,-1` for coordinate results; `waitcolor` stores only `false`. | The step itself is not a failure; later steps still run. |
| Script search/wait without result variables | Error (fail-fast) | Playback stops and `run` reports a runtime error. |

Use `waitcolor`, `pixelsearch`, `waitimage`, or `imageclick` when the screen may
change after the command starts.

### Timeout units and defaults

Use the unit encoded by the option name or command:

| Scope | Syntax | Unit and default |
| --- | --- | --- |
| Whole `play`/`run` command | `--timeout <n>` | Seconds; `0` means no command deadline. |
| CLI repeating screen operation | `--timeout-ms <n>` | Milliseconds; the four repeating screen commands default to 5000. |
| Script repeating screen operation | `timeout <n>` or positional wait timeout | Milliseconds; the four repeating screen commands default to 5000. |
| Shell step | `[timeout_ms]` | Milliseconds per attempt; `0` means no per-attempt limit. |
| Shell retry | `[retries] [backoff_ms]` | Extra attempts after the first, with millisecond backoff. |
| Schedule interval | `--interval 10s|5m|2h` | Seconds, minutes, or hours. |
| Recording/schedule countdown | `--duration`, `--countdown` | Seconds. |
| Trigger debounce/cooldown | `--debounce-ms`, `--cooldown-ms` | Milliseconds. |

`waitcolor`, `pixelsearch`, `waitimage`, and `imageclick` timeouts are total
budgets across all polls. A value of `0` performs one immediate check; for image
operations it accepts that one successful frame without a second-frame wait.
Use Ctrl+C or an outer command timeout to cancel an operation explicitly.

`window wait` has one compatibility detail: the CLI form treats
`--timeout-ms 0` as one immediate poll, while the script form treats a missing
or non-positive `timeout_ms` as its 5000 ms default. Use a positive script
timeout when an explicit window-wait budget is required.

### Coordinate spaces and multi-monitor behavior

- CLI screen coordinates and `move abs` use the logical virtual desktop. A
  search region is two corners with end-exclusive right/bottom edges:
  `[x1, x2) x [y1, y2)`; color searches normalize the two corners, while
  image/script regions must produce a positive right/bottom extent. The editor
  displays `left`, `top`, `width`, `height` and converts the latter to
  `right = left + width`, `bottom = top + height`.
- `screen pixel --relative` and `pixelcolor rel` add a delta to the live cursor.
  `move rel-raw`/`move rel` preserve raw device-relative behavior;
  `move rel-logical` uses logical desktop pixels and needs a known cursor
  position (an absolute move, a position provider, or the initial anchor).
- Absolute moves and image clicks use an absolute-capable backend when one is
  available. Otherwise image-click may use a current-position relative
  fallback; if neither position capability exists, it returns an environment
  error.
- On Wayland, capture regions may be stitched from intersecting monitors,
  including monitors with negative virtual coordinates. Areas between monitors
  are voids, not black pixels; matching ignores them and a template crossing a
  void is rejected. Portal-based sessions require every monitor containing the
  requested pixels to be selected in the portal picker.
- Keep search regions on the intended monitor. Coordinates are global desktop
  coordinates, not coordinates relative to the CrossMacro window or a monitor
  width/height pair.

### Schedule, shortcut, and trigger constraints

- Schedule, shortcut, and trigger tasks belong to the active profile. Use
  `list --json` first and pass the returned GUID to `run`, `edit`, `remove`,
  `enable`, `disable`, or `next`.
- A schedule accepts at most one of `--interval`, `--at`, or `--weekly`. If no
  form is supplied, it keeps the default 30-second interval. Interval values
  are positive integers with optional `s`, `m`, or `h` suffixes (no suffix means
  seconds). `--at` is parsed with the invariant-culture .NET date/time parser
  and is interpreted as local time when no offset is supplied. Prefer an
  unambiguous ISO-style value such as `2026-08-07T18:00` or
  `2026-08-07T18:00:00`; the CLI does not promise a narrower grammar, and
  daylight-saving edge cases follow the host runtime's local-time rules.
  Weekly values accept comma-separated short or full day names (`mon,wed` or
  `monday,wednesday`), `weekdays`, `weekends`, `everyday`, `daily`, or `all`;
  they require at least one day. `--time` is local time and only applies to
  weekly tasks.
- Schedule tasks are disabled by default unless `--enabled true` is supplied;
  `schedule enable <task-id>` also validates the completed task configuration.
- A shortcut needs a macro path and a `+`-separated hotkey before it can be
  enabled. `--run-while-held` is an infinite hold loop; otherwise `--loop`
  enables repeated playback and `--repeat` controls the count (`0` means
  infinite in loop mode).
- Shortcut tasks are disabled by default unless `--enabled true` is supplied;
  `shortcut enable <task-id>` requires both the macro path and hotkey.
- A trigger needs a non-empty `--value` for every field except `None`.
  `SwitchProfile` requires `--profile`; `RunMacro` requires `--macro`.
  `OnceOnChange`/`OnEnter` fire on a stable transition, `EveryMatch` fires on
  each matching poll, and `OnExit` fires when a previous match ends. Debounce
  requires a stable match for its interval; cooldown suppresses fires until its
  interval has elapsed.
- Trigger tasks are disabled by default unless `--enabled true` is supplied and
  the selected action is fully configured.

### Platform limitations

- Headless mode still needs a desktop session; it is not a display-less server
  mode. It keeps hotkeys, scheduling, shortcuts, and text expansion alive until
  Ctrl+C.
- Screen capture is available through native Windows capture, macOS 10.15+
  capture, Linux X11, or a supported Linux Wayland provider. macOS screen reads
  need Screen Recording permission; input recording/playback also needs the
  permissions described in [`docs/macos.md`](macos.md).
- Linux Wayland input and capture depend on the compositor, portal selection,
  daemon/direct-device path, and permissions. Run
  `crossmacro doctor --json --verbose` first; see [`docs/linux.md`](linux.md)
  for the supported paths.
- Window commands require a supported window manager/compositor. Unsupported
  backends return exit code `5`; they do not silently mutate another backend.
- Shell steps execute as the current user. Flatpak confines each step to a stricter
  nested sandbox; native and AppImage builds use the normal platform shell. Treat
  every shell-enabled macro as trusted code.
