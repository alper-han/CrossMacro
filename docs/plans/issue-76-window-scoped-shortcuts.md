# Issue 76: Window-Scoped Shortcuts

## Goal

Allow a shortcut to run only while one or more configured windows have focus. A
shortcut with no window rules keeps its current global behavior.

## Confirmed Findings

- `ShortcutService` currently starts the first enabled task with a matching
  hotkey and does not query the active window.
- Shortcut tasks are already profile-scoped and reload on a profile switch, but
  profile switching is only a workaround: it needs separate enter/exit trigger
  rules and can leave the wrong profile active.
- `TriggerService` already supports `WindowClass`, `WindowTitle`, and
  `ProcessName` matching with `Equals`, `Contains`, and non-backtracking regex
  matching with a 200 ms timeout.
- `IWindowManager.GetActiveWindowAsync` is the platform boundary for focused
  window access. It can be unavailable on a platform/session.
- Source-generated JSON does not populate a get-only rule collection. The
  persisted `WindowRules` contract therefore needs a public setter despite the
  normal domain-model preference for read-only collections.
- Rule lists are snapshotted under the shortcut service lock before focused
  window lookup. Saving/editing a shortcut while the asynchronous lookup is in
  flight cannot alter that hotkey decision or invalidate enumeration.
- The hotkey event is synchronous, so window lookup must happen outside the
  shortcut task lock. Scoped shortcuts must never run when active-window lookup
  is unavailable or fails.

## Design Decisions

- Add `ShortcutWindowRule` entries to `ShortcutTask`.
- Multiple rules use OR semantics: any matching focused window enables the
  shortcut. This directly supports one shortcut for Firefox, Chromium, and
  Chrome without duplicate hotkey registrations.
- Rules allow only `WindowClass`, `WindowTitle`, and `ProcessName`; workspace
  is not a window identity and is intentionally excluded.
- An empty rule list means unrestricted/global shortcut, preserving existing
  persisted shortcuts.
- Invalid rules prevent enabling the shortcut and never match at runtime.
- If scoped and unscoped tasks share a hotkey, a matching scoped task wins;
  otherwise an unscoped task is the fallback.
- Manual `RunTaskAsync` remains an explicit manual execution path and does not
  require the focused-window condition.
- Extract trigger regex/value matching into Core so triggers and shortcuts use
  the same timeout and matching semantics.

## Checklist

- [x] Map existing shortcut, trigger, persistence, and focused-window flows.
- [x] Record implementation decisions and compatibility constraints.
- [x] Add Core window-rule model and shared matcher.
- [x] Persist rules through shortcut JSON with old-file compatibility.
- [x] Apply rules before shortcut hotkey execution.
- [x] Add shortcut editor controls for multiple rules.
- [x] Add per-rule running-window value pickers, consistent with triggers.
- [x] Add localized labels and design-preview coverage.
- [x] Add Core, Infrastructure, serialization, and UI regression tests.
- [x] Add CLI rule management, JSON output, parser tests, and documentation.
- [x] Run targeted build/test validation and inspect the final diff.

## Validation Notes

- Linux Wayland active-window support remains compositor/provider dependent.
  Test the implementation on KDE separately from unit coverage.
- `dotnet build --configuration Debug --no-restore` passed on 2026-08-18.
- Targeted Core (42), Infrastructure (21), UI (40), and CLI (151) tests passed
  on 2026-08-18.
- Shortcut running-window picker UI tests (43) and focused Core tests (11)
  passed on 2026-08-18.
- The Release UI build and NixOS `Default` toplevel build passed after the
  picker field switch was made exhaustive on 2026-08-18.
- The compact window-rule layout and its NixOS `Default` toplevel build passed
  on 2026-08-18.
- Scoped `RunWhileHeld` shortcuts now cancel before playback if the hotkey is
  released while active-window lookup or macro loading is pending; focused
  runtime tests and the NixOS `Default` toplevel build passed on 2026-08-18.
- The NixOS `Default` toplevel build passed after refreshing the local
  `crossmacro` flake input snapshot on 2026-08-18. `WindowRules` uses a
  getter-only, source-generator-populated collection; no analyzer suppression
  is required.
- No commit or push is allowed for this work until explicitly requested.
