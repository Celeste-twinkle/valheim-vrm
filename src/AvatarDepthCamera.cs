using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ValheimVRM
{
    public sealed class AvatarDepthController : MonoBehaviour
    {
        void OnEnable() { Camera.onPreCull += Prepare; }
        void OnDisable()
        {
            Camera.onPreCull -= Prepare;
            foreach (var camera in Resources.FindObjectsOfTypeAll<AvatarDepthCamera>()) Destroy(camera);
        }

        static void Prepare(Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            foreach (var target in AvatarRenderingTarget.Active)
                if (target != null && target.isActiveAndEnabled) target.PrepareRenderQueues();
            var depth = camera.GetComponent<AvatarDepthCamera>();
            if (depth == null && camera.actualRenderingPath == RenderingPath.DeferredShading && AvatarRenderingTarget.Active.Count != 0)
                depth = camera.gameObject.AddComponent<AvatarDepthCamera>();
            if (depth != null) depth.Prepare(camera);
        }
    }

    public sealed class AvatarDepthCamera : MonoBehaviour
    {
        const CameraEvent RenderEvent = CameraEvent.AfterGBuffer;
        static readonly RenderTargetIdentifier[] Buffers = {
            BuiltinRenderTextureType.GBuffer0, BuiltinRenderTextureType.GBuffer1,
            BuiltinRenderTextureType.GBuffer2
        };
        readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        readonly List<Material> sharedMaterials = new List<Material>();
        readonly List<Material> staleMaterials = new List<Material>();
        Camera owner;
        CommandBuffer commands;

        internal void Prepare(Camera camera)
        {
            if (commands == null)
            {
                owner = camera;
                commands = new CommandBuffer { name = "ValheimVRM: avatar depth and normals" };
                owner.AddCommandBuffer(RenderEvent, commands);
            }
            commands.Clear();
            if (camera.actualRenderingPath != RenderingPath.DeferredShading || AvatarRendering.DepthShader == null) return;
            bool drawing = false;
            foreach (var target in AvatarRenderingTarget.Active)
            {
                if (target == null || !target.isActiveAndEnabled) continue;
                foreach (var renderer in target.Renderers)
                {
                    if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                        renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                    renderer.GetSharedMaterials(sharedMaterials);
                    for (int i = 0; i < sharedMaterials.Count; i++)
                    {
                        var source = sharedMaterials[i];
                        if (!AvatarRenderingTarget.SupportsSurface(source)) continue;
                        // Transparent-mode materials can contain fully opaque
                        // pixels, including exporters using an early queue. Write
                        // their actual surface only where sampled alpha is one;
                        // partial coverage must keep the depth behind it.
                        bool blended = AvatarRenderingTarget.AlphaMode(source) > 1.5f;
                        // Standard/PBR already supplies its own GBuffer lighting.
                        // Never erase it with the forward-only surface bridge.
                        if (!blended && AvatarRenderingTarget.HasDeferredSurface(source)) continue;
                        if (!blended && (source.renderQueue > 2500 || AvatarRenderingTarget.ZWrite(source) < .5f)) continue;
                        if (!drawing)
                        {
                            // Leave the lighting/emission target untouched. It can
                            // alias the camera target depending on HDR and Unity version.
                            commands.SetRenderTarget(Buffers, BuiltinRenderTextureType.CameraTarget);
                            drawing = true;
                        }
                        commands.DrawRenderer(renderer, GetMaterial(source), i, 0);
                    }
                }
            }
            if (drawing) commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            staleMaterials.Clear();
            foreach (var source in materials.Keys) if (source == null) staleMaterials.Add(source);
            foreach (var source in staleMaterials) { Destroy(materials[source]); materials.Remove(source); }
        }

        Material GetMaterial(Material source)
        {
            if (!materials.TryGetValue(source, out var material))
            {
                material = new Material(AvatarRendering.DepthShader) { hideFlags = HideFlags.HideAndDontSave };
                materials.Add(source, material);
            }
            string texture = AvatarRenderingTarget.MainTextureProperty(source);
            material.SetTexture("_MainTex", source.GetTexture(texture));
            material.SetTextureScale("_MainTex", source.GetTextureScale(texture));
            material.SetTextureOffset("_MainTex", source.GetTextureOffset(texture));
            material.SetColor("_Color", AvatarRenderingTarget.MainColor(source));
            float alphaMode = AvatarRenderingTarget.AlphaMode(source);
            // Use the live texture * color alpha, including UV/expression changes.
            // A small float tolerance admits alpha=1 after sampling, not fabric
            // alpha such as 0.35/0.7 or transparent holes. No source edits/copies.
            material.SetFloat("_Cutoff", alphaMode > 1.5f ? .99999f : alphaMode > .5f ? AvatarRenderingTarget.Cutoff(source) : -1f);
            material.SetFloat("_Cull", AvatarRenderingTarget.CullMode(source));
            material.SetTexture("_BumpMap", source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null);
            material.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 0);
            AvatarRenderingTarget.CopyUvAnimation(source, material);
            if (source.IsKeywordEnabled("_NORMALMAP")) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            return material;
        }

        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
        void Release()
        {
            if (commands != null)
            {
                if (owner != null) owner.RemoveCommandBuffer(RenderEvent, commands);
                commands.Release(); commands = null;
            }
            foreach (var material in materials.Values) if (material != null) Destroy(material);
            materials.Clear();
        }
    }
}
