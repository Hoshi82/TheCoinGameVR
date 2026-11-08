#include "pch.h"

#include <windows.h>
#include <d3d11.h>
#include <dxgi.h>

#define XR_USE_GRAPHICS_API_D3D11
#define XR_USE_PLATFORM_WIN32

#include <openxr/openxr.h>
#include <openxr/openxr_platform.h>

#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsD3D11.h"

#include <atomic>
#include <cstring>
#include <mutex>
#include <thread>
#include <vector>


// ============================================================
// OpenXR
// ============================================================

static XrInstance g_instance = XR_NULL_HANDLE;
static XrSystemId g_systemId = XR_NULL_SYSTEM_ID;
static XrSession g_session = XR_NULL_HANDLE;

static XrSpace g_localSpace = XR_NULL_HANDLE;
static XrSpace g_viewSpace = XR_NULL_HANDLE;

static XrEnvironmentBlendMode g_environmentBlendMode =
XR_ENVIRONMENT_BLEND_MODE_OPAQUE;


// ============================================================
// Session
// ============================================================

static std::atomic<int> g_sessionState{
    XR_SESSION_STATE_UNKNOWN
};

static std::atomic<bool> g_sessionBegun{
    false
};


// ============================================================
// Extensions
// ============================================================

static PFN_xrConvertWin32PerformanceCounterToTimeKHR
g_convertQpcToXrTime = nullptr;


// ============================================================
// Unity / D3D11
// ============================================================

static IUnityInterfaces* g_unityInterfaces = nullptr;
static IUnityGraphics* g_unityGraphics = nullptr;
static IUnityGraphicsD3D11* g_unityGraphicsD3D11 = nullptr;

static ID3D11Device* g_d3d11Device = nullptr;


// ============================================================
// OpenXR D3D11 Anforderungen
// ============================================================

static XrGraphicsRequirementsD3D11KHR
g_graphicsRequirements{
    XR_TYPE_GRAPHICS_REQUIREMENTS_D3D11_KHR
};


// ============================================================
// Tracking Thread
// ============================================================

static std::thread g_trackingThread;

static std::atomic<bool> g_trackingThreadShouldRun{
    false
};

static std::atomic<bool> g_trackingThreadRunning{
    false
};

static std::atomic<int> g_lastTrackingResult{
    0
};


// Synchronisiert einen zusammengehoerigen Head-/Stereo-Tracking-Sample.
static std::mutex g_trackingSampleMutex;

// 0.0.37: kein pauschaler Zeit-Offset mehr.
// Stattdessen wird fuer jeden gerenderten VR-Frame ein fester
// Tracking-Snapshot aufgenommen und exakt derselbe Snapshot
// spaeter im OpenXR Projection Layer verwendet.
static std::atomic<int> g_trackingPredictionMilliseconds{
    0
};


// ============================================================
// Head Pose
// ============================================================

struct HeadPoseData
{
    float posX = 0.0f;
    float posY = 0.0f;
    float posZ = 0.0f;

    float rotX = 0.0f;
    float rotY = 0.0f;
    float rotZ = 0.0f;
    float rotW = 1.0f;

    bool valid = false;
};

static HeadPoseData g_headPose;
static std::mutex g_headPoseMutex;


// ============================================================
// Stereo Daten
// ============================================================

struct StereoEyeData
{
    float posX = 0.0f;
    float posY = 0.0f;
    float posZ = 0.0f;

    float rotX = 0.0f;
    float rotY = 0.0f;
    float rotZ = 0.0f;
    float rotW = 1.0f;

    float angleLeft = 0.0f;
    float angleRight = 0.0f;
    float angleUp = 0.0f;
    float angleDown = 0.0f;

    bool valid = false;
};


struct StereoData
{
    StereoEyeData left;
    StereoEyeData right;

    uint32_t viewCount = 0;

    uint32_t recommendedWidth = 0;
    uint32_t recommendedHeight = 0;
    uint32_t recommendedSampleCount = 1;

    bool configurationValid = false;
    bool posesValid = false;
};


static StereoData g_stereoData;
static std::mutex g_stereoMutex;


// ============================================================
// 0.0.37 Render-Pose Snapshot
// ============================================================
//
// Unity nimmt vor dem Rendern eines VR-Frames EINEN Snapshot auf.
// Solange dieser Snapshot "in flight" ist, darf er nicht durch
// einen spaeteren Unity-Frame ueberschrieben werden.
// Der Render-Thread submitted anschliessend exakt dieselben Eye-Posen
// und FOV-Werte an OpenXR, mit denen Unity gerendert hat.
// ============================================================

struct RenderPoseSnapshot
{
    HeadPoseData head;
    StereoEyeData left;
    StereoEyeData right;

    bool valid = false;
    unsigned long long generation = 0;
};

static RenderPoseSnapshot g_renderPoseSnapshot;
static std::mutex g_renderPoseMutex;

static std::atomic<bool> g_renderPoseInFlight{
    false
};

static std::atomic<unsigned long long> g_renderPoseGeneration{
    0
};

static std::atomic<unsigned long long> g_lastSubmittedRenderPoseGeneration{
    0
};


// ============================================================
// OpenXR Swapchains
// ============================================================

static XrSwapchain g_leftSwapchain = XR_NULL_HANDLE;
static XrSwapchain g_rightSwapchain = XR_NULL_HANDLE;

static std::vector<XrSwapchainImageD3D11KHR>
g_leftSwapchainImages;

static std::vector<XrSwapchainImageD3D11KHR>
g_rightSwapchainImages;

static int64_t g_swapchainFormat = 0;

static uint32_t g_swapchainWidth = 0;
static uint32_t g_swapchainHeight = 0;

static std::atomic<bool> g_swapchainsCreated{
    false
};

static std::mutex g_swapchainMutex;


// ============================================================
// Unity Texturen
// ============================================================

static ID3D11Resource* g_leftUnityTexture = nullptr;
static ID3D11Resource* g_rightUnityTexture = nullptr;

static uint32_t g_unityTextureWidth = 0;
static uint32_t g_unityTextureHeight = 0;

static std::atomic<bool> g_unityTexturesRegistered{
    false
};

static std::mutex g_unityTextureMutex;


// ============================================================
// Alter Acquire Test
// ============================================================

static std::atomic<int> g_lastAcquireTestResult{ 0 };
static std::atomic<int> g_lastLeftImageIndex{ -1 };
static std::atomic<int> g_lastRightImageIndex{ -1 };

static std::atomic<unsigned long long>
g_successfulAcquireTests{
    0
};


// ============================================================
// Texture Copy Diagnose
// ============================================================

static std::atomic<int> g_lastTextureCopyResult{ 0 };

static std::atomic<int> g_lastTextureCopyLeftIndex{ -1 };
static std::atomic<int> g_lastTextureCopyRightIndex{ -1 };

static std::atomic<unsigned long long>
g_successfulTextureCopies{
    0
};

static std::atomic<int> g_lastLeftSourceFormat{ 0 };
static std::atomic<int> g_lastRightSourceFormat{ 0 };


// ============================================================
// 0.0.31 Frame Submit Diagnose
//
// Step:
// 0  = nichts
// 1  = Callback betreten
// 2  = vor xrWaitFrame
// 3  = xrWaitFrame fertig
// 4  = vor xrBeginFrame
// 5  = xrBeginFrame fertig
// 6  = xrLocateViews fertig
// 7  = linkes Image bereit
// 8  = rechtes Image bereit
// 9  = Texture Copy fertig
// 10 = Images released
// 11 = DIREKT VOR xrEndFrame
// 12 = xrEndFrame zur�ckgekehrt
// ============================================================

static std::atomic<int> g_frameSubmitStep{ 0 };

static std::atomic<int> g_lastFrameSubmitResult{ 0 };

static std::atomic<int> g_lastWaitFrameResult{ 0 };
static std::atomic<int> g_lastBeginFrameResult{ 0 };
static std::atomic<int> g_lastEndFrameResult{ 0 };

static std::atomic<int> g_lastShouldRender{ 0 };

static std::atomic<int> g_lastSubmitLeftIndex{ -1 };
static std::atomic<int> g_lastSubmitRightIndex{ -1 };

static std::atomic<unsigned long long>
g_successfulSubmittedFrames{
    0
};


// ============================================================
// Plugin Events
// ============================================================

static const int TCGVR_EVENT_COPY_STEREO_TEXTURES =
1001;

static const int TCGVR_EVENT_SUBMIT_SINGLE_FRAME =
1002;


// ============================================================
// Helper
// ============================================================

static bool LuidEquals(
    const LUID& a,
    const LUID& b)
{
    return
        a.HighPart == b.HighPart
        &&
        a.LowPart == b.LowPart;
}


// ============================================================
// R8G8B8A8 Formatfamilie
// ============================================================

static bool IsRGBA8Family(
    DXGI_FORMAT format)
{
    return
        format == DXGI_FORMAT_R8G8B8A8_TYPELESS
        ||
        format == DXGI_FORMAT_R8G8B8A8_UNORM
        ||
        format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;
}


// ============================================================
// B8G8R8A8 Formatfamilie
// ============================================================

static bool IsBGRA8Family(
    DXGI_FORMAT format)
{
    return
        format == DXGI_FORMAT_B8G8R8A8_TYPELESS
        ||
        format == DXGI_FORMAT_B8G8R8A8_UNORM
        ||
        format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
}


// ============================================================
// Copy kompatibel?
// ============================================================

