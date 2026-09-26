<h1 align="center">CrossMacro</h1>

<p align="center">
  <strong>Record once. Replay whenever you need.</strong>
</p>

<p align="center">
  A free, open-source mouse and keyboard macro recorder for Linux, Windows, and macOS.
</p>

<p align="center">
  <a href="https://github.com/alper-han/CrossMacro/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/alper-han/CrossMacro?display_name=tag&sort=semver&label=latest&logo=github&cacheSeconds=3600"></a>
  <a href="https://github.com/alper-han/CrossMacro/actions/workflows/ci.yml"><img alt="CI status" src="https://github.com/alper-han/CrossMacro/actions/workflows/ci.yml/badge.svg?branch=main&event=push"></a>
  <a href="https://flathub.org/apps/io.github.alper_han.crossmacro"><img alt="Cumulative Flathub installs" src="https://img.shields.io/flathub/downloads/io.github.alper_han.crossmacro?label=Flathub%20installs&logo=flathub&cacheSeconds=3600"></a>
  <a href="https://github.com/alper-han/CrossMacro/releases"><img alt="GitHub release asset downloads" src="https://img.shields.io/github/downloads/alper-han/CrossMacro/total?label=asset%20downloads&logo=github&cacheSeconds=3600"></a>
  <a href="https://github.com/alper-han/CrossMacro"><img alt="GitHub stars" src="https://img.shields.io/github/stars/alper-han/CrossMacro?style=flat&label=stars&logo=github&cacheSeconds=3600"></a>
  <a href="LICENSE"><img alt="GPL-3.0-only license" src="https://img.shields.io/github/license/alper-han/CrossMacro"></a>
</p>

<p align="center">
  <a href="https://github.com/alper-han/CrossMacro/releases/latest"><img alt="Download CrossMacro from GitHub Releases" src="https://img.shields.io/badge/Download-GitHub%20Releases-181717?style=for-the-badge&logo=github&logoColor=white"></a>
  <a href="https://flathub.org/apps/io.github.alper_han.crossmacro"><img alt="Download CrossMacro on Flathub" src="https://img.shields.io/badge/Get%20it%20on-Flathub-4A86CF?style=for-the-badge&logo=flathub&logoColor=white"></a>
  <a href="https://apps.microsoft.com/detail/9n1qp1d6js70"><img alt="Download CrossMacro from Microsoft Store" src="https://get.microsoft.com/images/en-us%20dark.svg" height="28"></a>
</p>

<p align="center">
  <a href="#install">Download</a> ·
  <a href="#your-first-macro">First macro</a> ·
  <a href="#features">Features</a> ·
  <a href="docs/README.md">Documentation</a> ·
  <a href="https://alper-han.github.io/CrossMacro/">Website</a>
</p>

<p align="center">
  <a href="screenshots/recording-tab.png"><img src="screenshots/recording-tab.png" alt="CrossMacro recording screen with mouse and keyboard capture options and the Start Recording button" width="700"></a><br>
  <sub>Start with a recording. Refine and automate it when you need more.</sub>
</p>

## Why CrossMacro?

CrossMacro brings recording, playback, and everyday automation into one desktop
app. Start with a task you already do by hand, then decide how much to automate.

- **Start without writing a script.** Record mouse and keyboard actions, save
  the result, and replay it. The visual editor is there when you want to make
  changes, not a prerequisite for your first macro.
- **Use your desktop.** Linux Wayland and X11 are supported alongside native
  Windows and macOS integrations. Setup and available capabilities depend on
  your platform and desktop session.
- **Keep your workflow together.** Reuse saved macros with shortcuts, schedules,
  and triggers; keep settings and automation tasks organized in profiles.

<a id="what-people-build-with-it"></a>

### What can you automate?

- **Repetitive data entry:** replay form-filling steps, clicks, and clipboard
  actions instead of repeating them by hand.
- **Everyday typing:** expand abbreviations into frequently used text, such as
  email addresses and standard replies.
- **UI checks:** repeat an interaction and wait for an expected color, image,
  or window before continuing.
- **Recurring desktop tasks:** run a saved workflow from a hotkey, on a
  schedule, or when the focused window matches a rule.

<a id="download-matrix"></a>

## Install

Choose the installation channel that suits you; the stores are not the only
options. Each setup link below starts with installation before troubleshooting.

