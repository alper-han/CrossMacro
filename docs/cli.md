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
