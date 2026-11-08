# Installation and troubleshooting

```text
THE COIN GAME VR — 0.0.89
Windows x64 extract-to-game-folder package | November 8, 2026
Author: Hoshi82
Included mod revision: menu-test F (user-tested working configuration)

INPUT SUPPORT
Gamepad or mouse/keyboard only. 6DoF motion-controller hand tracking and
controller-directed interaction are NOT supported in this release.
Motion-controller support may be explored later, with no announced schedule.
Headset rotation/position tracking is supported independently of hand controllers.

REQUIREMENTS — INSTALL BEFORE FIRST LAUNCH
- Windows 10/11 x64 and an installed copy of The Coin Game.
- Headset software and an active OpenXR runtime (tested with SteamVR).
- Microsoft Visual C++ v14 Redistributable, X64 edition.
  Install the latest supported x64 package from Microsoft's official page:
  https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/
  Official direct x64 installer link:
  https://aka.ms/vc14/vc_redist.x64.exe
  Run the installer and choose Install (or Repair for an existing installation).
  The x86 package alone is not sufficient for this x64 mod.
  Visual Studio itself is not required.
  Visual C++ runtime DLLs and the installer are NOT included in this ZIP.

QUICK INSTALL
1. Close The Coin Game.
2. Open the game's installation folder (Steam > Manage > Browse local files).
   This is the folder containing TheCoinGame.exe.
3. If you already use mods, back up your existing mod/loader files first.
4. Extract ALL CONTENTS of this ZIP directly into that folder.
   version.dll must sit beside TheCoinGame.exe, and the mod must be at
   Mods/TheCoinGameVR.dll. Do not create an extra package-name subfolder.
5. Start your headset software and SteamVR (or your configured OpenXR runtime),
   then launch the game normally through Steam. VR starts automatically.

This archive includes MelonLoader 0.7.3, the tested VR mod, its native OpenXR
bridge, and the OpenXR loader. It does not include Visual C++ runtime files.
You do not need to run the MelonLoader installer for this package.
SteamVR/headset drivers and the game itself must already be installed.

CONTROLS
F5                 Toggle VR on/off.
F9                 Recenter the headset view (yaw reference).
Xbox View / Back   Recenter, for controllers exposed through XInput.
F11                Detailed diagnostics; use only when troubleshooting.
F12                Additional technical diagnostics.

Keep using the game's normal mouse/gamepad controls for movement, turning,
interaction, grabbing, and menu navigation. Xbox View support is not a
universal button mapping for every controller; native non-XInput pads may
need Steam Input to expose an Xbox-compatible controller.

CURRENT SETTINGS
- Automatic VR startup once the camera and OpenXR are ready.
- 100% of the OpenXR runtime's recommended eye render dimensions.
  These are queried from the runtime, not fixed to a headset model.
  The tested Beyond 2e setup reported 3560 x 3560 per eye.
- Projected screen menus: 16:9, head-following, 2 meters away.
- The separate gameplay HUD and object interaction markers keep their
  previously tested placement. Action/button hints follow the input device.

TESTED CONFIGURATION
The Coin Game 1.0.11 (Unity 2022.3.62f3, Mono, x64), Windows 10,
Bigscreen Beyond 2e, SteamVR/OpenXR, Xbox-compatible gamepad.
The component files match the working installed configuration. This ZIP's
structure and checksums were verified, but a fresh installation on a clean
second computer has not been tested. Other headsets/game versions may differ.

TROUBLESHOOTING
- Missing MSVCP140.dll / VCRUNTIME140.dll / VCRUNTIME140_1.dll, or a native
  bridge load error: install/repair the Microsoft Visual C++ v14 X64
  Redistributable using the official link in REQUIREMENTS, then restart the game.
- No VR: verify the headset/runtime is running; check MelonLoader/Latest.log.
- No loader: confirm version.dll is next to TheCoinGame.exe and the ZIP was
  fully extracted. Do not mix this package with another proxy-based loader.
- Poor performance: the mod uses 100% of the runtime-recommended resolution;
  lower the runtime's render resolution for testing if necessary.
- Missing action hints: look at an interactable, press F11 before switching
  input devices, then share MelonLoader/Latest.log with the mod author.
- F11/F12 deliberately produce verbose logs and can cause a brief hitch.

DISABLE / REMOVE
To disable only this VR mod, close the game and move Mods/TheCoinGameVR.dll
out of the Mods folder. Restore it to enable the mod again.
To remove this package completely, use the included file manifest and your
backup. Do not delete files or folders belonging to another mod or the game.
Renaming/removing version.dll disables this MelonLoader installation and ALL
mods that depend on it, not just the VR mod.

No game files, save games, personal settings, or previous logs are included.
The package does not change Windows settings or install SteamVR for you.
Licenses, component sources, and file hashes: TheCoinGameVR_Docs/.

```
