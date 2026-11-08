# Building from source

## Tools and dependencies

- Visual Studio 2022 with MSVC v143 C++ tools and a Windows SDK.
- .NET Framework 4.8 targeting pack.
- Your own installation of The Coin Game and MelonLoader 0.7.3.
- [Khronos OpenXR SDK](https://github.com/KhronosGroup/OpenXR-SDK), including
  an x64 Release build of openxr_loader.lib. The packaged loader is 1.1.63.0.
- Unity 2022.3.62f3 plugin API headers from an appropriate Unity installation.

No proprietary game assemblies or Unity plugin headers are distributed here.

## Configure paths

Copy `Build.Local.props.example` to `Build.Local.props` at the repository root.
Edit the four paths to match your machine. The local file is ignored by Git.
`TheCoinGameDir` supplies MelonLoader and Unity reference assemblies;
`OpenXRSdkDir` supplies the include directory; `OpenXRLoaderLibDir` supplies
openxr_loader.lib; `UnityPluginApiDir` supplies IUnity*.h.

## Compile

Open `TheCoinGameVR.sln`, select **Release | x64**, and build. Alternatively,
use a Visual Studio developer command prompt:

```bat
msbuild TheCoinGameVR.sln /t:Build /p:Configuration=Release /p:Platform=x64 /m
```

The solution maps the managed project to Any CPU and the native project to x64.
The existing MSB3270 warning about the x64 MelonLoader reference is known;
the game itself runs x64. This publication copy preserves that tested mapping.

Outputs:
- `TheCoinGameVR/bin/Release/TheCoinGameVR.dll`
- `x64/Release/TCGVR_OpenXR.dll`

Builds do not automatically install into the game. Back up the installed files
before testing. The v0.0.89 release uses the validated existing native binary,
not a newly rebuilt native DLL; a local rebuild is not guaranteed byte-identical.
The managed code corresponds to the tested menu-test F configuration.

Check head tracking, stereo/world stability, recentering, controller/mouse aim,
grabbing, UI hints, menus, and scene transitions in the headset after changes.
Successful compilation alone does not establish correct VR behavior.
