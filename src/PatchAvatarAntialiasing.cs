using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Rendering;

namespace ValheimVRM
{
    [HarmonyPatch(typeof(TaaComponent), nameof(TaaComponent.SetProjectionMatrix))]
    static class PatchAvatarAntialiasing
    {
        static void Postfix(TaaComponent __instance)
        {
            var camera = __instance.context.camera;
            if (camera == null || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)) return;
            if (!AvatarAntialiasingCamera.CanSeeAvatar(camera)) return;
            var state = camera.GetComponent<AvatarAntialiasingCamera>();
            if (state == null) state = camera.gameObject.AddComponent<AvatarAntialiasingCamera>();
            state.MatchProjection(camera);
        }
    }

    // Valheim's TAA disables jitter for transparent draws, although they test
    // against jittered opaque depth. Close-fitting blended clothes then fail
    // that depth test in moving strips. Keep both layers in the same projection
    // for this camera render and restore the post-processing setting afterward.
    public sealed class AvatarAntialiasingCamera : MonoBehaviour
    {
        static readonly Plane[] planes = new Plane[6];
        Camera owner;
        bool pendingRestore;
        bool previous;

        internal static bool CanSeeAvatar(Camera camera)
        {
            if (AvatarRenderingTarget.Active.Count == 0) return false;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            foreach (var target in AvatarRenderingTarget.Active)
            {
                if (target == null || !target.isActiveAndEnabled) continue;
                foreach (var renderer in target.Renderers)
                {
                    if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                        !renderer.gameObject.activeInHierarchy || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly ||
                        (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                    if (GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) return true;
                }
            }
            return false;
        }

        internal void MatchProjection(Camera camera)
        {
            owner = camera;
            if (!pendingRestore) previous = owner.useJitteredProjectionMatrixForTransparentRendering;
            pendingRestore = true;
            owner.useJitteredProjectionMatrixForTransparentRendering = true;
        }

        void OnPostRender() { Restore(); }
        void OnDisable() { Restore(); }
        void OnDestroy() { Restore(); }

        void Restore()
        {
            if (pendingRestore && owner != null) owner.useJitteredProjectionMatrixForTransparentRendering = previous;
            pendingRestore = false;
        }
    }
}
