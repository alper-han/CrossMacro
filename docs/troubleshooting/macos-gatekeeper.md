# Resolve A macOS Gatekeeper Warning

CrossMacro's GitHub DMGs are currently distributed without Apple Developer ID
signing or notarization. macOS can therefore block the first launch even when the
download is legitimate.

## Open The App

1. Drag CrossMacro to **Applications** and try to open it once.
2. Open **System Settings > Privacy & Security**.
3. Scroll to the security message about CrossMacro and choose **Open Anyway**.
4. Confirm **Open** in the final macOS dialog.

You can also Control-click CrossMacro in Finder, choose **Open**, and confirm the
dialog. Which option appears depends on the macOS version and the warning shown.

## Optional Terminal Method

If the graphical override is unavailable and you downloaded the DMG from the
official GitHub Releases page, remove only Apple's quarantine attribute:

```bash
xattr -dr com.apple.quarantine /Applications/CrossMacro.app
```

Then open CrossMacro from **Applications**. This is narrower than `xattr -cr`:
it removes `com.apple.quarantine` recursively without clearing every extended
attribute in the application bundle.

## Optional Download Verification

Checksum verification is recommended when you want to verify the downloaded
artifact, but it is not required to launch CrossMacro.

1. Download `SHA256SUMS` from the same
   [GitHub Release](https://github.com/alper-han/CrossMacro/releases) as the DMG.
2. Calculate the DMG checksum:

   ```bash
   shasum -a 256 CrossMacro-*-osx-*.dmg
   ```

   Compare the complete hexadecimal value with the matching filename in
   `SHA256SUMS`.

Signature inspection is also optional. Since the current DMGs are unsigned and
not notarized, `codesign` or `spctl` can report a failure for an otherwise
checksum-matching official release:

   ```bash
   codesign --verify --deep --strict --verbose=2 /Applications/CrossMacro.app
   spctl --assess --type execute --verbose=4 /Applications/CrossMacro.app
   ```

If the checksum does not match, delete the artifact and download it again. Treat
`codesign` and `spctl` as diagnostic information unless a future release
explicitly promises Developer ID signing and notarization.
