# XenZarg

**Xenon 1** followed by **Zorgon's Revenge**: a single MonoGame port of the two 1983 Oric games for
Android, iOS, macOS and Windows. When the Zorgon Battle Star is destroyed, Zorgon's Revenge starts
as the next level; rescuing Princess Roz starts the next, harder cycle of Xenon 1.

By PFJ, based on the PSS originals.

## Faithful to the Oric

- **Colours:** only the Oric's eight pure RGB colours (`XenZarg.Core/Oric/OricColor.cs`).
- **Sprites:** captured pixel for pixel from the originals.
  - Xenon 1 comes from real Oric screenshots at 4x scale, downsampled to the native 240x224.
  - Zorgon's Revenge comes from the original memory image, rendered with the Oric ULA attribute rules
    (`Graphics/SpriteData.*.cs`).
- **Text:** the Oric ROM character set, with screen layouts and text copied from the originals (HUDs,
  instruction pages).
- **Screen:** a 240x224 virtual Oric screen, scaled with point sampling: whole multiples on desktop,
  fit to screen on mobile.
- **Sound:** synthesised square waves and noise in the style of the AY-3-8912 (`Audio/Synth.cs`).
- **Timing:** the game runs at 50 ticks per second, like a PAL Oric.
- **Reconstructed screens:** no original captures exist of Xenon's Battle Star or refuelling screens.
  They are built from the original instructions and its own sprites (the Battle Star is the Zorgon
  saucer, enlarged).

## Projects

| Project | Target |
|---|---|
| `XenZarg.Core` | The whole game (net10.0): scenes, Xenon levels, Zorgon missions, input, audio, capture mode |
| `XenZarg.Android` | Android 6+ (tilt via the gravity sensor) |
| `XenZarg.iOS` | iOS 15+ (tilt via CoreMotion, UIScene life cycle) |
| `XenZarg.DesktopGL` | macOS (also runs on Linux and Windows) |
| `XenZarg.WindowsDX` | Windows (DirectX), packaged as MSIX for the Microsoft Store |
| `XenZarg.Tests` | xUnit tests, including scripted runs proving every Zorgon screen can be completed |

Identifiers:
- Bundle and package ID: `uk.co.allthejohnsosns.xenzarg`. This spelling matches the provisioning
  profiles.
- Windows identity: `49556nodoid.XenZarg`.

## Controls

| | Desktop | Phone / tablet |
|---|---|---|
| Move | `Z` / `X` (cursor keys too) or the mouse | Tilt left / right (forward / back on vertical screens) |
| Fire / jump | `SPACE` or left click | Tap |
| Refuel (disengage) | Cursor up, right click, or click the fuel gauge | Tap the fuel gauge |
| Pause | `P` | Three-finger tap |
| Menu | `Esc` | Back |

## Build and run

```sh
dotnet test XenZarg.Tests                      # unit tests
dotnet run --project XenZarg.DesktopGL         # play on the Mac
```

Release builds go to `release/` (git-ignored):

```sh
tools/build_release.sh       # Android .aab/.apk (key ~/keys/xenzarg-upload.jks, password in Keychain) and iOS .ipa
tools/build_desktop.sh       # macOS universal .app + App Store .pkg   (tools/build_desktop.sh dev: locally runnable)
tools/build_windows.sh       # Windows x64 + arm64 .msix/.zip, built and tested in the Parallels "Windows 11" VM
```

Provisioning profiles and signing keys are read from `~/Downloads`, `~/keys` and the Keychain, and
are never committed.

## Store assets

`stores/` holds the listing copy (`LISTING.md`), the privacy policy, and screenshots and videos for
every store. To regenerate them:

```sh
XenZarg --capture <dir>/desktop stills          # from the DesktopGL build
XenZarg --capture <dir>/mobile stills --mobile
XenZarg --capture <dir>/video-desktop videos
XenZarg --capture <dir>/video-mobile videos --mobile
tools/build_windows.sh capture                  # Windows stills, captured by the Windows build in Parallels
python3 tools/make_store_assets.py <dir>        # with the Windows stills copied to <dir>/windows/stills
python3 tools/make_store_art.py                 # Microsoft Store super hero art and Xbox images
python3 tools/check_listing.py                  # character limits
```

Capture mode plays each screen on autopilot. Xenon uses an AI pilot; Zorgon follows the same
scripted winning routes as the tests. It saves the native frames, and records the sound effects so
the videos get a matching soundtrack.
