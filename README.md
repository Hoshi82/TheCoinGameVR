# The Coin Game VR

A community PC VR mod for **The Coin Game**, by **Hoshi82**.
Play with head tracking, stereo rendering, mouse/gamepad controls, head-directed
interaction, physical object grabbing, and VR-friendly menus.

> **Input support: gamepad or mouse/keyboard only. 6DoF motion-controller hand
> tracking and controller-directed interactions are not supported yet.**
> Motion-controller support may be explored in the future; there is no committed
> release date. Headset tracking is supported independently of hand controllers.

**[Download v0.0.89](https://github.com/Hoshi82/TheCoinGameVR/releases/tag/v0.0.89)** ·
**[Installation](docs/INSTALLATION.md)** · **[Report a problem](https://github.com/Hoshi82/TheCoinGameVR/issues/new/choose)**

## Download and install

Download `TheCoinGameVR-v0.0.89-Windows-x64.zip` from
[Releases](https://github.com/Hoshi82/TheCoinGameVR/releases). **Do not use GitHub's “Source code” ZIP to install the mod.**

1. Install the **Microsoft Visual C++ v14 Redistributable — X64** from
   [Microsoft's official page](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/).
2. Close the game. Back up any existing mod/loader installation.
3. Extract the release ZIP directly into the folder containing `TheCoinGame.exe`.
4. Start your headset software and SteamVR/OpenXR runtime, then launch the game
   normally through Steam. VR starts automatically when the camera/runtime are ready.

**MelonLoader 0.7.3 is already included in the release package. You do not need to
download or install MelonLoader separately.** Visual C++ runtime DLLs and the
Visual C++ installer are not included; users install that prerequisite themselves.

## Features

- Stereo rendering through a native Unity/D3D11/OpenXR bridge.
- Full headset rotation and position tracking, with yaw recentering.
- Mouse/gamepad movement and horizontal turning; headset-driven vertical aim.
- Head-directed interaction and physical object grabbing using the game's controls.
- Automatic VR startup and restoration across scene changes.
- Head-following **16:9 menus at 2 meters**, plus the separate gameplay HUD and
  object interaction markers.
- Action/button hints that update when switching input devices.
- Eye dimensions queried from OpenXR, at **100% of the runtime recommendation**.

## Controls

| Input | Action |
| --- | --- |
| F5 | Toggle VR |
| F9 | Recenter headset yaw |
| Xbox View / Back | Recenter through XInput |
| F11 | Capture detailed UI/interaction diagnostics |
| F12 | Additional technical diagnostics |

Use the game's normal controls for movement, interaction, grabbing, and menus.
View/Back support applies to XInput-compatible pads, including Xbox controllers.
Other controller types may need Steam Input to expose an Xbox-compatible device.
F11/F12 intentionally produce verbose diagnostics and may cause a brief hitch.

## Requirements and compatibility

**Headset compatibility: So far, this mod has only been tested with the Bigscreen Beyond 2e. Meta Quest 3 and other headsets that are not native SteamVR headsets have not been tested; whether the mod works with them is unknown.**


- The Coin Game for Windows, x64.
- Windows 10/11 x64, headset software, and a configured OpenXR runtime.
- [Microsoft Visual C++ v14 Redistributable X64](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/).

Tested with **The Coin Game 1.0.11**, Unity 2022.3.62f3 Mono, Windows 10,
**Bigscreen Beyond 2e**, SteamVR/OpenXR, and an Xbox-compatible gamepad.
The tested runtime recommended **3560 × 3560 per eye**; this is queried dynamically,
not hardcoded or equated with the physical panel resolution.

The packaged files were tested by removing the mod/loader from the game,
confirming the normal game started, and reinstalling from the release ZIP.
Other game versions, headsets, and a fresh second PC have not been verified.

## Possible future development

6DoF motion-controller support may be investigated later. It is not implemented
in this release, and no delivery date or guarantee is announced.

## Troubleshooting

See [installation and troubleshooting](docs/INSTALLATION.md). If a problem persists,
[open an issue](https://github.com/Hoshi82/TheCoinGameVR/issues/new/choose) with the game/mod version, headset/runtime,
steps to reproduce, and a relevant `MelonLoader/Latest.log`.
Review logs for personal paths or other private information before sharing them.

## Development

The repository includes the C# MelonLoader mod and C++ OpenXR bridge.
See [building from source](docs/BUILDING.md). Game assemblies, Unity SDK headers,
OpenXR SDK binaries, and local machine configuration are not included.

## License and credits

Original The Coin Game VR source code: **MIT**, © 2026 Hoshi82 — see [LICENSE](LICENSE).

**Third-party components keep their own licenses; the MIT license for this mod
does not relicense MelonLoader, OpenXR, or their dependencies.** The release ZIP
retains their notices and license texts. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Thanks to **Lava Gang / MelonLoader**, **Khronos / OpenXR**, and their contributors.
The game, SteamVR, and headset software are separate products, not included here.
This community project is not affiliated with or endorsed by the game developer,
Unity, Valve, Bigscreen, Lava Gang, or Khronos.
