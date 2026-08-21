# CrossMacro MCP Reference

CrossMacro exposes a local Model Context Protocol (MCP) server for desktop automation. This is the authoritative reference for its transport, security model, tool names, and operational limits.

The server is a focused API over CrossMacro. It is not a general-purpose shell, file-system, or process-execution server.

## Start And Connect

Start the server as a child process of an MCP-capable host:

```bash
crossmacro mcp
```

Use a restricted session when the host should only inspect status and macro metadata:

```bash
crossmacro mcp --restricted
```

Configure the host with an absolute executable path and the separate `mcp` argument. The exact configuration format is host-specific.

```text
command: /absolute/path/to/crossmacro
arguments: ["mcp"]
```

Do not add normal CLI `--json` output options. MCP owns standard output.

## Protocol And Runtime

- Transport is local stdio only. CrossMacro does not expose an HTTP listener or remote MCP transport.
- Standard output contains MCP JSON-RPC only. Send diagnostics to standard error or normal log files.
- Tool names in this document are exact, case-sensitive MCP names. They are not prefixed with `crossmacro.`.
- The server supports MCP tools. It does not currently implement Resources,
  Prompts, Sampling, Roots, Tasks, or MCP Apps.
- The server stops when its client closes standard input or cancels the process.
- MCP does not acquire the GUI/headless startup lock. Multiple MCP processes may run, and they may run alongside GUI or headless mode.
- That startup separation is not cross-process coordination. Concurrent MCP, GUI, and headless processes can still contend for input, clipboard ownership, settings, profiles, and scheduled tasks.
- A desktop session is still required. MCP is not a display-less server mode.

After connecting, call `status.get`, then `help.get`. The runtime `tools/list` response is authoritative for the installed version's complete JSON schemas, required arguments, optional arguments, and annotations.

## Security Model

Starting the server gives it the normal permissions of the user account that started it. Treat any configured host, repository configuration, and automatic approval policy as trusted local code.

CrossMacro applies capability, path, command, platform, size, timeout, and operation checks. It records bounded audit metadata, not request payloads. Host approval UI is additional host behavior; it is not a replacement for the CrossMacro policy.

### Default Policy

The following persisted capabilities default to enabled:

`mcp.macroRead`, `mcp.screenRead`, `mcp.clipboardRead`,
`mcp.clipboardWrite`, `mcp.inputAutomation`, `mcp.recording`,
`mcp.windowRead`, `mcp.windowControl`, `mcp.fileRead`, `mcp.fileWrite`,
`mcp.commandExecute`, `mcp.shellExecute`, `mcp.settingsRead`,
`mcp.settingsWrite`, `mcp.profileManage`, `mcp.textExpansionRead`,
`mcp.textExpansionWrite`, and `mcp.taskManage`.

`mcp.privilegeElevation` defaults to disabled. The standalone server's approval adapter also always denies privilege elevation, so `setup.run` is unavailable in the standard composition even if that setting is enabled.

Effectful calls are automatically approved by the standalone server only after all CrossMacro policy checks pass. A custom composition can add a denying or interactive approval service. The approval timeout defaults to 30 seconds and accepts values from 1 through 300 seconds through `mcp.approvalTimeoutSeconds`.

An MCP session can read supported settings, but cannot set or reset `mcp.*` security settings. Change those local-policy settings through the GUI or the local CLI instead.

### Restricted Mode

`crossmacro mcp --restricted` permits only status reads and macro metadata reads. It denies screen, clipboard, window, file, command, input, recording, settings, profile, text-expansion, and task operations for that process.

### Paths And Shell Steps

File-backed operations require absolute regular paths. Path traversal and symbolic-link/reparse-point paths are rejected where applicable.

The following settings optionally constrain each path class to semicolon-separated absolute roots. An empty root list means that path class has no configured root restriction.

```text
mcp.paths.macroRead
mcp.paths.macroWrite
mcp.paths.imageRead
mcp.paths.imageWrite
mcp.paths.fileRead
mcp.paths.fileWrite
```

For example:

```bash
crossmacro settings set mcp.paths.macroRead /home/user/macros
crossmacro settings set mcp.paths.macroWrite /home/user/macros
crossmacro settings set mcp.paths.imageRead /home/user/images
```

`command.execute` accepts a CrossMacro command token and an argument array, not a shell command string. Existing `run` DSL shell steps require `mcp.shellExecute`; they run as the current user and are not sandboxed. Do not grant that capability to an untrusted agent.

