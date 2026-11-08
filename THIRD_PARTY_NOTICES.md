# Third-party components

The mod's MIT license covers original The Coin Game VR code only. Components
distributed in the release ZIP retain their original licenses and credits.
No ownership of those projects is claimed.

| Component | Included in release ZIP? | License/source |
| --- | --- | --- |
| MelonLoader 0.7.3 (Mono/x64 selection) | Yes, including version.dll | [Apache-2.0](third_party_licenses/MelonLoader-LICENSE.md), [NOTICE](third_party_licenses/MelonLoader-NOTICE.txt), [official source](https://github.com/LavaGang/MelonLoader/tree/v0.7.3) |
| MelonLoader dependencies | Yes, as supplied by upstream for Mono | Own licenses; [license texts](third_party_licenses/MelonLoader-dependencies), upstream README credits in the ZIP |
| Khronos OpenXR loader 1.1.63.0 | Yes, openxr_loader.dll | [Apache-2.0](third_party_licenses/OpenXR-LICENSE.txt), [source](https://github.com/KhronosGroup/OpenXR-SDK/tree/release-1.1.63) |
| JsonCpp, used by the OpenXR loader | Within the loader | [JsonCpp license](third_party_licenses/JsonCpp-LICENSE.txt) |
| Microsoft Visual C++ v14 runtime | No | Users install [Microsoft's X64 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist/) separately |
| The Coin Game, SteamVR, headset software | No | Separate products under their respective terms |

The release uses unmodified upstream MelonLoader files selected for this Mono
game. The .NET 6/IL2CPP components are omitted. MelonLoader does not need a
separate download for users installing this release ZIP.

The package does not contain game executables, game assemblies, assets, saves,
personal configuration, or user logs. Unity plugin headers needed to compile
the native bridge are obtained from the developer's own Unity installation.

The full upstream credits and component sources remain in
`MelonLoader/Documentation/README.md` and `TheCoinGameVR_Docs/` inside the ZIP.
