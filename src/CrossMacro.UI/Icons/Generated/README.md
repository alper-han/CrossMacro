# Generated emoji icons

The `*EmojiIcon.g.cs` files are SkiaSharp `SKPictureRecorder` recordings of 23
vector icons. `EmojiAppIcon` maps each `AppIcon` value to one of them.

## Provenance

- Source art: the `128x128` SVG files of Google's **Noto Color Emoji**
  (<https://github.com/googlefonts/noto-emoji>, `svg/` directory). The SVGs were
  committed under `src/CrossMacro.UI/Assets/Emoji/NotoColorEmoji/Svg/` from commit
  `02c4c4c1` until commit `4dcf087c` converted them to C# and deleted them.
  Recover them with `git show 4dcf087c^:src/CrossMacro.UI/Assets/Emoji/NotoColorEmoji/Svg/<name>.svg`.
- License of the source art: Apache License 2.0 (the Noto Color Emoji font itself is
  SIL OFL 1.1). Noto Color Emoji is Copyright Google LLC. The license text is at
  <https://github.com/googlefonts/noto-emoji/blob/main/LICENSE>.
- File name to SVG name: `<Name>EmojiIcon.g.cs` was generated from
  `<name>.svg` (camelCase), for example `EditNoteEmojiIcon.g.cs` from `editNote.svg`.
  The Unicode code point behind each SVG is not recorded in the repository.

## Regeneration

The generator was never committed. The output shape (`skPictureRecorder0`,
`skPath0`, `Record()` + `Picture`) is that of the `svgc` tool of the Svg.Skia
project (<https://github.com/wieslawsoltes/Svg.Skia>), which the repository already
used at the time (`Svg.Controls.Skia.Avalonia`). That attribution is an inference
from the output, not a recorded fact.

To regenerate or add an icon, restore the SVGs from history as above, run `svgc`
from Svg.Skia on each file to emit a static class with a `Picture` property, set the
namespace to `CrossMacro.UI.Icons.Generated`, and add the class name to
`EmojiAppIcon`. Do not hand-edit the generated files.
