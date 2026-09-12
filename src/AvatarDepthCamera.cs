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
                        // Blended overlays must remain transparent. Only opaque
                        // and cutout MToon surfaces own a deferred surface pixel.
                        if (!AvatarRenderingTarget.Supports(source) || source.renderQueue > 2500 ||
                            source.GetFloat("_AlphaMode") > 1.5f || source.GetFloat("_M_ZWrite") < .5f) continue;
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
            material.SetTexture("_MainTex", source.GetTexture("_MainTex"));
            material.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
            material.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
            material.SetColor("_Color", source.GetColor("_Color"));
            material.SetFloat("_Cutoff", source.GetFloat("_AlphaMode") > .5f ? source.GetFloat("_Cutoff") : -1f);
            material.SetFloat("_Cull", source.GetFloat("_M_CullMode"));
            material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
            material.SetFloat("_BumpScale", source.GetFloat("_BumpScale"));
            material.SetTexture("_UvAnimMaskTex", source.GetTexture("_UvAnimMaskTex"));
            material.SetFloat("_UvAnimScrollXSpeed", source.GetFloat("_UvAnimScrollXSpeed"));
            material.SetFloat("_UvAnimScrollYSpeed", source.GetFloat("_UvAnimScrollYSpeed"));
            material.SetFloat("_UvAnimRotationSpeed", source.GetFloat("_UvAnimRotationSpeed"));
            if (source.IsKeywordEnabled("_MTOON_PARAMETERMAP")) material.EnableKeyword("_MTOON_PARAMETERMAP");
            else material.DisableKeyword("_MTOON_PARAMETERMAP");
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
