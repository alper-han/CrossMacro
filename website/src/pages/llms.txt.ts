import type { APIRoute } from 'astro';
import { absoluteUrl, guides, site } from '../data/site';

export const GET: APIRoute = () => {
  const guideLinks = Object.values(guides)
    .map((guide) => `- [${guide.label}](${absoluteUrl(guide.route)}): ${guide.description}`)
    .join('\n');
  const content = `# CrossMacro

> ${site.description}

CrossMacro is a desktop application licensed under GPL-3.0. It records mouse and keyboard actions, provides a visual macro editor, and supports replay, hotkeys, scheduling, text expansion, a CLI, and a local MCP server. It is available for Linux, Windows, and macOS.

## Product and installation

- [Official website](${absoluteUrl('/')}): Overview, screenshots, platform downloads, and first-macro steps.
- [Source repository](${site.repository}): README, source, license, and issue tracker.
- [Latest release](${site.releases}): Platform-specific downloads and checksums.
- [Documentation](${site.docs}): Maintained platform setup, troubleshooting, CLI, and MCP references.

## Practical guides

${guideLinks}

## Platform and integration references

- [Linux setup](${site.repository}/blob/dev/docs/linux.md): Wayland/X11 input paths, package differences, permissions, and compositor capabilities.
- [Windows setup](${site.repository}/blob/dev/docs/windows.md): Microsoft Store, portable builds, and desktop-session requirements.
- [macOS setup](${site.repository}/blob/dev/docs/macos.md): macOS 14+, Intel/Apple Silicon, unsigned DMG setup, and privacy permissions.
- [CLI reference](${site.repository}/blob/dev/docs/cli.md): Commands, dry runs, workflow syntax, JSON output, and runtime behavior.
- [MCP reference](${site.repository}/blob/dev/docs/mcp.md): Local stdio server, trusted hosts, access policies, and restricted mode.

## Important limits

- Desktop input automation needs an active desktop session. GUI-less mode is not a display-less server mode.
- Wayland capabilities and setup vary by compositor and package. Input, cursor position, screen capture, and window control are separate capabilities.
- Text expansion needs global and per-entry enablement. Typed triggers match immediately and are case-sensitive.
- Scheduled tasks need a running GUI or GUI-less desktop runtime. The Linux input daemon alone does not run schedules.
- macOS builds are unsigned and not notarized. Required privacy permissions depend on the action.
- MCP and macros can cause real input and other side effects. Use trusted workflows and the documented policies.
- The same macro file may need changes for another application's layout, operating system, or display configuration.

## Discovery

- [Sitemap](${absoluteUrl('/sitemap.xml')}): Canonical indexable website pages.

This file is a concise navigation aid. The linked documentation contains the detailed behavior and limitations; this summary does not replace it.
`;
  return new Response(content, { headers: { 'Content-Type': 'text/plain; charset=utf-8' } });
};
