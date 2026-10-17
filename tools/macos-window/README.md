# macOS window-task prototype

This fork reuses CrossMacro's **existing Shell Command editor actions, macro
files, playback engine and scheduler**. A small external executor captures a
specific app window, reads it with local Apple Vision OCR and sends window-bound
input through an already installed CUA MCP runtime. No model is called at run
time. This is an experimental integration, not a replacement for the default
desktop backend or an implementation of background input for every Mac app.

## Requirements

- macOS and a working local CUA MCP installation with its normal OS permissions.
  The runtime is supplied by the user. Its proprietary binaries are **not**
  included, and its internal entry point is not a stable public SDK contract.
- Python 3.9+ (use an explicit executable path for GUI-launched macros).
- Xcode command-line tools to compile the local OCR helper.
- .NET 10 to build CrossMacro following `CONTRIBUTING.md`.

Build the OCR helper locally:

```sh
mkdir -p tools/macos-window/build
clang -fobjc-arc -fno-modules -framework Foundation -framework Vision \
  -framework ImageIO tools/macos-window/vision-ocr.m \
  -o tools/macos-window/build/vision-ocr
```

Copy `config.example.json` **outside this repository** and set the MCP manifest,
OCR binary, exact running application path/bundle ID, language, expected account
identity strings and private log directory. `authorized_tools` is the explicit
local app/capability allowlist used for normal MCP elicitation requests. Requests
for other applications or capabilities are declined; no persistent grant or
chat identity is synthesized. Keep account IDs, paths, screenshots and logs local.

`guard_command`, when set, is an argv array returning JSON with an `active`
executor field. A non-null active executor stops the task before backend startup.
A local advisory lock prevents this executor from running two steps at once.
This is not a distributed lock with other automation systems: do not schedule
another executor against the same game during this experiment.

## Create an editable CrossMacro macro

```sh
/absolute/path/to/python3 tools/macos-window/window_macro.py export \
  --config /private/path/config.json \
  --workflow tools/macos-window/wos-profile.json \
  --output /private/path/wos-profile.macro
```

Open the generated file with **Editor → Load**. It restores four ordinary,
editable Shell Command actions: verify city, open profile, verify identity, and
return to city. The command/step selection, ordering and execution timeout can
be edited and saved in CrossMacro. OCR text and click coordinates currently live
in the JSON workflow; they do **not** yet have dedicated visual property panels.
The export embeds absolute paths; regenerate it when moving the project.

Before running GUI schedules, start the local worker from your terminal/runtime:

```sh
/absolute/path/to/python3 tools/macos-window/window_macro.py serve \
  --config /private/path/config.json \
  --workflow tools/macos-window/wos-profile.json
```

Add a `worker_socket` path to the local config, inside a private, current-user
directory (0700). The socket itself is 0600. The worker accepts only step names
from its loaded workflow; it does not accept commands, scripts, target apps or
configuration over IPC. Generated Shell actions use `request` to talk to this
worker, which serializes execution and retains one MCP connection. Stop it with
Ctrl-C (or SIGTERM when launched as a managed process).

In the local trial, starting the CUA runtime directly from a GUI-launched child
timed out during its initial tool call, while terminal-launched execution worked.
The worker is the tested integration route; its initialization still depends on
the user's installed runtime and desktop environment. This is not evidence that
the same initialization will work from launchd or after the host app exits.

Launch the platform app with these environment variables (an app bundle can
declare them in its local `LSEnvironment`):

```sh
export CROSSMACRO_CONFIG_DIR=/absolute/private/path/crossmacro-profile
export CROSSMACRO_WINDOW_TASKS_ONLY=1
dotnet run --project src/CrossMacro.UI.MacOS/CrossMacro.UI.MacOS.csproj
```

`CROSSMACRO_CONFIG_DIR` isolates profiles, schedules and logs from the user's
normal installation. The window-task mode starts the editor and scheduler,
without native global hotkeys, text expansion, triggers or recording services.
Native input/capture simulator factories explicitly reject requests in this
mode. The default application behavior is unchanged when the flag is absent.
External actions retain their own OS permission requirements.

Before scheduling, validate and replay the macro with the platform executable:

```sh
crossmacro macro validate /private/path/wos-profile.macro --json
crossmacro play /private/path/wos-profile.macro --json
crossmacro schedule add --name "WOS prototype once" \
  --macro /private/path/wos-profile.macro --at "2030-01-15T18:00:00"
```

New tasks start disabled. Enable a **single** future test in Schedule after
checking the target app, then keep CrossMacro running in the logged-in desktop
session. A one-time task becomes disabled after its attempt. Do not migrate
existing daily schedules until the trial is reliable.

## Execution behavior

- Each step takes a fresh target-window image and checks actual image dimensions
  against `reference_size`; a size change stops execution before a click.
- OCR must satisfy all preconditions. Identity-sensitive steps also require the
  configured identity strings. Clicks require a separate visual completion check.
- There are no blind retries, foreground fallback, global cursor events, cloud
  OCR or game network/memory APIs. A failure exits nonzero and is surfaced by
  CrossMacro's normal Shell/schedule status.
- The bundled WOS flow only opens a profile and returns. It spends no resources.
  Its sample coordinates are calibrated to a 788×1079 window, including title bar.
- Step logs contain timestamps, pass/fail conditions, screenshots and OCR JSON.
  Cancellation closes the owned MCP process group; it does not force clicks to
  recover an unknown game state.
- Lock screen, sleep, minimized windows, app upgrades and long unattended runs
  are not established by this prototype. The host/runtime currently must remain
  available. Shell steps each create a new MCP process, trading some startup
  overhead for simple cancellation and isolation. The exported GUI macro instead
  uses the persistent worker connection. A failed worker must be restarted before
  further scheduled work; no foreground fallback is attempted.

## Tests

```sh
python3 -m unittest discover -s tools/macos-window -p 'test_*.py' -v
dotnet test tests/CrossMacro.UI.Tests/CrossMacro.UI.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~ApplicationPathsEnvironmentTests|FullyQualifiedName~DesktopStartupRuntimeServiceTests'
```

The Python tests check OCR assertions, fail-closed input, invalid coordinates,
workflow export and coordinator exclusion. Platform integration must additionally
verify both screenshot results and the user's physical pointer during real runs.
