using UnityEngine;
using System.Collections.Generic;

namespace ValheimVRM
{
    public sealed class AvatarRenderingTarget : MonoBehaviour
    {
        internal static readonly HashSet<AvatarRenderingTarget> Active = new HashSet<AvatarRenderingTarget>();
        internal Renderer[] Renderers { get; private set; }
        readonly List<Material> queueMaterials = new List<Material>();
        void Awake() { Renderers = GetComponentsInChildren<Renderer>(true); }
        void Start() { Apply(); }
        void OnEnable() { Active.Add(this); Apply(); }
        void OnDisable() { Active.Remove(this); }
        void OnDestroy() { Active.Remove(this); }

        public void Apply()
        {
            var options = AvatarRendering.Current;
            bool useOriginal = options.SceneLighting && options.ReceiveShadows;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!SupportsSurface(material)) continue;
                    NormalizeTransparencyQueue(material);
                    // Non-MToon shaders retain their own lighting and color.
                    // Share depth, transparency ordering and bloom coverage.
                    if (!Supports(material)) continue;
                    bool legacy = IsLegacy(material);
                    string originalName = legacy ? "VRM/MToon" : "VRM10/MToon10";
                    var optionsShader = legacy ? AvatarRendering.LegacyOptionsShader : AvatarRendering.OptionsShader;
                    // Keep the material object so UniVRM expression bindings continue
                    // to animate it. These options intentionally apply to all avatars
                    // on this client, including instances sharing this material.
                    if (useOriginal)
                    {
                        if (material.shader.name == originalName) continue;
                        var original = Shader.Find(originalName);
                        if (original != null) SetShader(material, original);
                    }
                    else if (optionsShader != null)
                    {
                        SetShader(material, optionsShader);
                        material.SetFloat("_AvatarSceneLighting", options.SceneLighting ? 1 : 0);
                    }
                }
            }
        }

        static void SetShader(Material material, Shader shader)
        {
            // Unity resets the render queue when the shader changes. Preserve
            // cutout/transparent ordering for hair, face overlays and accessories.
            int queue = material.renderQueue;
            material.shader = shader;
            material.renderQueue = queue;
        }

        internal static bool Supports(Material material)
        {
            return material != null && material.shader != null &&
                (material.shader.name == "VRM10/MToon10" || material.shader.name == "ValheimVRM/MToon10Options" || IsLegacy(material));
        }

        internal static bool IsLegacy(Material material) => material != null && material.shader != null &&
            (material.shader.name == "VRM/MToon" || material.shader.name == "ValheimVRM/MToonOptions");

        internal static bool IsUniUnlit(Material material) => material != null && material.shader != null && material.shader.name == "UniGLTF/UniUnlit";
        internal static bool SupportsSurface(Material material) => material != null && material.shader != null &&
            !AvatarFurSurface.IsFur(material) && (material.HasProperty("_MainTex") || material.HasProperty("_BaseMap"));

        internal static string MainTextureProperty(Material material) => material.HasProperty("_MainTex") ? "_MainTex" : "_BaseMap";
        internal static Color MainColor(Material material) => material.HasProperty("_Color") ? material.GetColor("_Color") :
            material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
        internal static float Cutoff(Material material) => material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : .5f;
        internal static bool HasDeferredSurface(Material material) => material.FindPass("DEFERRED") >= 0 && material.GetShaderPassEnabled("DEFERRED");

        internal void PrepareRenderQueues()
        {
            if (Renderers == null) return;
            foreach (var renderer in Renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
                renderer.GetSharedMaterials(queueMaterials);
                foreach (var material in queueMaterials)
                    if (SupportsSurface(material)) NormalizeTransparencyQueue(material);
            }
        }

        static void NormalizeTransparencyQueue(Material material)
        {
            // Valheim applies post-effect AO before transparent draws. A blended
            // surface in an opaque queue is shaded with background AO as though
            // it were opaque. Restore its transparent stage without changing its
            // alpha, blend/depth-write settings or any already-valid queue.
            if (AlphaMode(material) <= 1.5f || material.renderQueue > 2500) return;
            int previous = material.renderQueue;
            material.renderQueue = ZWrite(material) > .5f ? 2501 : 3000;
            Debug.Log($"[ValheimVRM] Corrected transparent render queue for {material.name}: {previous} -> {material.renderQueue}");
        }

        internal static float AlphaMode(Material material)
        {
            if (Supports(material)) return material.GetFloat(IsLegacy(material) ? "_BlendMode" : "_AlphaMode");
            if (material.IsKeywordEnabled("_ALPHATEST_ON")) return 1;
            if (material.IsKeywordEnabled("_ALPHABLEND_ON") || material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")) return 2;
            // These fragment shaders use keywords, not their inspector enums
            // or potentially stale RenderType override tags, for alpha mode.
            if (IsUniUnlit(material) || material.shader.name == "Standard" || material.shader.name == "Standard (Specular setup)") return 0;
            string tag = material.GetTag("RenderType", false, "Opaque");
            return tag == "TransparentCutout" ? 1 : tag == "Transparent" ? 2 : 0;
        }
        internal static float CullMode(Material material) => material.HasProperty("_M_CullMode") ? material.GetFloat("_M_CullMode") :
            material.HasProperty("_CullMode") ? material.GetFloat("_CullMode") : material.HasProperty("_Cull") ? material.GetFloat("_Cull") : 2;
        internal static float ZWrite(Material material) => material.HasProperty("_M_ZWrite") ? material.GetFloat("_M_ZWrite") :
            material.HasProperty("_ZWrite") ? material.GetFloat("_ZWrite") : AlphaMode(material) < 1.5f || material.shader.name.EndsWith("ZWrite") ? 1 : 0;

        internal static void CopyUvAnimation(Material source, Material target)
        {
            bool unlit = IsUniUnlit(source);
            if (target.HasProperty("_VertexColorAlpha")) target.SetFloat("_VertexColorAlpha", unlit && source.IsKeywordEnabled("_VERTEXCOL_MUL") ? 1 : 0);
            if (!Supports(source))
            {
                target.SetFloat("_LegacyMToon", 0);
                target.SetFloat("_UvAnimScrollXSpeed", 0); target.SetFloat("_UvAnimScrollYSpeed", 0); target.SetFloat("_UvAnimRotationSpeed", 0);
                target.DisableKeyword("_MTOON_PARAMETERMAP");
                return;
            }
            bool legacy = IsLegacy(source);
            target.SetFloat("_LegacyMToon", legacy ? 1 : 0);
            target.SetTexture("_UvAnimMaskTex", source.GetTexture(legacy ? "_UvAnimMaskTexture" : "_UvAnimMaskTex"));
            target.SetFloat("_UvAnimScrollXSpeed", source.GetFloat(legacy ? "_UvAnimScrollX" : "_UvAnimScrollXSpeed"));
            target.SetFloat("_UvAnimScrollYSpeed", source.GetFloat(legacy ? "_UvAnimScrollY" : "_UvAnimScrollYSpeed"));
            target.SetFloat("_UvAnimRotationSpeed", source.GetFloat(legacy ? "_UvAnimRotation" : "_UvAnimRotationSpeed"));
            if (legacy || source.IsKeywordEnabled("_MTOON_PARAMETERMAP")) target.EnableKeyword("_MTOON_PARAMETERMAP");
            else target.DisableKeyword("_MTOON_PARAMETERMAP");
        }
    }
}
