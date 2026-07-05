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

## Common commands

```bash
crossmacro --help
crossmacro --version
crossmacro --start-minimized

crossmacro play ./demo.macro --speed 1.25 --repeat 3 --repeat-delay-ms 500
crossmacro macro validate ./demo.macro
crossmacro macro info ./demo.macro
crossmacro doctor --json --verbose

crossmacro settings get
crossmacro settings get playback.speed
crossmacro settings set playback.speed 1.25

crossmacro schedule list
crossmacro schedule run <task-id>
crossmacro shortcut list
crossmacro shortcut run <task-id>

crossmacro clipboard get
crossmacro clipboard set "hello"
crossmacro clipboard set --file ./message.txt
crossmacro clipboard clear
crossmacro window active --json
crossmacro window search --title Firefox
crossmacro window move --active 100 100
crossmacro window workspace switch 2
crossmacro screen pixel 500 300
crossmacro screen wait-color 500 300 00FF00 --timeout-ms 5000
crossmacro screen search-color 0 0 1920 1080 FF0000 --tolerance 26
crossmacro screenshot --output ./shot.png
crossmacro screenshot --clipboard
crossmacro screenshot --output ./crop.png --region 100 100 800 600

crossmacro record --output ./recorded.macro --duration 10 --mode auto
crossmacro headless
crossmacro --headless
```

Supported log levels for CLI commands are `Verbose`, `Debug`, `Information`,
`Warning`, `Error`, and `Fatal`.

For desktop autostart, use `crossmacro --start-minimized`. When tray icon
support is available, CrossMacro starts hidden to tray; otherwise it starts as a
minimized window.

## GUI-less desktop runtime

The `headless` commands start CrossMacro's GUI-less desktop runtime for services
such as hotkeys, scheduler, shortcuts, and text expansion:

```bash
crossmacro headless
crossmacro --headless
```

This mode still requires a desktop session. It is not intended for display-less
server automation.

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
selectors match the compositor/window-manager address exactly. Unsupported
platforms return a clear non-zero environment error.

## Screen command

The first-class screen command is an ergonomic wrapper for pixel/color reads:

```bash
crossmacro screen pixel 500 300
crossmacro screen pixel --relative 0 0 --json
crossmacro screen wait-color 500 300 00FF00 --timeout-ms 5000
crossmacro screen search-color 0 0 1920 1080 FF0000 --tolerance 26 --json
```

- `screen pixel <x> <y>` prints one pixel color and includes coordinates/color in
  JSON `data`.
- `screen pixel --relative <dx> <dy>` samples relative to the current cursor; it
  returns an unsupported error if no mouse position provider is available.
- `screen wait-color <x> <y> <RRGGBB> [--timeout-ms <n>]` waits for a color.
- `screen search-color <x1> <y1> <x2> <y2> <RRGGBB> [--tolerance <0..255>]`
  searches the end-exclusive region `[x1, x2) x [y1, y2)`.

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

## Direct run examples

`crossmacro run` executes inline steps without a `.macro` file:

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
crossmacro run \
  --step "set i=0" \
  --step 'while $i < 10 {' \
  --step "click left" \
  --step "inc i" \
  --step "}"
crossmacro run --step 'shell "notify-send done" 1 250 5000'
crossmacro run --file ./steps.txt --json
```

Use single quotes around shell expressions containing `$`, such as
`'repeat $n {'`, so the shell does not expand the variable before CrossMacro
sees it.

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
arbitrary commands as the current OS user, so only run trusted macros. They are
disabled in Flatpak builds to prevent sandbox escapes; use a native or AppImage
build when a macro needs shell execution. Use `$$NAME` when you want the shell to
receive `$NAME` literally instead of resolving a CrossMacro variable.

## Screen reading steps

CrossMacro's screen-reading commands are available on Windows desktop sessions,
macOS 10.15+, native Linux X11, and Linux Wayland. Windows and macOS use native
capture APIs; macOS requires Screen Recording permission. Linux backend details
are documented in [`docs/linux.md`](linux.md).

```bash
pixelcolor 500 300 mycolor
pixelcolor rel 0 0 underCursor
clipboard get clipText
clipboard set "new clipboard text"
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
pixelsearch 0 0 1920 1080 FF0000 found found_x found_y tolerance 26
```

- `pixelcolor <x> <y> [var]` samples one pixel at an absolute position.
- `pixelcolor rel <dx> <dy> [var]` samples one pixel relative to the current
  cursor position.
- `waitcolor <x> <y> <RRGGBB|$var> [timeout_ms] [result_var]` waits for an exact
  color match at a single point. When `result_var` is present, timeout writes
  `false` and playback continues; without it, timeout keeps the existing
  fail-fast behavior.
- `pixelsearch <x1> <y1> <x2> <y2> <RRGGBB|$var> [found_var var_x var_y|var_x var_y] [tolerance <0..255>]`
  searches the end-exclusive region `[x1, x2) x [y1, y2)` and stores the first
  match. When `found_var` is present, no match writes `false` plus `-1, -1`
  coordinates and playback continues; the legacy `var_x var_y` form keeps
  fail-fast behavior.
- `clipboard get <var>` stores current clipboard text in a runtime variable.
- `clipboard set <text>` replaces clipboard text after variable substitution.
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

Target colors can be a canonical six-digit `RRGGBB` value with no `#`, or a
`$var` reference to a color previously written by `pixelcolor`; bare variable
names are not accepted in target color positions. Hex values are written back in
uppercase. `pixelsearch` defaults to exact matching when tolerance is omitted;
non-zero tolerance allows that many shades of difference per RGB channel. Image
matching is not included.

`waitcolor` polls every 50 ms by default and uses a 30 second default timeout
when `timeout_ms` is omitted. If you pass a timeout, it is measured in
milliseconds.

`pixelsearch` scans row by row, left to right, and only assigns `var_x` and
`var_y` after the search succeeds. Variable names still follow the usual script
variable rules.

On macOS, grant Screen Recording permission in System Settings > Privacy &
Security > Screen Recording, then restart CrossMacro.

## Run step commands

Supported direct-run steps include:

- `move abs <x> <y>` and `move rel <dx> <dy>`
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
- `clipboard get <var>` and `clipboard set <text>`
- `window active|search|wait|focus|close|move|resize|center|maximize|fullscreen|float|getdesktop|setdesktop|setdesktopforwindow ...`
- `pixelcolor <x> <y> [var]`, `pixelcolor rel <dx> <dy> [var]`,
  `waitcolor <x> <y> <RRGGBB|$var> [timeout_ms] [result_var]`, and
  `pixelsearch <x1> <y1> <x2> <y2> <RRGGBB|$var> [found_var var_x var_y|var_x var_y] [tolerance <0..255>]`
- `set <name> <value>` or `set <name>=<value>`
- `inc <name> [amount]` and `dec <name> [amount]`
- `repeat <count> { ... }`
- `if <left> <op> <right> { ... } else { ... }`
- `while <left> <op> <right> { ... }`
- `for <var> from <start> to <end> [step <n>] { ... }`
- `break`, `continue`, and `}`

Use `--dry-run` to parse, compile, and validate a direct-run command without
sending input.
