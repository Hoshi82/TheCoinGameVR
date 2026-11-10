# The Coin Game VR v0.0.90

Vehicle and room-scale fixes, based on the successfully tested **0.0.89 vehicle-test D** development build.

## Fixed

- **Golf cart tracking:** the VR player position now follows the golf cart correctly while driving and turning.
- **Room-scale movement:** physical movement works before entering the cart and after getting out.
- **Movement direction:** room-scale displacement now uses the VR camera's base orientation, keeping movement aligned with the rendered view instead of reversing horizontal directions.

The on-foot body-follow logic now yields to the game's vehicle/seat control and resumes on foot. The update also adds vehicle/camera state diagnostics to F11 and rechecks the source camera when it becomes inactive.

## Install or update

1. Install [Microsoft Visual C++ v14 Redistributable X64](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/) ([official Microsoft download](https://aka.ms/vc14/vc_redist.x64.exe)).
2. Close the game and back up any existing mod/loader files.
3. Extract **TheCoinGameVR-v0.0.90-Windows-x64.zip** directly beside `TheCoinGame.exe`, replacing the previous mod files when prompted.
4. Start your headset software and SteamVR/OpenXR runtime, then launch the game through Steam.

**MelonLoader 0.7.3 is already included. No separate MelonLoader download is needed.** Visual C++ runtime DLLs and the installer are not bundled. No game files, personal settings, logs, or empty Plugins/UserLibs folders are included.

Download the mod ZIP below, **not** GitHub's automatically generated source archives. A SHA-256 checksum file is provided alongside it.

## Tested configuration and limitations

The vehicle-test D session was successfully tested with The Coin Game 1.0.11 / Unity 2022.3.62f3 Mono x64, Windows 10, Bigscreen Beyond 2e, SteamVR/OpenXR, and an Xbox-compatible gamepad. The test covered golf cart driving, room-scale movement before entry and after exit, and movement direction, including turning/recentering checks. F11 snapshots were recorded before and after the cart test.

**Only the Bigscreen Beyond 2e has been tested so far. Meta Quest 3 and other headsets that are not native SteamVR headsets have not been tested; whether this mod works with them is unknown.** Other game versions and a fresh second PC remain unverified. This test does not establish compatibility with every vehicle or general collision/room-scale behavior in every situation.

**Gamepad or mouse/keyboard only; motion-controller support is not yet implemented.** Headset rotation and position tracking are supported independently. 6DoF motion-controller support may be explored later, with no announced schedule.

## Build identification

**v0.0.90 is the public package/release version.** To distribute the exact DLL used in the successful test, the managed binary intentionally still identifies itself as **0.0.89 vehicle-test D** in MelonLoader/startup logs. This is the new vehicle-fix build, not the previous v0.0.89 menu-test F release.

The current development source was rebuilt in Release and produced a byte-for-byte match with the installed DLL and the hash recorded in the successful test log:

`Mods/TheCoinGameVR.dll` SHA-256: `f9c545695c1f5f2d83439536007482d629577aeaa064d199fe05d38bb22a07d6`

The validated installed native bridge and OpenXR loader are retained unchanged. The package was rebuilt and checked for CRCs, paths, payload hashes, and required components; a new headset session using the repackaged ZIP has not been performed.

## Licenses

Original mod code: **MIT**, © 2026 Hoshi82. MelonLoader, OpenXR, and their dependencies retain their own licenses and credits, included in the package and in [THIRD_PARTY_NOTICES.md](https://github.com/Hoshi82/TheCoinGameVR/blob/main/THIRD_PARTY_NOTICES.md).
