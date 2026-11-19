# macOS target-process audio experiment

An opt-in helper for Shell actions that need local sound feedback, independently
of desktop pointer recording/playback. Requires macOS 14.2+, Xcode command-line
tools, Python 3.9+ and NumPy for matching. No dependencies or permissions are
added to the normal CrossMacro application.

The helper selects exactly one running application by bundle ID, creates a
private Core Audio process tap, writes CAF audio and reports JSON timestamps.
It does not record the microphone or fall back to the global system mix, change
the output device, or mute the target. Silence, missing target, denied permission
and capture errors must not be treated as successful actions.

## Build an app bundle

Build under a stable local path so macOS can associate its System Audio Recording
permission with the helper. Use an application-specific identifier for your copy.

```sh
mkdir -p build/AudioTap.app/Contents/MacOS
cat > build/AudioTap.app/Contents/Info.plist <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>local.crossmacro.audio-tap</string>
<key>CFBundleExecutable</key><string>audio-tap</string>
<key>CFBundleName</key><string>CrossMacro Audio Tap</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>NSAudioCaptureUsageDescription</key><string>Capture sound from the selected application for local macro feedback.</string>
</dict></plist>
PLIST
clang -fobjc-arc -fno-modules -mmacosx-version-min=14.2 \
  -framework AppKit -framework CoreAudio -framework AudioToolbox \
  tools/macos-audio/audio-tap.m -o build/AudioTap.app/Contents/MacOS/audio-tap
codesign --force --sign - build/AudioTap.app
```

Run it from a normal logged-in desktop session and approve the macOS audio
capture prompt when appropriate:

```sh
python3 tools/macos-audio/audio_feedback.py capture \
  --binary build/AudioTap.app/Contents/MacOS/audio-tap \
  --bundle-id com.example.target --folder /private/path/audio-trial --seconds 8
```

The same explicit command can be placed in CrossMacro's existing Shell Command
editor action or scheduled macro. Use absolute paths in GUI-launched commands.
Capture alone does not perform input or verify a macro's completion. No new
macro format, editor widget, scheduler behavior or default capture is introduced.

For local matching, supply a JSON object mapping event names to your own
16-bit PCM WAV templates (sample rates must be multiples of 16 kHz):

```sh
python3 tools/macos-audio/audio_feedback.py match \
  --wav /private/path/audio-trial/audio.wav --templates /private/path/templates.json
python3 -m unittest discover -s tools/macos-audio -p 'test_*.py' -v
```

Normalized waveform correlation aligns short repeatable effects and tolerates
gain changes. A match is only a **candidate**, never semantic confirmation that
an intended page opened or a reward was received. Keep a later visual check.
Background audio, changed effects, ambiguous matches and silence need visual
fallback. Templates/recordings are user-generated and must remain local.
`check_run.py` is an optional offline audit helper; it never sends input.

Validated on macOS 15.3.2: target-only 48 kHz stereo capture; repeated menu effect
matching; one reward effect sample. The sample size does not establish general
accuracy for arbitrary apps. No recordings, app identifiers or account data from
the trial are included. A stable GUI action and distributed/signed packaging
remain future integration work.

Apple reference: [Capturing system audio with Core Audio taps](https://developer.apple.com/documentation/coreaudio/capturing-system-audio-with-core-audio-taps).
