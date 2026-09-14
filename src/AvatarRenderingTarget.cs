using UnityEngine;
using System.Collections.Generic;

namespace ValheimVRM
{
    public sealed class AvatarRenderingTarget : MonoBehaviour
    {
        internal static readonly HashSet<AvatarRenderingTarget> Active = new HashSet<AvatarRenderingTarget>();
        internal Renderer[] Renderers { get; private set; }
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

        internal static float AlphaMode(Material material) => material.GetFloat(IsLegacy(material) ? "_BlendMode" : "_AlphaMode");
        internal static float CullMode(Material material) => material.GetFloat(IsLegacy(material) ? "_CullMode" : "_M_CullMode");
        internal static float ZWrite(Material material) => material.GetFloat(IsLegacy(material) ? "_ZWrite" : "_M_ZWrite");

        internal static void CopyUvAnimation(Material source, Material target)
        {
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