## Tool Catalog

All tools return structured content. For exact request and response schemas, use the server's `tools/list` result. The tables below summarize the stable v1 surface. **Read** tools do not intentionally mutate CrossMacro or the desktop; **effectful** tools require the policy and approval checks described above.

### Server And Settings

| Tool | Access | Required policy | Primary input | Purpose |
| --- | --- | --- | --- | --- |
| `status.get` | Read | `StatusRead` | None | Returns runtime, session, active-profile, capability, policy, and active-operation status. |
| `help.get` | Read | `StatusRead` | None | Returns safe usage guidance and the currently enabled tool catalog. |
| `setup.status` | Read | `StatusRead` | None | Reports whether temporary input setup applies to the current package and session. |
| `setup.run` | Effectful | `PrivilegeElevation` | None | Runs temporary input setup. The standalone server denies it. |
| `daemon.status` | Read | `StatusRead` | None | Returns bounded Linux daemon handshake and socket diagnostics; unavailable off Linux. |
| `settings.get` | Read | `SettingsRead` | Optional `key` or `all: true` | Reads supported settings; sensitive values are redacted. |
| `settings.list_keys` | Read | `SettingsRead` | None | Lists supported settings keys. |
| `settings.set` | Effectful | `SettingsWrite` | `key`, `value` | Updates one supported non-`mcp.*` setting. |
| `settings.reset` | Effectful | `SettingsWrite` | `key` | Restores one supported non-`mcp.*` setting to its default. |

### Profiles And Text Expansions

| Tool | Access | Required policy | Primary input | Purpose |
| --- | --- | --- | --- | --- |
| `profile.list` | Read | `ProfileManage` | None | Lists profiles and the active profile. |
| `profile.current` | Read | `ProfileManage` | None | Returns the active profile. |
| `profile.create` | Effectful | `ProfileManage` | `name` | Creates a profile. |
| `profile.switch` | Effectful | `ProfileManage` | `profile` | Switches the active profile. |
| `profile.rename` | Effectful | `ProfileManage` | `profile`, `newName` | Renames a profile. |
| `profile.delete` | Effectful | `ProfileManage` | `profile`; optional `force` | Deletes a profile. |
| `text_expansion.list` | Read | `TextExpansionRead` | Optional `profile` | Lists text expansions. |
| `text_expansion.test` | Read | `TextExpansionRead` | `trigger`; optional `profile` | Resolves an expansion without sending input. |
| `text_expansion.add` | Effectful | `TextExpansionWrite` | `trigger`, `replacement` | Adds an expansion; optional method and insertion arguments use the live schema. |
| `text_expansion.remove` | Effectful | `TextExpansionWrite` | `trigger`; optional `profile` | Removes an expansion. |
| `text_expansion.enable` | Effectful | `TextExpansionWrite` | `trigger`; optional `profile` | Enables an expansion. |
| `text_expansion.disable` | Effectful | `TextExpansionWrite` | `trigger`; optional `profile` | Disables an expansion. |

### Schedules, Shortcuts, And Triggers

