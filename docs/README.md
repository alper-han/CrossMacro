# CrossMacro Documentation

Use the canonical reference for the platform or interface you are changing.
Each reference begins with safe first-run guidance and continues through the
complete supported behavior, so there is one source of truth for each area.

## Get Started

- [Create your first macro](../README.md#quick-start)
- [Set up Linux](linux.md)
- [Set up Windows](windows.md)
- [Set up macOS](macos.md)
- [Use the command line](cli.md)
- [Connect an MCP host](mcp.md)

## Reference

- [Linux platform reference](linux.md)
- [Windows setup](windows.md)
- [CLI and runtime reference](cli.md)
- [MCP reference](mcp.md)
- [CLI manpage](man/crossmacro.1)

## Troubleshooting

- [macOS permissions](troubleshooting/macos-permissions.md)
- [macOS Gatekeeper](troubleshooting/macos-gatekeeper.md)

Run `crossmacro doctor --json --verbose` in the affected desktop session before
changing Linux permissions, opening a bug report, or diagnosing MCP tools. A DMG
install on macOS normally uses:

```bash
/Applications/CrossMacro.app/Contents/MacOS/CrossMacro.UI doctor --json --verbose
```

See [macOS setup](macos.md#troubleshooting) for the complete DMG flow.
