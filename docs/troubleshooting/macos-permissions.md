# Troubleshoot macOS Permissions

Run diagnostics with the app-bundle executable:

```bash
/Applications/CrossMacro.app/Contents/MacOS/CrossMacro.UI doctor --json --verbose
```

| Symptom | Action |
| --- | --- |
| Recording or global shortcuts fail | Enable CrossMacro under **Input Monitoring** |
| Playback fails | Enable CrossMacro under **Accessibility**, then quit and reopen the app if macOS has not applied the approval yet |
| Pixel, screenshot, or image operations fail | Enable CrossMacro under **Screen & System Audio Recording** or **Screen Recording** |
| Permission is enabled but still reported missing | Quit CrossMacro completely and open the copy in `/Applications` again |
| CrossMacro is not listed | Trigger the affected feature again, then use the System Settings page opened by CrossMacro |
| Multiple CrossMacro entries appear | Remove obsolete entries, keep the `/Applications` copy, and grant permission again |

If a permission was granted to a build in another directory, macOS can treat the
copy in `/Applications` as a different application identity. Test the exact app
bundle you intend to keep using.

For installation blocks or damaged-app warnings, use the
[Gatekeeper guide](macos-gatekeeper.md).
