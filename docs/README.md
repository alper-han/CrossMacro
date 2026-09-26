# CrossMacro Documentation

Start with installation and a first macro, explore the visual workflows, or use
the canonical reference for your platform or command interface.

## Get Started

- [Install CrossMacro: download channels and checksums](install.md)
- [Create your first macro](../README.md#quick-start)
- [Explore features and use cases](features.md)
- [Browse the screenshot gallery](features.md#screenshots)
- [Compare desktop and command-line tools](comparison.md)
- [Set up Linux](install.md#linux)
- [Set up Windows](windows.md)
- [Set up macOS](macos.md)
- [Use the command line](cli.md)
- [Connect an MCP host](mcp.md)

## Reference

- [Linux platform reference](linux.md)
- [Windows setup](windows.md)
- [macOS setup](macos.md)
- [CLI and runtime reference](cli.md)
- [MCP reference](mcp.md)
- [CLI manpage](man/crossmacro.1)

## Troubleshooting

- [Linux desktop, daemon, and permissions reference](linux.md)
- [Windows setup and diagnostics](windows.md)
- [macOS troubleshooting](macos.md#troubleshooting)
- [macOS permissions](troubleshooting/macos-permissions.md)
- [macOS Gatekeeper](troubleshooting/macos-gatekeeper.md)

Run `crossmacro doctor --json --verbose` in the affected desktop session before
changing Linux permissions, opening a bug report, or diagnosing MCP tools. A DMG
install on macOS normally uses:

```bash
/Applications/CrossMacro.app/Contents/MacOS/CrossMacro.UI doctor --json --verbose
```

See [macOS setup](macos.md#troubleshooting) for the complete DMG flow.

The report checks input, cursor, permission, session, daemon, and available
platform-provider diagnostics. On Linux it reports daemon and direct-device
readiness separately. It is not a complete readiness test for every clipboard,
tray, window, or screen operation.

When [opening a bug report](https://github.com/alper-han/CrossMacro/issues/new/choose),
include your CrossMacro version and install channel, operating system and desktop
session, the smallest reproducible workflow, and relevant logs and `doctor` output.
You can also [ask the community](https://github.com/alper-han/CrossMacro/discussions)
or [join Discord](https://discord.gg/QUBuND5TvM).

## Contributing and security

- [Contributing](../CONTRIBUTING.md): build the project, run tests, and prepare a
  pull request targeting `dev`. Bug reports, documentation improvements,
  translations, platform testing, and code contributions are welcome.
- [Security policy](../SECURITY.md): report vulnerabilities privately through
  [GitHub security advisories](https://github.com/alper-han/CrossMacro/security/advisories/new).