static bool FormatsCompatibleForCopy(
    DXGI_FORMAT source,
    DXGI_FORMAT destination)
{
    if (source == destination)
    {
        return true;
    }


    if (
        IsRGBA8Family(source)
        &&
        IsRGBA8Family(destination)
        )
    {
        return true;
    }


    if (
        IsBGRA8Family(source)
        &&
        IsBGRA8Family(destination)
        )
    {
        return true;
    }


    return false;
}


// ============================================================
// Head l�schen
// ============================================================

static void ClearHeadPose()
{
    std::lock_guard<std::mutex>
        lock(g_headPoseMutex);


    g_headPose =
        HeadPoseData{};
}


// ============================================================
// Stereo l�schen
// ============================================================

static void ClearStereoPoses()
{
    std::lock_guard<std::mutex>
        lock(g_stereoMutex);


    g_stereoData.left =
        StereoEyeData{};


    g_stereoData.right =
        StereoEyeData{};


    g_stereoData.posesValid =
        false;
}


// ============================================================
// Unity Texturen l�schen
// ============================================================

static void ClearUnityTexturePointers()
{
    std::lock_guard<std::mutex>
        lock(g_unityTextureMutex);


    g_leftUnityTexture =
        nullptr;


    g_rightUnityTexture =
        nullptr;


    g_unityTextureWidth =
        0;


    g_unityTextureHeight =
        0;


    g_unityTexturesRegistered.store(
        false
    );
}


// ============================================================
// Swapchains l�schen
// ============================================================

static void DestroySwapchains()
{
    std::lock_guard<std::mutex>
        lock(g_swapchainMutex);


    g_leftSwapchainImages.clear();

    g_rightSwapchainImages.clear();


    if (
        g_leftSwapchain !=
        XR_NULL_HANDLE
        )
    {
        xrDestroySwapchain(
            g_leftSwapchain
        );


        g_leftSwapchain =
            XR_NULL_HANDLE;
    }


    if (
        g_rightSwapchain !=
        XR_NULL_HANDLE
        )
    {
        xrDestroySwapchain(
            g_rightSwapchain
        );


        g_rightSwapchain =
            XR_NULL_HANDLE;
    }


    g_swapchainFormat =
        0;


    g_swapchainWidth =
        0;


    g_swapchainHeight =
        0;


    g_swapchainsCreated.store(
        false
    );
}


// ============================================================
// Unity Graphics Event
// ============================================================

static void UNITY_INTERFACE_API
OnGraphicsDeviceEvent(
    UnityGfxDeviceEventType eventType)
{
    if (
        eventType ==
        kUnityGfxDeviceEventInitialize
        )
    {
        if (
            g_unityInterfaces !=
            nullptr
            )
        {
            g_unityGraphicsD3D11 =
                g_unityInterfaces
                ->Get<IUnityGraphicsD3D11>();


            if (
                g_unityGraphicsD3D11 !=
                nullptr
                )
            {
                g_d3d11Device =
                    g_unityGraphicsD3D11
                    ->GetDevice();
            }
        }
    }
    else if (
        eventType ==
        kUnityGfxDeviceEventShutdown
        )
    {
        ClearUnityTexturePointers();


        g_d3d11Device =
            nullptr;


        g_unityGraphicsD3D11 =
            nullptr;
    }
}


// ============================================================
// Unity Plugin Load
// ============================================================

extern "C"
void UNITY_INTERFACE_EXPORT
UNITY_INTERFACE_API
UnityPluginLoad(
    IUnityInterfaces* unityInterfaces)
{
    g_unityInterfaces =
        unityInterfaces;


    if (
        g_unityInterfaces ==
        nullptr
        )
    {
        return;
    }


    g_unityGraphics =
        g_unityInterfaces
        ->Get<IUnityGraphics>();


    if (
        g_unityGraphics !=
        nullptr
        )
    {
        g_unityGraphics
            ->RegisterDeviceEventCallback(
                OnGraphicsDeviceEvent
            );


        OnGraphicsDeviceEvent(
            kUnityGfxDeviceEventInitialize
        );
    }
}


// ============================================================
// Unity Plugin Unload
// ============================================================

extern "C"
void UNITY_INTERFACE_EXPORT
UNITY_INTERFACE_API
UnityPluginUnload()
{
    if (
        g_unityGraphics !=
        nullptr
        )
    {
        g_unityGraphics
            ->UnregisterDeviceEventCallback(
                OnGraphicsDeviceEvent
            );
    }


    ClearUnityTexturePointers();


    g_d3d11Device =
        nullptr;


    g_unityGraphicsD3D11 =
        nullptr;


    g_unityGraphics =
        nullptr;


    g_unityInterfaces =
        nullptr;
}


// ============================================================
// Blend Mode w�hlen
// ============================================================

