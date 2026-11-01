# CrossMacro Custom Themes

CrossMacro loads custom JSON themes from your personal themes folder, not from
the application install or source directory. Open **Settings > Theme > Open
themes folder** to open the exact folder used by the current installation.

The usual locations are:

| Platform | Themes folder |
| --- | --- |
| Linux | `$XDG_CONFIG_HOME/crossmacro/themes` or `~/.config/crossmacro/themes` |
| Windows | `%APPDATA%\crossmacro\themes` |
| macOS | `~/Library/Application Support/crossmacro/themes` |

On first use, CrossMacro creates this folder and copies `_template.json` plus
this README into it. A JSON file appears in **Settings > Theme** after you press
the refresh button next to the theme selector or restart the app.

## Getting started

1. Copy `_template.json` to a new file, for example `my-theme.json`.
2. Change `"name"` — this is what shows up in the theme list.
3. Optionally set `"appearance"` to `"Light"` or `"Dark"`; if omitted, CrossMacro infers it from the background and primary text colors.
4. Edit the colors inside `"palette"`.
5. Press the refresh button next to the theme selector and pick your theme.

## System and fixed themes

CrossMacro starts by following the operating system's light or dark preference,
using Rose Pine Dawn for light and Mocha for dark. Choose **System** in the theme
selector to follow OS appearance, or choose any named theme to keep that palette fixed.
Custom themes can declare `"appearance": "Light"` or `"Dark"`; omitting it
lets CrossMacro infer appearance from the background and primary text colors.

## Built-in palette families

CrossMacro includes light and dark variants from several established palette
families: Catppuccin-inspired Latte/Mocha, Gruvbox, Nord, and Rosé Pine, plus
Dracula's Alucard, Tokyo Night, Ayu, Everforest, and Material Theme palettes.
The palette JSON files in this directory show the exact colors and are the
source of truth for each shipped variant.

Palette references:

- [Dracula / Alucard palette](https://github.com/dracula/dracula-theme#color-palette-oss)
- [Tokyo Night VS Code themes](https://github.com/tokyo-night/tokyo-night-vscode-theme/tree/master/themes)
- [Rosé Pine palette](https://github.com/rose-pine/rose-pine-palette/blob/main/palette.json)
- [Ayu color palettes](https://github.com/ayu-theme/ayu-colors)
- [Everforest palette and variants](https://github.com/sainnhe/everforest/blob/master/palette.md)
- [Material Theme color palette](https://material-theme.com/docs/reference/color-palette/)

The new built-in variants are Ayu Dark, Ayu Mirage, Ayu Light, Everforest Dark
Soft, Everforest Light, Material Oceanic, and Material Lighter.

## Rules

- The template uses `#RRGGBB` hex colors. Avalonia accepts other valid color
  values, but hex values are recommended for portable, easy-to-review themes.
- Every palette key in the template is required. A file with missing or
  invalid values is skipped and a warning is written to the application log.
- Theme names must be unique (case-insensitive). A file whose name matches a
  built-in theme or another custom theme is skipped.
- Files starting with an underscore are ignored. Prefix a file with `_`
  (e.g. `_my-theme.json`) to disable a theme without deleting it.

## Palette keys

| Group | Keys |
| --- | --- |
| Primary actions | `primaryColor`, `primaryHoverColor`, `primaryPressedColor` |
| Success states | `successColor`, `successHoverColor`, `successPressedColor` |
| Danger states | `dangerColor`, `dangerHoverColor`, `dangerPressedColor` |
| Surfaces | `backgroundColor`, `surfaceColor`, `surfaceHoverColor` |
| Text | `textPrimaryColor`, `textSecondaryColor` |
| Warnings | `warningColor`, `warningHoverColor` |
| Accent highlights | `accentColor` |
| System accent variants | `systemAccentColor`, `systemAccentColorDark1`-`Dark3`, `systemAccentColorLight1`-`Light3` |
| Text on colored buttons | `textOnPrimaryColor`, `textOnSuccessColor`, `textOnDangerColor`, `textOnWarningColor` |

Tips:

- Pick each `textOn…` color so it has enough contrast against its matching
  base color (WCAG 4.5:1 is a good target).
- The built-in JSON themes use the same format and are good references:
  <https://github.com/alper-han/CrossMacro/tree/dev/src/CrossMacro.UI/Themes>
