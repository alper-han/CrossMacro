# CrossMacro custom themes

Drop a JSON file into this folder to add your own theme. It appears in
**Settings → Theme** after you press the refresh button next to the theme
selector (or restart the app).

## Getting started

1. Copy `_template.json` to a new file, e.g. `my-theme.json`.
2. Change `"name"` — this is what shows up in the theme list.
3. Edit the colors inside `"palette"`.
4. Press the refresh button next to the theme selector and pick your theme.

## Rules

- Colors are hex values: `#RGB`, `#RRGGBB`, or `#AARRGGBB`.
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
- The built-in themes use the exact same format and are good references:
  <https://github.com/alper-han/CrossMacro/tree/main/src/CrossMacro.UI/Themes>