| Tool | Access | Required policy | Primary input | Purpose |
| --- | --- | --- | --- | --- |
| `schedule.list` | Read | `TaskManage` | None | Lists schedule tasks. |
| `schedule.next` | Read | `TaskManage` | `taskId` | Returns the next run time for a schedule. |
| `schedule.run` | Effectful | `TaskManage`, `InputAutomation`, `MacroRead` | `taskId` | Runs a schedule task after checking its stored macro path. |
| `schedule.add` | Effectful | `TaskManage`, `InputAutomation`, `MacroRead` | `name`, absolute `macroPath` | Adds a schedule; interval, time, and enablement fields are optional. |
| `schedule.edit` | Effectful | `TaskManage`, `InputAutomation`, `MacroRead` | `taskId` | Updates supplied schedule fields. |
| `schedule.remove` | Effectful | `TaskManage` | `taskId` | Removes a schedule. |
| `schedule.enable` | Effectful | `TaskManage`, `InputAutomation`, `MacroRead` | `taskId` | Enables a schedule after checking its stored macro path. |
| `schedule.disable` | Effectful | `TaskManage` | `taskId` | Disables a schedule. |
| `shortcut.list` | Read | `TaskManage` | None | Lists shortcut tasks. |
| `shortcut.run` | Effectful | `TaskManage`, `InputAutomation`, `MacroRead` | `taskId` | Runs a shortcut task after checking its stored macro path. |
| `shortcut.add` | Effectful | `TaskManage`, `InputAutomation`, `MacroRead` | `name`, absolute `macroPath`, `hotkey` | Adds a shortcut; playback and window-rule fields are optional. |
| `shortcut.edit` | Effectful | `TaskManage`, `InputAutomation`, `MacroRead` | `taskId` | Updates supplied shortcut fields. |
| `shortcut.remove` | Effectful | `TaskManage` | `taskId` | Removes a shortcut. |
| `shortcut.enable` | Effectful | `TaskManage`, `InputAutomation`, `MacroRead` | `taskId` | Enables a shortcut after checking its stored macro path. |
| `shortcut.disable` | Effectful | `TaskManage` | `taskId` | Disables a shortcut. |
| `shortcut.bind` | Effectful | `TaskManage` | `taskId`, `hotkey` | Changes a shortcut hotkey. |
| `trigger.list` | Read | `TaskManage` | None | Lists trigger tasks. |
| `trigger.add` | Effectful | `TaskManage`, `InputAutomation`; `MacroRead` for `RunMacro` | `name`, `field`, `value` | Adds a trigger; match, action, macro, and timing fields are optional. |
| `trigger.edit` | Effectful | `TaskManage`, `InputAutomation`; `MacroRead` for `RunMacro` | `taskId` | Updates supplied trigger fields. |
| `trigger.remove` | Effectful | `TaskManage` | `taskId` | Removes a trigger. |
| `trigger.enable` | Effectful | `TaskManage`, `InputAutomation`; `MacroRead` for `RunMacro` | `taskId` | Enables a trigger. |
| `trigger.disable` | Effectful | `TaskManage` | `taskId` | Disables a trigger. |

### Automation, Command Compatibility, And Macros

| Tool | Access | Required policy | Primary input | Purpose |
| --- | --- | --- | --- | --- |
| `automation.start` | Effectful | Depends on `kind` | `kind` | Starts one bounded `play`, `run`, or `record` operation and returns an opaque operation ID. |
| `automation.get` | Read | `StatusRead` | `operationId` | Returns operation state and a redacted final outcome. |
| `automation.stop` | Effectful | Any of `InputAutomation`, `Recording`, `CommandExecute` | `operationId` | Requests cancellation. Repeated calls are safe. |
| `command.execute` | Effectful | `CommandExecute` plus command-specific policy | `command`, optional `arguments` array | Runs one permitted CLI command token with structured arguments. |
| `macro.list` | Read | `MacroRead` | Absolute `directoryPath` | Lists up to 100 direct `.macro` files. |
| `macro.inspect` | Read | `MacroRead` | Absolute `macroPath` | Returns macro metadata and validation diagnostics without macro events or embedded assets. |
| `macro.validate` | Read | `MacroRead` | Absolute `macroPath` | Validates a macro without playing it. |

`automation.start` requirements by `kind`:

- `play`: absolute `macroPath`, `MacroRead`, and `InputAutomation`.
- `run`: `steps` or `stepFilePath`, `CommandExecute`; named image assets and
   shell steps receive further path and capability checks.
- `record`: absolute `outputPath`, `Recording`, and `FileWrite`.

`command.execute` accepts only these command tokens:

```text
macro, play, doctor, settings, profile, text-expansion, text, schedule,
shortcut, trigger, record, run, move, click, down, up, scroll, key, tap,
type, delay, clipboard, window, screen, screenshot
```

It rejects recursive `mcp` hosting, GUI/headless lifecycle commands, setup and quick-setup commands, privilege-elevation paths, blocked display-startup options, path-like command tokens, oversized arguments, and arbitrary process execution. Use a dedicated tool whenever one exists.

### Clipboard, Windows, Screen, And Images

