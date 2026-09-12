using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace ValheimVRM
{
    [HarmonyPatch(typeof(TaaComponent), nameof(TaaComponent.Render))]
    static class PatchAvatarBloomTemporalInput
    {
        static void Postfix(TaaComponent __instance)
        {
            // TAA resolves its jittered scene before Bloom.Prepare. Sample the
            // current coverage at that same offset; reset on each camera cull.
            var camera = __instance.context.camera;
            if (camera != null) camera.GetComponent<AvatarBloomCamera>()?.SetTemporalJitter(__instance.jitterVector);
        }
    }

    [HarmonyPatch(typeof(BloomComponent), nameof(BloomComponent.Prepare))]
    public static class PatchAvatarBloom
    {
        static void Prefix(BloomComponent __instance, ref RenderTexture source, out RenderTexture __state)
        {
            __state = null;
            if (!AvatarBloomController.Enabled) return;
            var camera = __instance.context.camera;
            var limiter = camera != null ? camera.GetComponent<AvatarBloomCamera>() : null;
            if (limiter == null || !limiter.HasVisibleAvatar || limiter.BloomMask == null || limiter.BloomFilter == null) return;

            var descriptor = source.descriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            __state = RenderTexture.GetTemporary(descriptor);
            // Only BloomComponent receives this copy. The original scene color, eye
            // adaptation and color grading keep the avatar's ordinary lit appearance.
            Graphics.Blit(source, __state, limiter.BloomFilter, 1);
            source = __state;
        }

        static Exception Finalizer(Exception __exception, RenderTexture __state)
        {
            if (__state != null) RenderTexture.ReleaseTemporary(__state);
            return __exception;
        }
    }
}
