# XenZarg

**Xenon 1**, **Zorgon's Revenge** and **Xenon 3 - The Genesis Probe**: a single MonoGame port of the
three Oric games for Android, iOS, macOS and Windows. When the Zorgon Battle Star is destroyed,
Zorgon's Revenge starts as the next level; rescuing Princess Roz opens the Genesis Probe; clearing
Radon starts the next, harder cycle of Xenon 1.

By PFJ, based on the IJK originals. Released under the [DILLIGAF License](LICENSE):
do what you like with the code.

The start screen shows a gameplay screenshot for each game in a triangle, with **PLAY ALL GAMES**
underneath. Choose one screenshot to play that game alone, or Play All Games for the full Xenon 1,
Zorgon's Revenge and Genesis Probe campaign.

## Faithful to the Oric

- **Colours:** only the Oric's eight pure RGB colours (`XenZarg.Core/Oric/OricColor.cs`).
- **Sprites:** captured pixel for pixel from the originals, except where noted.
  - Xenon 1 comes from real Oric screenshots at 4x scale, downsampled to the native 240x224.
  - Zorgon's Revenge comes from the original memory image, rendered with the Oric ULA attribute rules.
  - Xenon 3's shapes are reconstructed from the original's Oric screens and instructions in the same
    8-colour style (`Graphics/SpriteData.Genesis.cs`); its campaign, missions and hyperspace run
    follow the original's design.
- **Text:** the Oric ROM character set, with screen layouts and text copied from the originals (HUDs,
  instruction pages).
- **Screen:** a 240x224 virtual Oric screen, scaled with point sampling: whole multiples on desktop,
  fit to screen on mobile.
- **Sound:** synthesised square waves and noise in the style of the AY-3-8912 (`Audio/Synth.cs`).
- **Timing:** the game runs at 50 ticks per second, like a PAL Oric.
- **Reconstructed screens:** no original captures exist of Xenon's Battle Star or refuelling screens.
  They are built from the original instructions and its own sprites (the Battle Star is the Zorgon
  saucer, enlarged). Xenon 3's later planets were reconstructed from its mission instructions and the
  first planets' screens.

## The Genesis Probe

Xenon 3 adds the hyperspace run and six planets. Raise the probe's speed into the red markers and hold
it there until the jump gauge fills, then play the planet. Rundus, Somtana, Nuciro and Terra use the
teleport beam: catch two of every species and stop the red barrels rolling off the screen. Xer uses the
boomerang to slice the wormy creatures. Radon is the final storm: dodge five waves of Zorgons; there
is no weapon. Clearing Radon starts the next, harder cycle.

## Projects

| Project | Target |
|---|---|
| `XenZarg.Core` | The whole game (net10.0): scenes, Xenon levels, Zorgon missions, Genesis planets, input, audio, capture mode |
| `XenZarg.Android` | Android 6+ (tilt via the gravity sensor) |
| `XenZarg.iOS` | iOS 15+ (tilt via CoreMotion, UIScene life cycle) |
| `XenZarg.DesktopGL` | macOS (also runs on Linux and Windows) |
| `XenZarg.WindowsDX` | Windows (DirectX), packaged as MSIX for the Microsoft Store |
| `XenZarg.Tests` | xUnit tests, including scripted runs proving every Zorgon screen and Genesis planet can be completed |

Identifiers:
- Bundle ID (iOS and macOS): `uk.co.allthejohnsosns.xenzarg`. This spelling matches the provisioning
  profiles.
- Android package name: `uk.co.allthejohnsons.xenzorg`.
- Windows identity: `49556nodoid.XenZarg`.

## Controls

| | Desktop | Phone / tablet |
|---|---|---|
| Move | `Z` / `X` (cursor keys too) or the mouse | Tilt left / right (forward / back on vertical screens) |
| Fire / jump | `SPACE` or left click | Tap |
| Refuel (disengage) | Cursor up, right click, or click the fuel gauge | Tap the fuel gauge |
| Pause | `P` | Three-finger tap |
| Menu | `Esc` | Back |

The Genesis Probe uses the same controls: left/right steers the probe and the boomerang, and fire
teleports a specimen or throws the boomerang. On Radon there is no weapon: just dodge the Zorgons.

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
scripted winning routes as the tests; Genesis uses autopilots for each mission. It saves the native
frames, and records the sound effects so the videos get a matching soundtrack.

## Licence

The XenZarg code, tools and documentation are released under the **[DILLIGAF License](LICENSE)**:
use, copy, change, share or sell them however you like, with no warranty.

The licence can't cover what isn't ours:
- the original Xenon 1, Zorgon's Revenge and Xenon 3 - The Genesis Probe games, and the graphics
  captured or reconstructed from them;
- the Oric ROM font;
- third-party packages such as MonoGame, which keep their own licences.
