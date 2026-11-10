# Changelog

## 0.0.90 — 2026-11-10

- Fixed VR player position following the golf cart while driving.
- Restored room-scale movement before vehicle entry and after exit.
- Aligned room-scale movement direction with the VR camera base orientation.
- Suspend on-foot body follow while the game controls the seated player.
- Recheck inactive source cameras and include vehicle state in F11 diagnostics.
- Packages the exact successfully tested 0.0.89 vehicle-test D DLL; the internal
  version marker remains unchanged. Public package version: v0.0.90.
- Retains MelonLoader 0.7.3, existing native/OpenXR loaders, licenses, and input
  and headset compatibility limitations.

See [full release notes](docs/RELEASE_v0.0.90.md).

## 0.0.89 — 2026-11-08

First packaged public release, based on the user-tested menu-test F revision.

- Consistent early/final camera yaw handling and reduced automatic diagnostics.
- Automatic VR startup and scene restoration.
- Xbox View/Back recentering alongside F9.
- Full runtime-recommended eye render dimensions.
- Head-following 16:9 menu projection at 2 meters.
- Restored action hints and live mouse/gamepad glyph updates.
- Includes MelonLoader 0.7.3 Mono/x64 and the validated native/OpenXR loaders.
- Visual C++ runtime is a separate documented prerequisite.

The experimental fixed-in-world menu approach is not enabled in this release.