| Platform | Install options | Setup |
| --- | --- | --- |
| <a id="linux"></a>**Linux** | [Flathub](https://flathub.org/apps/io.github.alper_han.crossmacro)<br>[DEB, RPM, AUR, AppImage, NixOS](docs/install.md#linux) | [Linux setup](docs/install.md#linux) |
| <a id="windows"></a>**Windows** | [Microsoft Store](https://apps.microsoft.com/detail/9n1qp1d6js70)<br>[winget](docs/install.md#winget) · [Portable EXE](docs/install.md#portable-exe) | [Windows setup](docs/install.md#windows) |
| <a id="macos"></a>**macOS 14+** | [DMG for Apple Silicon or Intel](docs/install.md#macos) | [macOS setup](docs/macos.md#install-the-app) |

The [full download guide](docs/install.md#download-matrix) covers every channel,
architecture selection, development packages, and [checksums](docs/install.md#verify-downloads).
GitHub MSIX files are unsigned advanced/test packages; use Store, winget, or
portable EXE for normal Windows installation.

<a id="permissions-and-desktop-readiness"></a>

- **Linux:** Wayland setup depends on the package and desktop. Native packages
  may require a new login after group changes; Flatpak and AppImage may request
  temporary device setup.
- **macOS:** DMGs are unsigned and not notarized. Follow the setup guide for
  Gatekeeper and privacy permissions before recording or replaying.

<a id="quick-start"></a>

## Your First Macro

Try typing a short phrase in an empty text editor first.

1. **Open CrossMacro** and complete your platform's setup prompts.
2. **Record:** focus the empty text editor, press `F8`, type your phrase, then
   press `F8` again to stop recording.
3. **Replay:** clear the test text and put the text cursor back at the beginning.
   Keep the editor focused and press `F9`. Use `F10` to pause or resume, or press
   `F9` again to stop playback.
4. **Keep it:** open **Files** and save the macro to reuse it. You can adjust
   its steps later in the editor or attach it to a shortcut, schedule, or trigger.

These are the default hotkeys; you can change them in Settings. For macros with
mouse actions, also restore the original window layout and starting pointer
position before replaying. Test a saved macro before scheduling it; automation
needs an active desktop session.

## Features

<a id="macro-recorder-and-player"></a>

### Recording and playback

Capture mouse movements, clicks, scrolling, and keyboard events, together or
independently. Adjust playback speed, countdowns, repeat counts, and delays;
pause and resume a running macro. Save recordings as reusable `.macro` files
and organize them into a library.

<a id="macro-editor-and-workflow-building"></a>
<a id="screen-desktop-and-text-integration"></a>

### Visual editing and screen-aware actions

Edit, reorder, or remove recorded steps instead of starting over. Add text,
delays, clipboard and window actions, or build more involved workflows with
variables, loops, and conditions. Wait for colors, images, or windows before
continuing, rather than relying only on fixed delays.

<a id="hotkeys-schedules-triggers-and-background-automation"></a>

### Shortcuts, schedules, and triggers

Launch a macro with a global keyboard or mouse shortcut. Run tasks once, at
intervals, or on a daily or weekly schedule. Match the focused window's class,
title, or process name, or the active workspace, to trigger a macro or switch
profiles on supported desktops.

### Text expansion and profiles

Turn short abbreviations into reusable text. Separate work, testing, and other
routines into named profiles that keep their macro libraries, shortcuts,
schedules, triggers, text expansions, and settings together.

<a id="cross-platform-application-and-integrations"></a>
<a id="more-cli-examples"></a>

### CLI and local MCP integration

The graphical app, [command-line interface (CLI)](docs/cli.md), and
[Model Context Protocol (MCP) server](docs/mcp.md) share the same automation
engine. Use the CLI to record, validate, and run macros or script desktop
actions with structured JSON output. The GUI-less runtime can keep automation
active without the app window, but still needs a desktop session; it is not
a display-less server mode.

MCP lets trusted local agents perform real desktop actions. Start with
restricted mode and review its permissions before granting access.

Screen capture, cursor tracking, and window control vary by platform and desktop
session. See the platform guides for supported capabilities and setup details.

<a id="see-it-in-action"></a>
<a id="compare"></a>
<a id="desktop-automation-products"></a>
<a id="command-line-automation"></a>

[Explore all features](docs/features.md) ·
[Browse screenshots](docs/features.md#screenshots) ·
[Compare with other tools](docs/comparison.md)

<a id="documentation"></a>

## Documentation & Help

| Looking for... | Start here |
| --- | --- |
| A package, setup instructions, or permissions | [Linux installation](docs/install.md#linux) · [Windows installation](docs/install.md#windows) · [macOS setup](docs/macos.md) |
| Command-line automation and scripting | [CLI reference](docs/cli.md) |
| Connecting a local agent safely | [MCP setup and security](docs/mcp.md) |
| <a id="diagnose-a-problem"></a>Troubleshooting an unavailable feature | [Diagnostics and troubleshooting](docs/README.md#troubleshooting) |

<a id="community"></a>

Browse the [documentation index](docs/README.md) for the full reference.
For questions, use [Discussions](https://github.com/alper-han/CrossMacro/discussions)
or join [Discord](https://discord.gg/QUBuND5TvM).
[Report a bug](https://github.com/alper-han/CrossMacro/issues/new/choose) with your
version, install channel, desktop environment, and steps to reproduce it.

<p>
  <a href="https://discord.gg/QUBuND5TvM"><img src="https://discord.com/api/guilds/1477899451476742164/widget.png?style=banner2" alt="Join the CrossMacro Discord community"></a>
</p>

## Contributing

Code is only one way to help. Bug reports, documentation improvements,
translations, and testing on different desktops are welcome.
See [CONTRIBUTING.md](CONTRIBUTING.md) to get started; pull requests target `dev`.

[![CrossMacro contributors](https://contrib.rocks/image?repo=alper-han/CrossMacro)](https://github.com/alper-han/CrossMacro/graphs/contributors)

<a id="license"></a>

Report vulnerabilities privately through the [security policy](SECURITY.md).
CrossMacro is licensed under [GPL-3.0-only](LICENSE).

## Support CrossMacro

If CrossMacro is useful to you, consider starring the repository to show your
support. Sharing a workflow or helping another user is welcome too.

<a id="star-history"></a>

<p align="left">
  <a href="https://star-history.dera.page/#alper-han/crossmacro&type=date&legend=top-left">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="https://star-history.dera.page/svg?repos=alper-han/crossmacro&type=date&theme=dark&legend=top-left">
      <source media="(prefers-color-scheme: light)" srcset="https://star-history.dera.page/svg?repos=alper-han/crossmacro&type=date&legend=top-left">
      <img alt="CrossMacro star history chart" src="https://star-history.dera.page/svg?repos=alper-han/crossmacro&type=date&legend=top-left" width="600">
    </picture>
  </a>
</p>