| Tool | Access | Required policy | Primary input | Purpose |
| --- | --- | --- | --- | --- |
| `clipboard.get_text` | Read | `ClipboardRead` | None | Reads text clipboard content, up to 65,536 characters. |
| `clipboard.set_text` | Effectful | `ClipboardWrite` | `text` | Writes text clipboard content without echoing it. |
| `clipboard.get_image` | Read | `ClipboardRead` | Optional `includeImage` | Reads a PNG clipboard image when the runtime supports it. |
| `clipboard.set_image` | Effectful | `ClipboardWrite`, `FileRead` | Absolute PNG `imagePath` | Validates and writes a PNG image to the clipboard. |
| `window.query` | Read | `WindowRead` | `mode` | Reads active, listed, searched, or waited-for window state. |
| `window.control` | Effectful | `WindowControl` | `action` | Focuses, closes, moves, resizes, or changes supported workspace/window state. |
| `screen.read` | Read | `ScreenRead` | `mode`, `x`, `y` | Reads a pixel or performs bounded color wait/search operations. |
| `cursor.position` | Read | `ScreenRead` | None | Reads logical global cursor coordinates without moving the pointer. |
| `screen.find_image` | Read | `ScreenRead`, `FileRead` | Absolute PNG `imagePath` | Searches a bounded screen region for an image. |
| `image.read` | Read | `FileRead` | Absolute PNG `imagePath` | Validates a PNG and optionally returns MCP image content. |
| `screenshot.capture` | Effectful | `ScreenRead`; optional `FileWrite` or `ClipboardWrite` | At least one destination | Captures a full screen or bounded region inline, to a PNG path, or to the image clipboard. |

## Calling Rules And Results

Prefer dedicated tools over `command.execute`. Send only the arguments defined by the live schema and preserve returned identifiers exactly.

For effectful work:

1. Read current status with `status.get` when desktop availability matters.
2. Use a dedicated tool with the narrowest capability.
3. For play, run, or recording, retain the returned `operationId`.
4. Poll `automation.get` until the operation reaches a terminal state.
5. Use `automation.stop` only to request cancellation; it is safe to repeat.

CrossMacro application failures are structured results. Inspect the returned `outcome.success`, `outcome.exitCode`, `outcome.message`, warnings, and errors. Effectful failures can also set the MCP call result's `isError` flag. Do not assume that a successful JSON-RPC exchange means that the requested desktop action completed.

## Limits And Data Handling

- Macro lists and window lists return at most 100 entries.
- Text clipboard reads and writes are capped at 65,536 characters.
- PNG files and clipboard images are capped at 48 MiB.
- Inline PNG image content is returned only when explicitly requested and is capped at 8 MiB.
- Screen regions are capped at 16,777,216 pixels.
- Window and screen waits default to 5 seconds and are capped at 30 seconds.
- Automation countdown, timeout, recording duration, and repeat delay are capped at one hour.
- Run automation accepts at most 100 steps, 16,384 characters per step, and 262,144 characters in total.
- `command.execute` accepts at most 128 arguments, 16,384 characters per argument, and 262,144 characters in total.
- One play, run, or record operation can be active per MCP process. At most 32 completed operation snapshots are retained.
- Automation snapshots and audit entries do not retain original paths, run steps, recording paths, shell output, clipboard writes, image bytes, or backend exception details.

## Platform Behavior

Tool discovery only confirms that the server started. It does not prove that a desktop provider or permission is available.

| Area | Linux | Windows | macOS |
| --- | --- | --- | --- |
| Input automation | Depends on compositor, provider, daemon/direct device access, and permissions. | Depends on desktop session permissions. | Requires accessibility permission. |
| Window tools | Depends on the active compositor/provider. | Depends on desktop session support. | Depends on window and accessibility permissions. |
| Screen tools | Depends on X11/Wayland provider, portal, PipeWire, and session. | Depends on capture/session permissions. | Requires Screen Recording permission. |
| PNG clipboard read | Currently unavailable in the MCP adapter. | Supports native PNG clipboard formats. | Currently unavailable in the MCP adapter. |

On Linux, portal capture, native X11 capture, Wayland capture, input injection, daemon IPC, `/dev/input`, and `/dev/uinput` are independent capabilities. Use the same desktop session to run:

```bash
crossmacro doctor --json --verbose
```

## Troubleshooting

- **No tools appear:** confirm that the host launches the absolute executable with `mcp` as a separate argument; inspect standard error for startup errors.
- **Protocol output is invalid:** remove wrappers or banners that write to standard output.
- **A tool returns `environment_error`:** inspect `status.get`, then diagnose the active desktop session, provider, permissions, and package channel.
- **A path is rejected:** use an absolute regular path with the expected file extension; review the configured MCP roots.
- **An effectful tool is denied:** check its capability setting and, for setup, remember that standalone MCP always denies privilege elevation.
- **Automation will not start:** check whether another automation operation is active and whether input or recording preflight has failed.
