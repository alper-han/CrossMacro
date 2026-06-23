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
crossmacro run --file ./steps.txt --json
```

Use single quotes around shell expressions containing `$`, such as
`'repeat $n {'`, so the shell does not expand the variable before CrossMacro
sees it.

## Screen reading steps

CrossMacro's screen-reading commands are available on Windows desktop sessions,
macOS 10.15+, native Linux X11, and Linux Wayland. Windows and macOS use native
capture APIs; macOS requires Screen Recording permission. Linux backend details
are documented in [`docs/linux.md`](linux.md).

```bash
pixelcolor 500 300 mycolor
pixelcolor rel 0 0 underCursor
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
- `set <name> <value>` or `set <name>=<value>`
- `inc <name> [amount]` and `dec <name> [amount]`
- `repeat <count> { ... }`
- `if <left> <op> <right> { ... } else { ... }`
- `while <left> <op> <right> { ... }`
- `for <var> from <start> to <end> [step <n>] { ... }`
- `break`, `continue`, and `}`

Use `--dry-run` to parse, compile, and validate a direct-run command without
sending input.