static int ChooseEnvironmentBlendMode()
{
    uint32_t count =
        0;


    XrResult result =
        xrEnumerateEnvironmentBlendModes(
            g_instance,
            g_systemId,
            XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO,
            0,
            &count,
            nullptr
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    if (
        count ==
        0
        )
    {
        return -1800;
    }


    std::vector<XrEnvironmentBlendMode>
        modes(
            count
        );


    result =
        xrEnumerateEnvironmentBlendModes(
            g_instance,
            g_systemId,
            XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO,
            count,
            &count,
            modes.data()
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    for (
        XrEnvironmentBlendMode mode :
    modes
        )
    {
        if (
            mode ==
            XR_ENVIRONMENT_BLEND_MODE_OPAQUE
            )
        {
            g_environmentBlendMode =
                mode;


            return 1;
        }
    }


    g_environmentBlendMode =
        modes[0];


    return 1;
}


// ============================================================
// Events
// ============================================================

static int ProcessOpenXREvents()
{
    if (
        g_instance ==
        XR_NULL_HANDLE
        )
    {
        return -2200;
    }


    XrEventDataBuffer eventData{};

    eventData.type =
        XR_TYPE_EVENT_DATA_BUFFER;


    while (true)
    {
        XrResult result =
            xrPollEvent(
                g_instance,
                &eventData
            );


        if (
            result ==
            XR_EVENT_UNAVAILABLE
            )
        {
            break;
        }


        if (XR_FAILED(result))
        {
            return
                static_cast<int>(
                    result
                    );
        }


        if (
            eventData.type ==
            XR_TYPE_EVENT_DATA_SESSION_STATE_CHANGED
            )
        {
            auto* stateChanged =
                reinterpret_cast<
                XrEventDataSessionStateChanged*
                >(
                    &eventData
                    );


            if (
                stateChanged->session ==
                g_session
                )
            {
                g_sessionState.store(
                    static_cast<int>(
                        stateChanged->state
                        )
                );


                if (
                    stateChanged->state ==
                    XR_SESSION_STATE_READY
                    )
                {
                    if (
                        !g_sessionBegun.load()
                        )
                    {
                        XrSessionBeginInfo
                            beginInfo{};


                        beginInfo.type =
                            XR_TYPE_SESSION_BEGIN_INFO;


                        beginInfo
                            .primaryViewConfigurationType =
                            XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;


                        XrResult beginResult =
                            xrBeginSession(
                                g_session,
                                &beginInfo
                            );


                        if (
                            XR_FAILED(
                                beginResult
                            )
                            )
                        {
                            return
                                static_cast<int>(
                                    beginResult
                                    );
                        }


                        g_sessionBegun.store(
                            true
                        );
                    }
                }


                if (
                    stateChanged->state ==
                    XR_SESSION_STATE_STOPPING
                    )
                {
                    if (
                        g_sessionBegun.load()
                        )
                    {
                        xrEndSession(
                            g_session
                        );


                        g_sessionBegun.store(
                            false
                        );
                    }


                    ClearHeadPose();

                    ClearStereoPoses();
                }


                if (
                    stateChanged->state ==
                    XR_SESSION_STATE_EXITING
                    ||
                    stateChanged->state ==
                    XR_SESSION_STATE_LOSS_PENDING
                    )
                {
                    g_sessionBegun.store(
                        false
                    );


                    ClearHeadPose();

                    ClearStereoPoses();
                }
            }
        }


        eventData = {};

        eventData.type =
            XR_TYPE_EVENT_DATA_BUFFER;
    }


    return 1;
}


// ============================================================
// Aktuelle Zeit
// ============================================================

static int GetCurrentXrTime(
    XrTime* xrTime)
{
    if (
        xrTime ==
        nullptr
        )
    {
        return -3000;
    }


    if (
        g_convertQpcToXrTime ==
        nullptr
        )
    {
        return -3002;
    }


    LARGE_INTEGER qpc{};


    if (
        !QueryPerformanceCounter(
            &qpc
        )
        )
    {
        return -3003;
    }


    XrResult result =
        g_convertQpcToXrTime(
            g_instance,
            &qpc,
            xrTime
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    return 1;
}


// ============================================================
// Head Tracking
// ============================================================

static int UpdateHeadPose(
    XrTime xrTime)
{
    if (
        !g_sessionBegun.load()
        )
    {
        return -3101;
    }


    XrSpaceLocation location{};

    location.type =
        XR_TYPE_SPACE_LOCATION;


    XrResult result =
        xrLocateSpace(
            g_viewSpace,
            g_localSpace,
            xrTime,
            &location
        );


    if (XR_FAILED(result))
    {
        ClearHeadPose();


        return
            static_cast<int>(
                result
                );
    }


    const XrSpaceLocationFlags
        requiredFlags =
        XR_SPACE_LOCATION_POSITION_VALID_BIT
        |
        XR_SPACE_LOCATION_ORIENTATION_VALID_BIT;


    if (
        (
            location.locationFlags
            &
            requiredFlags
            )
        !=
        requiredFlags
        )
    {
        ClearHeadPose();

        return 0;
    }


    {
        std::lock_guard<std::mutex>
            lock(g_headPoseMutex);


        g_headPose.posX =
            location.pose.position.x;


        g_headPose.posY =
            location.pose.position.y;


        g_headPose.posZ =
            location.pose.position.z;


        g_headPose.rotX =
            location.pose.orientation.x;


        g_headPose.rotY =
            location.pose.orientation.y;


        g_headPose.rotZ =
            location.pose.orientation.z;


        g_headPose.rotW =
            location.pose.orientation.w;


        g_headPose.valid =
            true;
    }


    return 1;
}


// ============================================================
// Stereo Tracking
// ============================================================

static int UpdateStereoViews(
    XrTime xrTime)
{
    if (
        !g_sessionBegun.load()
        )
    {
        return -3201;
    }


    XrViewLocateInfo locateInfo{};

    locateInfo.type =
        XR_TYPE_VIEW_LOCATE_INFO;


    locateInfo.viewConfigurationType =
        XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;


    locateInfo.displayTime =
        xrTime;


    locateInfo.space =
        g_localSpace;


    XrViewState viewState{};

    viewState.type =
        XR_TYPE_VIEW_STATE;


    XrView views[2]{};


    views[0].type =
        XR_TYPE_VIEW;


    views[1].type =
        XR_TYPE_VIEW;


    uint32_t viewCountOutput =
        0;


    XrResult result =
        xrLocateViews(
            g_session,
            &locateInfo,
            &viewState,
            2,
            &viewCountOutput,
            views
        );


    if (XR_FAILED(result))
    {
        ClearStereoPoses();


        return
            static_cast<int>(
                result
                );
    }


    if (
        viewCountOutput <
        2
        )
    {
        ClearStereoPoses();

        return -3203;
    }


    const XrViewStateFlags
        requiredFlags =
        XR_VIEW_STATE_POSITION_VALID_BIT
        |
        XR_VIEW_STATE_ORIENTATION_VALID_BIT;


    if (
        (
            viewState.viewStateFlags
            &
            requiredFlags
            )
        !=
        requiredFlags
        )
    {
        ClearStereoPoses();

        return 0;
    }


    {
        std::lock_guard<std::mutex>
            lock(g_stereoMutex);


        g_stereoData.left.posX =
            views[0].pose.position.x;

        g_stereoData.left.posY =
            views[0].pose.position.y;

        g_stereoData.left.posZ =
            views[0].pose.position.z;

        g_stereoData.left.rotX =
            views[0].pose.orientation.x;

        g_stereoData.left.rotY =
            views[0].pose.orientation.y;

        g_stereoData.left.rotZ =
            views[0].pose.orientation.z;

        g_stereoData.left.rotW =
            views[0].pose.orientation.w;

        g_stereoData.left.angleLeft =
            views[0].fov.angleLeft;

        g_stereoData.left.angleRight =
            views[0].fov.angleRight;

        g_stereoData.left.angleUp =
            views[0].fov.angleUp;

        g_stereoData.left.angleDown =
            views[0].fov.angleDown;

        g_stereoData.left.valid =
            true;


        g_stereoData.right.posX =
            views[1].pose.position.x;

        g_stereoData.right.posY =
            views[1].pose.position.y;

        g_stereoData.right.posZ =
            views[1].pose.position.z;

        g_stereoData.right.rotX =
            views[1].pose.orientation.x;

        g_stereoData.right.rotY =
            views[1].pose.orientation.y;

        g_stereoData.right.rotZ =
            views[1].pose.orientation.z;

        g_stereoData.right.rotW =
            views[1].pose.orientation.w;

        g_stereoData.right.angleLeft =
            views[1].fov.angleLeft;

        g_stereoData.right.angleRight =
            views[1].fov.angleRight;

        g_stereoData.right.angleUp =
            views[1].fov.angleUp;

        g_stereoData.right.angleDown =
            views[1].fov.angleDown;

        g_stereoData.right.valid =
            true;


        g_stereoData.viewCount =
            viewCountOutput;


        g_stereoData.posesValid =
            true;
    }


    return 1;
}


// ============================================================
// Tracking Thread
// ============================================================

static void TrackingThreadMain()
{
    g_trackingThreadRunning.store(
        true
    );


    while (
        g_trackingThreadShouldRun.load()
        )
    {
        int eventResult =
            ProcessOpenXREvents();


        if (
            eventResult <
            0
            )
        {
            g_lastTrackingResult.store(
                eventResult
            );


            Sleep(10);

            continue;
        }


        if (
            g_sessionBegun.load()
            )
        {
            XrTime xrTime =
                0;


            int timeResult =
                GetCurrentXrTime(
                    &xrTime
                );


            if (
                timeResult <
                0
                )
            {
                g_lastTrackingResult.store(
                    timeResult
                );


                Sleep(5);

                continue;
            }


            int predictionMs =
                g_trackingPredictionMilliseconds.load();


            if (predictionMs < 0)
            {
                predictionMs = 0;
            }


            if (predictionMs > 50)
            {
                predictionMs = 50;
            }


            // XrTime/XrDuration arbeiten in Nanosekunden.
            xrTime +=
                static_cast<XrDuration>(predictionMs)
                * 1000000LL;


            int headResult = 0;
            int stereoResult = 0;

            {
                std::lock_guard<std::mutex>
                    sampleLock(g_trackingSampleMutex);

                headResult =
                    UpdateHeadPose(
                        xrTime
                    );

                stereoResult =
                    UpdateStereoViews(
                        xrTime
                    );
            }


            if (
                headResult == 1
                &&
                stereoResult == 1
                )
            {
                g_lastTrackingResult.store(
                    1
                );
            }
            else if (
                headResult <
                0
                )
            {
                g_lastTrackingResult.store(
                    headResult
                );
            }
            else if (
                stereoResult <
                0
                )
            {
                g_lastTrackingResult.store(
                    stereoResult
                );
            }
            else
            {
                g_lastTrackingResult.store(
                    0
                );
            }
        }


        Sleep(5);
    }


    g_trackingThreadRunning.store(
        false
    );
}


// ============================================================
// Stereo Config
// ============================================================

static int QueryStereoConfiguration()
{
    uint32_t viewCount =
        0;


    XrResult result =
        xrEnumerateViewConfigurationViews(
            g_instance,
            g_systemId,
            XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO,
            0,
            &viewCount,
            nullptr
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    if (
        viewCount <
        2
        )
    {
        return -3302;
    }


    std::vector<XrViewConfigurationView>
        configViews(
            viewCount
        );


    for (
        auto& view :
        configViews
        )
    {
        view.type =
            XR_TYPE_VIEW_CONFIGURATION_VIEW;
    }


    result =
        xrEnumerateViewConfigurationViews(
            g_instance,
            g_systemId,
            XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO,
            viewCount,
            &viewCount,
            configViews.data()
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    {
        std::lock_guard<std::mutex>
            lock(g_stereoMutex);


        g_stereoData.viewCount =
            viewCount;


        g_stereoData.recommendedWidth =
            configViews[0]
            .recommendedImageRectWidth;


        g_stereoData.recommendedHeight =
            configViews[0]
            .recommendedImageRectHeight;


        g_stereoData.recommendedSampleCount =
            configViews[0]
            .recommendedSwapchainSampleCount;


        g_stereoData.configurationValid =
            true;
    }


    return 1;
}


// ============================================================
// Swapchain Format
// ============================================================

static int ChooseSwapchainFormat(
    int64_t* selectedFormat)
{
    uint32_t formatCount =
        0;


    XrResult result =
        xrEnumerateSwapchainFormats(
            g_session,
            0,
            &formatCount,
            nullptr
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    if (
        formatCount ==
        0
        )
    {
        return -4002;
    }


    std::vector<int64_t>
        formats(
            formatCount
        );


    result =
        xrEnumerateSwapchainFormats(
            g_session,
            formatCount,
            &formatCount,
            formats.data()
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    const int64_t preferredFormats[] =
    {
        DXGI_FORMAT_R8G8B8A8_UNORM_SRGB,
        DXGI_FORMAT_R8G8B8A8_UNORM,
        DXGI_FORMAT_B8G8R8A8_UNORM_SRGB,
        DXGI_FORMAT_B8G8R8A8_UNORM
    };


    for (
        int64_t preferred :
    preferredFormats
        )
    {
        for (
            int64_t available :
        formats
            )
        {
            if (
                preferred ==
                available
                )
            {
                *selectedFormat =
                    preferred;


                return 1;
            }
        }
    }


    *selectedFormat =
        formats[0];


    return 2;
}


// ============================================================
// Eine Swapchain erstellen
// ============================================================

static int CreateOneSwapchain(
    uint32_t width,
    uint32_t height,
    int64_t format,
    XrSwapchain* swapchain,
    std::vector<XrSwapchainImageD3D11KHR>& images)
{
    XrSwapchainCreateInfo
        createInfo{};


    createInfo.type =
        XR_TYPE_SWAPCHAIN_CREATE_INFO;


    createInfo.usageFlags =
        XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT
        |
        XR_SWAPCHAIN_USAGE_TRANSFER_DST_BIT;


    createInfo.format =
        format;


    createInfo.sampleCount =
        1;


    createInfo.width =
        width;


    createInfo.height =
        height;


    createInfo.faceCount =
        1;


    createInfo.arraySize =
        1;


    createInfo.mipCount =
        1;


    XrResult result =
        xrCreateSwapchain(
            g_session,
            &createInfo,
            swapchain
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    uint32_t imageCount =
        0;


    result =
        xrEnumerateSwapchainImages(
            *swapchain,
            0,
            &imageCount,
            nullptr
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    images.resize(
        imageCount
    );


    for (
        auto& image :
        images
        )
    {
        image.type =
            XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR;


        image.next =
            nullptr;


        image.texture =
            nullptr;
    }


    result =
        xrEnumerateSwapchainImages(
            *swapchain,
            imageCount,
            &imageCount,
            reinterpret_cast<
            XrSwapchainImageBaseHeader*
            >(
                images.data()
                )
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    return 1;
}


// ============================================================
// Acquire einer Swapchain
// ============================================================

static int AcquireAndWaitSwapchain(
    XrSwapchain swapchain,
    uint32_t* imageIndex)
{
    XrSwapchainImageAcquireInfo
        acquireInfo{};


    acquireInfo.type =
        XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO;


    XrResult result =
        xrAcquireSwapchainImage(
            swapchain,
            &acquireInfo,
            imageIndex
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    XrSwapchainImageWaitInfo
        waitInfo{};


    waitInfo.type =
        XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO;


    waitInfo.timeout =
        1000000000LL;


    result =
        xrWaitSwapchainImage(
            swapchain,
            &waitInfo
        );


    if (XR_FAILED(result))
    {
        XrSwapchainImageReleaseInfo
            releaseInfo{};


        releaseInfo.type =
            XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO;


        xrReleaseSwapchainImage(
            swapchain,
            &releaseInfo
        );


        return
            static_cast<int>(
                result
                );
    }


    return 1;
}


// ============================================================
// Release
// ============================================================

static int ReleaseSwapchain(
    XrSwapchain swapchain)
{
    XrSwapchainImageReleaseInfo
        releaseInfo{};


    releaseInfo.type =
        XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO;


    XrResult result =
        xrReleaseSwapchainImage(
            swapchain,
            &releaseInfo
        );


    if (XR_FAILED(result))
    {
        return
            static_cast<int>(
                result
                );
    }


    return 1;
}


// ============================================================
// D3D11 Copy auf bereits erworbene Images
// ============================================================

static int CopyUnityTexturesToIndices(
    uint32_t leftIndex,
    uint32_t rightIndex)
{
    if (
        !g_unityTexturesRegistered.load()
        )
    {
        return -6001;
    }


    if (
        leftIndex >=
        g_leftSwapchainImages.size()
        ||
        rightIndex >=
        g_rightSwapchainImages.size()
        )
    {
        return -6008;
    }


    ID3D11Texture2D* leftSource =
        nullptr;


    ID3D11Texture2D* rightSource =
        nullptr;


    HRESULT hr =
        g_leftUnityTexture
        ->QueryInterface(
            __uuidof(ID3D11Texture2D),
            reinterpret_cast<void**>(
                &leftSource
                )
        );


    if (
        FAILED(hr)
        ||
        leftSource ==
        nullptr
        )
    {
        return -6004;
    }


    hr =
        g_rightUnityTexture
        ->QueryInterface(
            __uuidof(ID3D11Texture2D),
            reinterpret_cast<void**>(
                &rightSource
                )
        );


    if (
        FAILED(hr)
        ||
        rightSource ==
        nullptr
        )
    {
        leftSource->Release();


        return -6005;
    }


    ID3D11Texture2D* leftDestination =
        g_leftSwapchainImages[
            leftIndex
        ].texture;


    ID3D11Texture2D* rightDestination =
        g_rightSwapchainImages[
            rightIndex
        ].texture;


    if (
        leftDestination ==
        nullptr
        ||
        rightDestination ==
        nullptr
        )
    {
        leftSource->Release();

        rightSource->Release();


        return -6009;
    }


    D3D11_TEXTURE2D_DESC
        leftSourceDesc{};


    D3D11_TEXTURE2D_DESC
        rightSourceDesc{};


    D3D11_TEXTURE2D_DESC
        leftDestinationDesc{};


    D3D11_TEXTURE2D_DESC
        rightDestinationDesc{};


    leftSource->GetDesc(
        &leftSourceDesc
    );


    rightSource->GetDesc(
        &rightSourceDesc
    );


    leftDestination->GetDesc(
        &leftDestinationDesc
    );


    rightDestination->GetDesc(
        &rightDestinationDesc
    );


    g_lastLeftSourceFormat.store(
        static_cast<int>(
            leftSourceDesc.Format
            )
    );


    g_lastRightSourceFormat.store(
        static_cast<int>(
            rightSourceDesc.Format
            )
    );


    if (
        leftSourceDesc.Width !=
        leftDestinationDesc.Width
        ||
        leftSourceDesc.Height !=
        leftDestinationDesc.Height
        ||
        rightSourceDesc.Width !=
        rightDestinationDesc.Width
        ||
        rightSourceDesc.Height !=
        rightDestinationDesc.Height
        )
    {
        leftSource->Release();

        rightSource->Release();


        return -6010;
    }


    if (
        !FormatsCompatibleForCopy(
            leftSourceDesc.Format,
            leftDestinationDesc.Format
        )
        ||
        !FormatsCompatibleForCopy(
            rightSourceDesc.Format,
            rightDestinationDesc.Format
        )
        )
    {
        leftSource->Release();

        rightSource->Release();


        return -6011;
    }


    ID3D11DeviceContext* context =
        nullptr;


    g_d3d11Device
        ->GetImmediateContext(
            &context
        );


    if (
        context ==
        nullptr
        )
    {
        leftSource->Release();

        rightSource->Release();


        return -6012;
    }


    context->CopyResource(
        leftDestination,
        leftSource
    );


    context->CopyResource(
        rightDestination,
        rightSource
    );


    context->Flush();


    context->Release();


    leftSource->Release();

    rightSource->Release();


    return 1;
}


// ============================================================
// Alter Copy Test
// ============================================================

static int CopyUnityTexturesToOpenXR()
{
    std::lock_guard<std::mutex>
        swapchainLock(
            g_swapchainMutex
        );


    std::lock_guard<std::mutex>
        textureLock(
            g_unityTextureMutex
        );


    uint32_t leftIndex =
        0;


    uint32_t rightIndex =
        0;


    int result =
        AcquireAndWaitSwapchain(
            g_leftSwapchain,
            &leftIndex
        );


    if (
        result !=
        1
        )
    {
        return result;
    }


    result =
        AcquireAndWaitSwapchain(
            g_rightSwapchain,
            &rightIndex
        );


    if (
        result !=
        1
        )
    {
        ReleaseSwapchain(
            g_leftSwapchain
        );


        return result;
    }


    result =
        CopyUnityTexturesToIndices(
            leftIndex,
            rightIndex
        );


    int leftRelease =
        ReleaseSwapchain(
            g_leftSwapchain
        );


    int rightRelease =
        ReleaseSwapchain(
            g_rightSwapchain
        );


    if (
        result !=
        1
        )
    {
        return result;
    }


    if (
        leftRelease !=
        1
        )
    {
        return leftRelease;
    }


    if (
        rightRelease !=
        1
        )
    {
        return rightRelease;
    }


    g_lastTextureCopyLeftIndex.store(
        static_cast<int>(
            leftIndex
            )
    );


    g_lastTextureCopyRightIndex.store(
        static_cast<int>(
            rightIndex
            )
    );


    g_successfulTextureCopies.fetch_add(
        1
    );


    return 1;
}


// ============================================================
// 0.0.31
// EIN kompletter OpenXR Projection Frame
// ============================================================

static int SubmitSingleProjectionFrame()
{
    g_frameSubmitStep.store(
        1
    );


    g_lastFrameSubmitResult.store(
        0
    );


    g_lastWaitFrameResult.store(
        0
    );


    g_lastBeginFrameResult.store(
        0
    );


    g_lastEndFrameResult.store(
        0
    );


    g_lastShouldRender.store(
        0
    );


    // --------------------------------------------------------
    // 0.0.37:
    // Der Snapshot wurde auf dem Unity-Main-Thread aufgenommen,
    // BEVOR die beiden Eye-Cameras gerendert wurden.
    //
    // Wir kopieren ihn hier lokal und verwenden spaeter genau
    // diese Eye-Posen/FOVs fuer den Projection Layer.
    // --------------------------------------------------------

    RenderPoseSnapshot renderPose{};

    {
        std::lock_guard<std::mutex>
            renderPoseLock(g_renderPoseMutex);

        if (
            !g_renderPoseInFlight.load()
            ||
            !g_renderPoseSnapshot.valid
            )
        {
            g_lastFrameSubmitResult.store(
                -7004
            );

            return -7004;
        }

        renderPose =
            g_renderPoseSnapshot;
    }


    auto releaseRenderPose =
        []()
        {
            {
                std::lock_guard<std::mutex>
                    renderPoseLock(g_renderPoseMutex);

                g_renderPoseSnapshot.valid =
                    false;
            }

            g_renderPoseInFlight.store(
                false
            );
        };


    if (
        !g_sessionBegun.load()
        )
    {
        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            -7000
        );

        return -7000;
    }


    if (
        !g_swapchainsCreated.load()
        ||
        !g_unityTexturesRegistered.load()
        )
    {
        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            -7001
        );

        return -7001;
    }


    // ========================================================
    // xrWaitFrame
    // ========================================================

    g_frameSubmitStep.store(
        2
    );


    XrFrameWaitInfo waitInfo{};

    waitInfo.type =
        XR_TYPE_FRAME_WAIT_INFO;


    XrFrameState frameState{};

    frameState.type =
        XR_TYPE_FRAME_STATE;


    XrResult xrResult =
        xrWaitFrame(
            g_session,
            &waitInfo,
            &frameState
        );


    g_lastWaitFrameResult.store(
        static_cast<int>(
            xrResult
            )
    );


    if (XR_FAILED(xrResult))
    {
        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            static_cast<int>(
                xrResult
                )
        );

        return
            static_cast<int>(
                xrResult
                );
    }


    g_frameSubmitStep.store(
        3
    );


    g_lastShouldRender.store(
        frameState.shouldRender
        ?
        1
        :
        0
    );


    // ========================================================
    // xrBeginFrame
    // ========================================================

    g_frameSubmitStep.store(
        4
    );


    XrFrameBeginInfo beginInfo{};

    beginInfo.type =
        XR_TYPE_FRAME_BEGIN_INFO;


    xrResult =
        xrBeginFrame(
            g_session,
            &beginInfo
        );


    g_lastBeginFrameResult.store(
        static_cast<int>(
            xrResult
            )
    );


    if (XR_FAILED(xrResult))
    {
        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            static_cast<int>(
                xrResult
                )
        );

        return
            static_cast<int>(
                xrResult
                );
    }


    g_frameSubmitStep.store(
        5
    );


    // ========================================================
    // 0.0.37:
    // KEIN zweites xrLocateViews fuer die Projection-Pose.
    //
    // Die Runtime darf laut OpenXR eine Projection-Pose erhalten,
    // die von xrLocateViews abweicht. Entscheidend fuer unseren Test:
    // Das Projection-Layer beschreibt exakt die Pose, mit der die
    // Pixel in Unity gerendert wurden.
    // ========================================================

    XrView views[2]{};

    views[0].type =
        XR_TYPE_VIEW;

    views[0].pose.position.x =
        renderPose.left.posX;

    views[0].pose.position.y =
        renderPose.left.posY;

    views[0].pose.position.z =
        renderPose.left.posZ;

    views[0].pose.orientation.x =
        renderPose.left.rotX;

    views[0].pose.orientation.y =
        renderPose.left.rotY;

    views[0].pose.orientation.z =
        renderPose.left.rotZ;

    views[0].pose.orientation.w =
        renderPose.left.rotW;

    views[0].fov.angleLeft =
        renderPose.left.angleLeft;

    views[0].fov.angleRight =
        renderPose.left.angleRight;

    views[0].fov.angleUp =
        renderPose.left.angleUp;

    views[0].fov.angleDown =
        renderPose.left.angleDown;


    views[1].type =
        XR_TYPE_VIEW;

    views[1].pose.position.x =
        renderPose.right.posX;

    views[1].pose.position.y =
        renderPose.right.posY;

    views[1].pose.position.z =
        renderPose.right.posZ;

    views[1].pose.orientation.x =
        renderPose.right.rotX;

    views[1].pose.orientation.y =
        renderPose.right.rotY;

    views[1].pose.orientation.z =
        renderPose.right.rotZ;

    views[1].pose.orientation.w =
        renderPose.right.rotW;

    views[1].fov.angleLeft =
        renderPose.right.angleLeft;

    views[1].fov.angleRight =
        renderPose.right.angleRight;

    views[1].fov.angleUp =
        renderPose.right.angleUp;

    views[1].fov.angleDown =
        renderPose.right.angleDown;


    g_frameSubmitStep.store(
        6
    );


    // ========================================================
    // Wenn Runtime ausdruecklich nicht rendern moechte
    // ========================================================

    if (!frameState.shouldRender)
    {
        g_frameSubmitStep.store(
            11
        );


        XrFrameEndInfo endInfo{};

        endInfo.type =
            XR_TYPE_FRAME_END_INFO;

        endInfo.displayTime =
            frameState.predictedDisplayTime;

        endInfo.environmentBlendMode =
            g_environmentBlendMode;

        endInfo.layerCount =
            0;

        endInfo.layers =
            nullptr;


        xrResult =
            xrEndFrame(
                g_session,
                &endInfo
            );


        g_lastEndFrameResult.store(
            static_cast<int>(
                xrResult
                )
        );


        g_frameSubmitStep.store(
            12
        );


        releaseRenderPose();


        int result =
            XR_FAILED(xrResult)
            ?
            static_cast<int>(
                xrResult
                )
            :
            -7003;


        g_lastFrameSubmitResult.store(
            result
        );


        return result;
    }


    // ========================================================
    // Swapchain / Texture Locks
    // ========================================================

    std::lock_guard<std::mutex>
        swapchainLock(
            g_swapchainMutex
        );


    std::lock_guard<std::mutex>
        textureLock(
            g_unityTextureMutex
        );


    // ========================================================
    // LEFT
    // ========================================================

    uint32_t leftIndex =
        0;


    int result =
        AcquireAndWaitSwapchain(
            g_leftSwapchain,
            &leftIndex
        );


    if (
        result !=
        1
        )
    {
        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            result
        );

        return result;
    }


    g_frameSubmitStep.store(
        7
    );


    // ========================================================
    // RIGHT
    // ========================================================

    uint32_t rightIndex =
        0;


    result =
        AcquireAndWaitSwapchain(
            g_rightSwapchain,
            &rightIndex
        );


    if (
        result !=
        1
        )
    {
        ReleaseSwapchain(
            g_leftSwapchain
        );

        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            result
        );

        return result;
    }


    g_frameSubmitStep.store(
        8
    );


    // ========================================================
    // Copy
    // ========================================================

    result =
        CopyUnityTexturesToIndices(
            leftIndex,
            rightIndex
        );


    if (
        result !=
        1
        )
    {
        ReleaseSwapchain(
            g_leftSwapchain
        );

        ReleaseSwapchain(
            g_rightSwapchain
        );

        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            result
        );

        return result;
    }


    g_frameSubmitStep.store(
        9
    );


    // ========================================================
    // Release
    // ========================================================

    int leftReleaseResult =
        ReleaseSwapchain(
            g_leftSwapchain
        );


    int rightReleaseResult =
        ReleaseSwapchain(
            g_rightSwapchain
        );


    if (
        leftReleaseResult !=
        1
        )
    {
        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            leftReleaseResult
        );

        return leftReleaseResult;
    }


    if (
        rightReleaseResult !=
        1
        )
    {
        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            rightReleaseResult
        );

        return rightReleaseResult;
    }


    g_lastSubmitLeftIndex.store(
        static_cast<int>(
            leftIndex
            )
    );


    g_lastSubmitRightIndex.store(
        static_cast<int>(
            rightIndex
            )
    );


    g_frameSubmitStep.store(
        10
    );


    // ========================================================
    // Projection Views
    // ========================================================

    XrCompositionLayerProjectionView
        projectionViews[2]{};


    projectionViews[0].type =
        XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW;

    projectionViews[0].pose =
        views[0].pose;

    projectionViews[0].fov =
        views[0].fov;

    projectionViews[0]
        .subImage
        .swapchain =
        g_leftSwapchain;

    projectionViews[0]
        .subImage
        .imageRect
        .offset =
    {
        0,
        0
    };

    projectionViews[0]
        .subImage
        .imageRect
        .extent =
    {
        static_cast<int32_t>(
            g_swapchainWidth
        ),

        static_cast<int32_t>(
            g_swapchainHeight
        )
    };

    projectionViews[0]
        .subImage
        .imageArrayIndex =
        0;


    projectionViews[1].type =
        XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW;

    projectionViews[1].pose =
        views[1].pose;

    projectionViews[1].fov =
        views[1].fov;

    projectionViews[1]
        .subImage
        .swapchain =
        g_rightSwapchain;

    projectionViews[1]
        .subImage
        .imageRect
        .offset =
    {
        0,
        0
    };

    projectionViews[1]
        .subImage
        .imageRect
        .extent =
    {
        static_cast<int32_t>(
            g_swapchainWidth
        ),

        static_cast<int32_t>(
            g_swapchainHeight
        )
    };

    projectionViews[1]
        .subImage
        .imageArrayIndex =
        0;


    XrCompositionLayerProjection
        projectionLayer{};


    projectionLayer.type =
        XR_TYPE_COMPOSITION_LAYER_PROJECTION;

    projectionLayer.space =
        g_localSpace;

    projectionLayer.viewCount =
        2;

    projectionLayer.views =
        projectionViews;


    const XrCompositionLayerBaseHeader*
        layers[] =
    {
        reinterpret_cast<
            const XrCompositionLayerBaseHeader*
        >(
            &projectionLayer
        )
    };


    XrFrameEndInfo endInfo{};

    endInfo.type =
        XR_TYPE_FRAME_END_INFO;

    endInfo.displayTime =
        frameState.predictedDisplayTime;

    endInfo.environmentBlendMode =
        g_environmentBlendMode;

    endInfo.layerCount =
        1;

    endInfo.layers =
        layers;


    g_frameSubmitStep.store(
        11
    );


    xrResult =
        xrEndFrame(
            g_session,
            &endInfo
        );


    g_lastEndFrameResult.store(
        static_cast<int>(
            xrResult
            )
    );


    g_frameSubmitStep.store(
        12
    );


    if (XR_FAILED(xrResult))
    {
        releaseRenderPose();

        g_lastFrameSubmitResult.store(
            static_cast<int>(
                xrResult
                )
        );

        return
            static_cast<int>(
                xrResult
                );
    }


    g_lastSubmittedRenderPoseGeneration.store(
        renderPose.generation
    );


    g_successfulSubmittedFrames.fetch_add(
        1
    );


    g_lastFrameSubmitResult.store(
        1
    );


    releaseRenderPose();


    return 1;
}


// ============================================================
// Render Callback
// ============================================================

static void UNITY_INTERFACE_API
OnRenderEvent(
    int eventId)
{
    if (
        eventId ==
        TCGVR_EVENT_COPY_STEREO_TEXTURES
        )
    {
        int result =
            CopyUnityTexturesToOpenXR();


        g_lastTextureCopyResult.store(
            result
        );


        return;
    }


    if (
        eventId ==
        TCGVR_EVENT_SUBMIT_SINGLE_FRAME
        )
    {
        SubmitSingleProjectionFrame();


        return;
    }
}


// ============================================================
// Exports
// ============================================================

extern "C"
{


    // ============================================================
    // Init
    // ============================================================

    __declspec(dllexport)
        int TCGVR_InitializeOpenXR()
    {
        if (
            g_instance !=
            XR_NULL_HANDLE
            )
        {
            return 1;
        }


        uint32_t extensionCount =
            0;


        XrResult result =
            xrEnumerateInstanceExtensionProperties(
                nullptr,
                0,
                &extensionCount,
                nullptr
            );


        if (XR_FAILED(result))
        {
            return
                static_cast<int>(
                    result
                    );
        }


        std::vector<XrExtensionProperties>
            extensions(
                extensionCount
            );


        for (
            auto& extension :
            extensions
            )
        {
            extension.type =
                XR_TYPE_EXTENSION_PROPERTIES;
        }


        result =
            xrEnumerateInstanceExtensionProperties(
                nullptr,
                extensionCount,
                &extensionCount,
                extensions.data()
            );


        if (XR_FAILED(result))
        {
            return
                static_cast<int>(
                    result
                    );
        }


        bool d3d11Available =
            false;


        bool timeAvailable =
            false;


        for (
            const auto& extension :
            extensions
            )
        {
            if (
                strcmp(
                    extension.extensionName,
                    XR_KHR_D3D11_ENABLE_EXTENSION_NAME
                )
                ==
                0
                )
            {
                d3d11Available =
                    true;
            }


            if (
                strcmp(
                    extension.extensionName,
                    XR_KHR_WIN32_CONVERT_PERFORMANCE_COUNTER_TIME_EXTENSION_NAME
                )
                ==
                0
                )
            {
                timeAvailable =
                    true;
            }
        }


        if (!d3d11Available)
        {
            return -2001;
        }


        if (!timeAvailable)
        {
            return -2002;
        }


        const char*
            enabledExtensions[] =
        {
            XR_KHR_D3D11_ENABLE_EXTENSION_NAME,
            XR_KHR_WIN32_CONVERT_PERFORMANCE_COUNTER_TIME_EXTENSION_NAME
        };


        XrInstanceCreateInfo
            createInfo{};


        createInfo.type =
            XR_TYPE_INSTANCE_CREATE_INFO;


        strcpy_s(
            createInfo
            .applicationInfo
            .applicationName,
            "The Coin Game VR"
        );


        createInfo
            .applicationInfo
            .applicationVersion =
            1;


        strcpy_s(
            createInfo
            .applicationInfo
            .engineName,
            "TCGVR"
        );


        createInfo
            .applicationInfo
            .engineVersion =
            1;


        createInfo
            .applicationInfo
            .apiVersion =
            XR_MAKE_VERSION(
                1,
                0,
                0
            );


        createInfo.enabledExtensionCount =
            2;


        createInfo.enabledExtensionNames =
            enabledExtensions;


        result =
            xrCreateInstance(
                &createInfo,
                &g_instance
            );


        if (XR_FAILED(result))
        {
            g_instance =
                XR_NULL_HANDLE;


            return
                static_cast<int>(
                    result
                    );
        }


        result =
            xrGetInstanceProcAddr(
                g_instance,
                "xrConvertWin32PerformanceCounterToTimeKHR",
                reinterpret_cast<
                PFN_xrVoidFunction*
                >(
                    &g_convertQpcToXrTime
                    )
            );


        if (
            XR_FAILED(result)
            ||
            g_convertQpcToXrTime ==
            nullptr
            )
        {
            return -2003;
        }


        return 1;
    }


    // ============================================================
    // HMD
    // ============================================================

    __declspec(dllexport)
        int TCGVR_FindHMD()
    {
        XrSystemGetInfo
            systemInfo{};


        systemInfo.type =
            XR_TYPE_SYSTEM_GET_INFO;


        systemInfo.formFactor =
            XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;


        XrResult result =
            xrGetSystem(
                g_instance,
                &systemInfo,
                &g_systemId
            );


        if (XR_FAILED(result))
        {
            return
                static_cast<int>(
                    result
                    );
        }


        int blendResult =
            ChooseEnvironmentBlendMode();


        if (
            blendResult !=
            1
            )
        {
            return blendResult;
        }


        return 1;
    }


    // ============================================================
    // Session
    // ============================================================

    __declspec(dllexport)
        int TCGVR_CreateSession()
    {
        if (
            g_d3d11Device ==
            nullptr
            )
        {
            return -2102;
        }


        if (
            g_session !=
            XR_NULL_HANDLE
            )
        {
            return 1;
        }


        PFN_xrGetD3D11GraphicsRequirementsKHR
            getRequirements =
            nullptr;


        XrResult result =
            xrGetInstanceProcAddr(
                g_instance,
                "xrGetD3D11GraphicsRequirementsKHR",
                reinterpret_cast<
                PFN_xrVoidFunction*
                >(
                    &getRequirements
                    )
            );


        if (
            XR_FAILED(result)
            ||
            getRequirements ==
            nullptr
            )
        {
            return -2103;
        }


        result =
            getRequirements(
                g_instance,
                g_systemId,
                &g_graphicsRequirements
            );


        if (XR_FAILED(result))
        {
            return
                static_cast<int>(
                    result
                    );
        }


        IDXGIDevice* dxgiDevice =
            nullptr;


        HRESULT hr =
            g_d3d11Device
            ->QueryInterface(
                __uuidof(IDXGIDevice),
                reinterpret_cast<void**>(
                    &dxgiDevice
                    )
            );


        if (
            FAILED(hr)
            ||
            dxgiDevice ==
            nullptr
            )
        {
            return -2104;
        }


        IDXGIAdapter* adapter =
            nullptr;


        hr =
            dxgiDevice
            ->GetAdapter(
                &adapter
            );


        dxgiDevice->Release();


        if (
            FAILED(hr)
            ||
            adapter ==
            nullptr
            )
        {
            return -2105;
        }


        DXGI_ADAPTER_DESC
            adapterDesc{};


        hr =
            adapter
            ->GetDesc(
                &adapterDesc
            );


        adapter->Release();


        if (FAILED(hr))
        {
            return -2106;
        }


        if (
            !LuidEquals(
                adapterDesc.AdapterLuid,
                g_graphicsRequirements
                .adapterLuid
            )
            )
        {
            return -2107;
        }


        if (
            g_d3d11Device
            ->GetFeatureLevel()
            <
            g_graphicsRequirements
            .minFeatureLevel
            )
        {
            return -2108;
        }


        int stereoResult =
            QueryStereoConfiguration();


        if (
            stereoResult !=
            1
            )
        {
            return stereoResult;
        }


        XrGraphicsBindingD3D11KHR
            graphicsBinding{};


        graphicsBinding.type =
            XR_TYPE_GRAPHICS_BINDING_D3D11_KHR;


        graphicsBinding.device =
            g_d3d11Device;


        XrSessionCreateInfo
            sessionInfo{};


        sessionInfo.type =
            XR_TYPE_SESSION_CREATE_INFO;


        sessionInfo.next =
            &graphicsBinding;


        sessionInfo.systemId =
            g_systemId;


        result =
            xrCreateSession(
                g_instance,
                &sessionInfo,
                &g_session
            );


        if (XR_FAILED(result))
        {
            return
                static_cast<int>(
                    result
                    );
        }


        XrReferenceSpaceCreateInfo
            localInfo{};


        localInfo.type =
            XR_TYPE_REFERENCE_SPACE_CREATE_INFO;


        localInfo.referenceSpaceType =
            XR_REFERENCE_SPACE_TYPE_LOCAL;


        localInfo
            .poseInReferenceSpace
            .orientation
            .w =
            1.0f;


        result =
            xrCreateReferenceSpace(
                g_session,
                &localInfo,
                &g_localSpace
            );


        if (XR_FAILED(result))
        {
            return
                static_cast<int>(
                    result
                    );
        }


        XrReferenceSpaceCreateInfo
            viewInfo{};


        viewInfo.type =
            XR_TYPE_REFERENCE_SPACE_CREATE_INFO;


        viewInfo.referenceSpaceType =
            XR_REFERENCE_SPACE_TYPE_VIEW;


        viewInfo
            .poseInReferenceSpace
            .orientation
            .w =
            1.0f;


        result =
            xrCreateReferenceSpace(
                g_session,
                &viewInfo,
                &g_viewSpace
            );


        if (XR_FAILED(result))
        {
            return
                static_cast<int>(
                    result
                    );
        }


        return 1;
    }


    // ============================================================
    // Swapchains
    // ============================================================

    __declspec(dllexport)
        int TCGVR_CreateStereoSwapchains(
            int width,
            int height)
    {
        std::lock_guard<std::mutex>
            lock(g_swapchainMutex);


        if (
            g_swapchainsCreated.load()
            )
        {
            return 1;
        }


        int64_t selectedFormat =
            0;


        int formatResult =
            ChooseSwapchainFormat(
                &selectedFormat
            );


        if (
            formatResult <
            1
            )
        {
            return formatResult;
        }


        int leftResult =
            CreateOneSwapchain(
                static_cast<uint32_t>(
                    width
                    ),
                static_cast<uint32_t>(
                    height
                    ),
                selectedFormat,
                &g_leftSwapchain,
                g_leftSwapchainImages
            );


        if (
            leftResult !=
            1
            )
        {
            return leftResult;
        }


        int rightResult =
            CreateOneSwapchain(
                static_cast<uint32_t>(
                    width
                    ),
                static_cast<uint32_t>(
                    height
                    ),
                selectedFormat,
                &g_rightSwapchain,
                g_rightSwapchainImages
            );


        if (
            rightResult !=
            1
            )
        {
            return rightResult;
        }


        g_swapchainFormat =
            selectedFormat;


        g_swapchainWidth =
            static_cast<uint32_t>(
                width
                );


        g_swapchainHeight =
            static_cast<uint32_t>(
                height
                );


        g_swapchainsCreated.store(
            true
        );


        return 1;
    }


    // ============================================================
    // Swapchain Info
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetSwapchainInfo(
            int* width,
            int* height,
            int* format,
            int* leftImageCount,
            int* rightImageCount)
    {
        if (
            !g_swapchainsCreated.load()
            )
        {
            return 0;
        }


        *width =
            static_cast<int>(
                g_swapchainWidth
                );


        *height =
            static_cast<int>(
                g_swapchainHeight
                );


        *format =
            static_cast<int>(
                g_swapchainFormat
                );


        *leftImageCount =
            static_cast<int>(
                g_leftSwapchainImages.size()
                );


        *rightImageCount =
            static_cast<int>(
                g_rightSwapchainImages.size()
                );


        return 1;
    }


    // ============================================================
    // Alter Acquire Test
    // ============================================================

    __declspec(dllexport)
        int TCGVR_TestSwapchainAcquireRelease()
    {
        std::lock_guard<std::mutex>
            lock(g_swapchainMutex);


        uint32_t leftIndex =
            0;


        uint32_t rightIndex =
            0;


        int result =
            AcquireAndWaitSwapchain(
                g_leftSwapchain,
                &leftIndex
            );


        if (
            result !=
            1
            )
        {
            g_lastAcquireTestResult.store(
                result
            );


            return result;
        }


        result =
            ReleaseSwapchain(
                g_leftSwapchain
            );


        if (
            result !=
            1
            )
        {
            g_lastAcquireTestResult.store(
                result
            );


            return result;
        }


        result =
            AcquireAndWaitSwapchain(
                g_rightSwapchain,
                &rightIndex
            );


        if (
            result !=
            1
            )
        {
            g_lastAcquireTestResult.store(
                result
            );


            return result;
        }


        result =
            ReleaseSwapchain(
                g_rightSwapchain
            );


        if (
            result !=
            1
            )
        {
            g_lastAcquireTestResult.store(
                result
            );


            return result;
        }


        g_lastLeftImageIndex.store(
            static_cast<int>(
                leftIndex
                )
        );


        g_lastRightImageIndex.store(
            static_cast<int>(
                rightIndex
                )
        );


        g_successfulAcquireTests.fetch_add(
            1
        );


        g_lastAcquireTestResult.store(
            1
        );


        return 1;
    }


    // ============================================================
    // Acquire Info
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetAcquireTestInfo(
            int* lastResult,
            int* leftImageIndex,
            int* rightImageIndex,
            unsigned long long* successfulTests)
    {
        *lastResult =
            g_lastAcquireTestResult.load();


        *leftImageIndex =
            g_lastLeftImageIndex.load();


        *rightImageIndex =
            g_lastRightImageIndex.load();


        *successfulTests =
            g_successfulAcquireTests.load();


        return 1;
    }


    // ============================================================
    // Unity Texturen registrieren
    // ============================================================

    __declspec(dllexport)
        int TCGVR_SetUnityStereoTextures(
            void* leftTexture,
            void* rightTexture,
            int width,
            int height)
    {
        if (
            leftTexture ==
            nullptr
            ||
            rightTexture ==
            nullptr
            )
        {
            return -6100;
        }


        std::lock_guard<std::mutex>
            lock(g_unityTextureMutex);


        g_leftUnityTexture =
            reinterpret_cast<ID3D11Resource*>(
                leftTexture
                );


        g_rightUnityTexture =
            reinterpret_cast<ID3D11Resource*>(
                rightTexture
                );


        g_unityTextureWidth =
            static_cast<uint32_t>(
                width
                );


        g_unityTextureHeight =
            static_cast<uint32_t>(
                height
                );


        g_unityTexturesRegistered.store(
            true
        );


        return 1;
    }


    // ============================================================
    // Texturen clear
    // ============================================================

    __declspec(dllexport)
        void TCGVR_ClearUnityStereoTextures()
    {
        ClearUnityTexturePointers();
    }


    // ============================================================
    // Render Event Function
    // ============================================================

    __declspec(dllexport)
        UnityRenderingEvent
        TCGVR_GetRenderEventFunc()
    {
        return
            OnRenderEvent;
    }


    // ============================================================
    // Copy Info
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetTextureCopyInfo(
            int* lastResult,
            int* leftImageIndex,
            int* rightImageIndex,
            unsigned long long* successfulCopies,
            int* leftSourceFormat,
            int* rightSourceFormat)
    {
        *lastResult =
            g_lastTextureCopyResult.load();


        *leftImageIndex =
            g_lastTextureCopyLeftIndex.load();


        *rightImageIndex =
            g_lastTextureCopyRightIndex.load();


        *successfulCopies =
            g_successfulTextureCopies.load();


        *leftSourceFormat =
            g_lastLeftSourceFormat.load();


        *rightSourceFormat =
            g_lastRightSourceFormat.load();


        return 1;
    }


    // ============================================================
    // 0.0.31 Frame Submit Info
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetFrameSubmitInfo(
            int* step,
            int* lastResult,
            int* waitResult,
            int* beginResult,
            int* endResult,
            int* shouldRender,
            int* leftImageIndex,
            int* rightImageIndex,
            unsigned long long* successfulFrames)
    {
        if (
            step ==
            nullptr
            ||
            lastResult ==
            nullptr
            ||
            waitResult ==
            nullptr
            ||
            beginResult ==
            nullptr
            ||
            endResult ==
            nullptr
            ||
            shouldRender ==
            nullptr
            ||
            leftImageIndex ==
            nullptr
            ||
            rightImageIndex ==
            nullptr
            ||
            successfulFrames ==
            nullptr
            )
        {
            return -7100;
        }


        *step =
            g_frameSubmitStep.load();


        *lastResult =
            g_lastFrameSubmitResult.load();


        *waitResult =
            g_lastWaitFrameResult.load();


        *beginResult =
            g_lastBeginFrameResult.load();


        *endResult =
            g_lastEndFrameResult.load();


        *shouldRender =
            g_lastShouldRender.load();


        *leftImageIndex =
            g_lastSubmitLeftIndex.load();


        *rightImageIndex =
            g_lastSubmitRightIndex.load();


        *successfulFrames =
            g_successfulSubmittedFrames.load();


        return 1;
    }


    // ============================================================
    // Tracking Thread
    // ============================================================

    __declspec(dllexport)
        int TCGVR_StartTrackingThread()
    {
        if (
            g_trackingThreadRunning.load()
            ||
            g_trackingThreadShouldRun.load()
            )
        {
            return 1;
        }


        g_trackingThreadShouldRun.store(
            true
        );


        try
        {
            g_trackingThread =
                std::thread(
                    TrackingThreadMain
                );
        }
        catch (...)
        {
            g_trackingThreadShouldRun.store(
                false
            );


            return -2501;
        }


        return 1;
    }


    // ============================================================
    // Status
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetSessionState()
    {
        return
            g_sessionState.load();
    }


    __declspec(dllexport)
        int TCGVR_GetLastTrackingResult()
    {
        return
            g_lastTrackingResult.load();
    }


    // ============================================================
    // 0.0.37 Render-Pose Snapshot
    // ============================================================

    __declspec(dllexport)
        int TCGVR_CaptureRenderPose()
    {
        bool expected =
            false;


        if (
            !g_renderPoseInFlight
            .compare_exchange_strong(
                expected,
                true
            )
            )
        {
            // Voriger Unity-Frame wurde auf dem Render-Thread
            // noch nicht submitted.
            return 0;
        }


        HeadPoseData head{};
        StereoData stereo{};


        {
            std::lock_guard<std::mutex>
                sampleLock(g_trackingSampleMutex);

            {
                std::lock_guard<std::mutex>
                    headLock(g_headPoseMutex);

                head =
                    g_headPose;
            }

            {
                std::lock_guard<std::mutex>
                    stereoLock(g_stereoMutex);

                stereo =
                    g_stereoData;
            }
        }


        if (
            !head.valid
            ||
            !stereo.posesValid
            ||
            !stereo.left.valid
            ||
            !stereo.right.valid
            )
        {
            g_renderPoseInFlight.store(
                false
            );

            return 0;
        }


        unsigned long long generation =
            g_renderPoseGeneration.fetch_add(
                1
            )
            +
            1;


        {
            std::lock_guard<std::mutex>
                renderPoseLock(g_renderPoseMutex);

            g_renderPoseSnapshot.head =
                head;

            g_renderPoseSnapshot.left =
                stereo.left;

            g_renderPoseSnapshot.right =
                stereo.right;

            g_renderPoseSnapshot.generation =
                generation;

            g_renderPoseSnapshot.valid =
                true;
        }


        return 1;
    }


    __declspec(dllexport)
        void TCGVR_ReleaseRenderPoseSnapshot()
    {
        {
            std::lock_guard<std::mutex>
                renderPoseLock(g_renderPoseMutex);

            g_renderPoseSnapshot.valid =
                false;
        }


        g_renderPoseInFlight.store(
            false
        );
    }


    __declspec(dllexport)
        int TCGVR_GetRenderPoseInfo(
            unsigned long long* capturedGeneration,
            unsigned long long* submittedGeneration,
            int* inFlight)
    {
        if (
            capturedGeneration ==
            nullptr
            ||
            submittedGeneration ==
            nullptr
            ||
            inFlight ==
            nullptr
            )
        {
            return -7200;
        }


        *capturedGeneration =
            g_renderPoseGeneration.load();

        *submittedGeneration =
            g_lastSubmittedRenderPoseGeneration.load();

        *inFlight =
            g_renderPoseInFlight.load()
            ?
            1
            :
            0;


        return 1;
    }


    __declspec(dllexport)
        int TCGVR_GetRenderHeadPose(
            float* posX,
            float* posY,
            float* posZ,
            float* rotX,
            float* rotY,
            float* rotZ,
            float* rotW)
    {
        std::lock_guard<std::mutex>
            renderPoseLock(g_renderPoseMutex);


        if (
            !g_renderPoseSnapshot.valid
            ||
            !g_renderPoseSnapshot.head.valid
            )
        {
            return 0;
        }


        const HeadPoseData& head =
            g_renderPoseSnapshot.head;


        *posX = head.posX;
        *posY = head.posY;
        *posZ = head.posZ;

        *rotX = head.rotX;
        *rotY = head.rotY;
        *rotZ = head.rotZ;
        *rotW = head.rotW;


        return 1;
    }


    __declspec(dllexport)
        int TCGVR_GetRenderLeftEye(
            float* posX,
            float* posY,
            float* posZ,
            float* rotX,
            float* rotY,
            float* rotZ,
            float* rotW,
            float* angleLeft,
            float* angleRight,
            float* angleUp,
            float* angleDown)
    {
        std::lock_guard<std::mutex>
            renderPoseLock(g_renderPoseMutex);


        if (
            !g_renderPoseSnapshot.valid
            ||
            !g_renderPoseSnapshot.left.valid
            )
        {
            return 0;
        }


        const StereoEyeData& eye =
            g_renderPoseSnapshot.left;


        *posX = eye.posX;
        *posY = eye.posY;
        *posZ = eye.posZ;

        *rotX = eye.rotX;
        *rotY = eye.rotY;
        *rotZ = eye.rotZ;
        *rotW = eye.rotW;

        *angleLeft = eye.angleLeft;
        *angleRight = eye.angleRight;
        *angleUp = eye.angleUp;
        *angleDown = eye.angleDown;


        return 1;
    }


    __declspec(dllexport)
        int TCGVR_GetRenderRightEye(
            float* posX,
            float* posY,
            float* posZ,
            float* rotX,
            float* rotY,
            float* rotZ,
            float* rotW,
            float* angleLeft,
            float* angleRight,
            float* angleUp,
            float* angleDown)
    {
        std::lock_guard<std::mutex>
            renderPoseLock(g_renderPoseMutex);


        if (
            !g_renderPoseSnapshot.valid
            ||
            !g_renderPoseSnapshot.right.valid
            )
        {
            return 0;
        }


        const StereoEyeData& eye =
            g_renderPoseSnapshot.right;


        *posX = eye.posX;
        *posY = eye.posY;
        *posZ = eye.posZ;

        *rotX = eye.rotX;
        *rotY = eye.rotY;
        *rotZ = eye.rotZ;
        *rotW = eye.rotW;

        *angleLeft = eye.angleLeft;
        *angleRight = eye.angleRight;
        *angleUp = eye.angleUp;
        *angleDown = eye.angleDown;


        return 1;
    }


    // ============================================================
    // Tracking Prediction (0.0.37: normalerweise 0 ms)
    // ============================================================

    __declspec(dllexport)
        int TCGVR_SetTrackingPredictionMs(
            int milliseconds)
    {
        if (milliseconds < 0)
        {
            milliseconds = 0;
        }


        if (milliseconds > 50)
        {
            milliseconds = 50;
        }


        g_trackingPredictionMilliseconds.store(
            milliseconds
        );


        return milliseconds;
    }


    __declspec(dllexport)
        int TCGVR_GetTrackingPredictionMs()
    {
        return
            g_trackingPredictionMilliseconds.load();
    }


    // ============================================================
    // Head Pose
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetHeadPose(
            float* posX,
            float* posY,
            float* posZ,
            float* rotX,
            float* rotY,
            float* rotZ,
            float* rotW)
    {
        std::lock_guard<std::mutex>
            lock(g_headPoseMutex);


        if (!g_headPose.valid)
        {
            return 0;
        }


        *posX = g_headPose.posX;
        *posY = g_headPose.posY;
        *posZ = g_headPose.posZ;

        *rotX = g_headPose.rotX;
        *rotY = g_headPose.rotY;
        *rotZ = g_headPose.rotZ;
        *rotW = g_headPose.rotW;


        return 1;
    }


    // ============================================================
    // Stereo Config
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetStereoConfiguration(
            int* viewCount,
            int* recommendedWidth,
            int* recommendedHeight,
            int* recommendedSampleCount)
    {
        std::lock_guard<std::mutex>
            lock(g_stereoMutex);


        if (
            !g_stereoData.configurationValid
            )
        {
            return 0;
        }


        *viewCount =
            static_cast<int>(
                g_stereoData.viewCount
                );


        *recommendedWidth =
            static_cast<int>(
                g_stereoData.recommendedWidth
                );


        *recommendedHeight =
            static_cast<int>(
                g_stereoData.recommendedHeight
                );


        *recommendedSampleCount =
            static_cast<int>(
                g_stereoData.recommendedSampleCount
                );


        return 1;
    }


    // ============================================================
    // Left Eye
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetLeftEye(
            float* posX,
            float* posY,
            float* posZ,
            float* rotX,
            float* rotY,
            float* rotZ,
            float* rotW,
            float* angleLeft,
            float* angleRight,
            float* angleUp,
            float* angleDown)
    {
        std::lock_guard<std::mutex>
            lock(g_stereoMutex);


        if (
            !g_stereoData.posesValid
            ||
            !g_stereoData.left.valid
            )
        {
            return 0;
        }


        const StereoEyeData&
            eye =
            g_stereoData.left;


        *posX = eye.posX;
        *posY = eye.posY;
        *posZ = eye.posZ;

        *rotX = eye.rotX;
        *rotY = eye.rotY;
        *rotZ = eye.rotZ;
        *rotW = eye.rotW;

        *angleLeft = eye.angleLeft;
        *angleRight = eye.angleRight;
        *angleUp = eye.angleUp;
        *angleDown = eye.angleDown;


        return 1;
    }


    // ============================================================
    // Right Eye
    // ============================================================

    __declspec(dllexport)
        int TCGVR_GetRightEye(
            float* posX,
            float* posY,
            float* posZ,
            float* rotX,
            float* rotY,
            float* rotZ,
            float* rotW,
            float* angleLeft,
            float* angleRight,
            float* angleUp,
            float* angleDown)
    {
        std::lock_guard<std::mutex>
            lock(g_stereoMutex);


        if (
            !g_stereoData.posesValid
            ||
            !g_stereoData.right.valid
            )
        {
            return 0;
        }


        const StereoEyeData&
            eye =
            g_stereoData.right;


        *posX = eye.posX;
        *posY = eye.posY;
        *posZ = eye.posZ;

        *rotX = eye.rotX;
        *rotY = eye.rotY;
        *rotZ = eye.rotZ;
        *rotW = eye.rotW;

        *angleLeft = eye.angleLeft;
        *angleRight = eye.angleRight;
        *angleUp = eye.angleUp;
        *angleDown = eye.angleDown;


        return 1;
    }


    // ============================================================
    // Shutdown
    // ============================================================

    __declspec(dllexport)
        void TCGVR_ShutdownOpenXR()
    {
        g_trackingThreadShouldRun.store(
            false
        );


        if (
            g_trackingThread.joinable()
            )
        {
            g_trackingThread.join();
        }


        {
            std::lock_guard<std::mutex>
                renderPoseLock(g_renderPoseMutex);

            g_renderPoseSnapshot.valid =
                false;
        }

        g_renderPoseInFlight.store(
            false
        );


        ClearUnityTexturePointers();


        DestroySwapchains();


        if (
            g_viewSpace !=
            XR_NULL_HANDLE
            )
        {
            xrDestroySpace(
                g_viewSpace
            );


            g_viewSpace =
                XR_NULL_HANDLE;
        }


        if (
            g_localSpace !=
            XR_NULL_HANDLE
            )
        {
            xrDestroySpace(
                g_localSpace
            );


            g_localSpace =
                XR_NULL_HANDLE;
        }


        if (
            g_session !=
            XR_NULL_HANDLE
            )
        {
            if (
                g_sessionBegun.load()
                )
            {
                xrEndSession(
                    g_session
                );


                g_sessionBegun.store(
                    false
                );
            }


            xrDestroySession(
                g_session
            );


            g_session =
                XR_NULL_HANDLE;
        }


        if (
            g_instance !=
            XR_NULL_HANDLE
            )
        {
            xrDestroyInstance(
                g_instance
            );


            g_instance =
                XR_NULL_HANDLE;
        }


        g_systemId =
            XR_NULL_SYSTEM_ID;
    }


}