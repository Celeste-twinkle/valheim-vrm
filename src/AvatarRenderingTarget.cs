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
            if (!useOriginal && AvatarRendering.OptionsShader == null) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!Supports(material)) continue;
                    // Keep the material object so UniVRM expression bindings continue
                    // to animate it. These options intentionally apply to all avatars
                    // on this client, including instances sharing this material.
                    if (useOriginal)
                    {
                        if (material.shader.name == "VRM10/MToon10") continue;
                        var original = Shader.Find("VRM10/MToon10");
                        if (original != null) SetShader(material, original);
                    }
                    else
                    {
                        SetShader(material, AvatarRendering.OptionsShader);
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
                (material.shader.name == "VRM10/MToon10" || material.shader.name == "ValheimVRM/MToon10Options");
        }
    }
}
