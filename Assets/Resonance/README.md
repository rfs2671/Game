
## Build variants
| APK | XR loader | Packages | Install ID |
|---|---|---|---|
| Resonance.apk | XREAL (SDK 3.1.0, downloaded by CI) | com.xreal.xr | com.rfs2671.resonance |
| Resonance-openxr.apk | OpenXR | com.unity.xr.openxr 1.14.3 + com.unity.xr.meta-openxr 1.0.3 (AR Foundation 5.x line) | com.rfs2671.resonance.openxr |

The OpenXR variant is built by `Resonance.EditorTools.OpenXRVariant.BuildCI`: it removes the XREAL loader, assigns the OpenXR loader for Android,
enables Hand Tracking (XR Hands), Meta Quest Support, the Meta AR features (Session, Camera, Planes, Anchors, Raycasts) and Resonance's
`SeeThroughBlendFeature` (requests ADDITIVE, then ALPHA_BLEND environment blend when the runtime offers it), sets Vulkan first with a GLES3 fallback,
applies OpenXR's automatic project fixes, then builds. Locally: **Resonance > Build Variant > OpenXR (Android)** (rewrites settings; revert with git).
Both variants clear the camera to transparent black (0,0,0,0): additive glasses show the room, passthrough compositors use alpha 0.

SYSTEM CHECK's result screen shows XR on/off, the graphics API and the OpenXR blend mode in use.
