using UnityEngine;

namespace ValheimVRM
{
    // References are serialized so Instantiate remaps the renderer to each player's
    // own skeleton. Meshes/materials stay owned by the resident import template.
    [DefaultExecutionOrder(32000)]
    public sealed class AvatarFurSurface : MonoBehaviour
    {
        [SerializeField] Renderer source;
        [SerializeField] Renderer overlay;
        Material[] originals, fur;
        SkinnedMeshRenderer skin, furSkin;

        public void Initialize(Renderer sourceRenderer, Renderer furRenderer)
        {
            source = sourceRenderer; overlay = furRenderer; Cache(); Sync();
        }
        void Awake() { if (source != null && overlay != null) Cache(); }
        void Cache()
        {
            originals = source.sharedMaterials; fur = overlay.sharedMaterials;
            skin = source as SkinnedMeshRenderer; furSkin = overlay as SkinnedMeshRenderer;
        }
        void LateUpdate() { Sync(); }
        void Sync()
        {
            if (source == null || overlay == null) return;
            overlay.enabled = source.enabled;
            overlay.forceRenderingOff = source.forceRenderingOff;
            gameObject.layer = source.gameObject.layer;
            if (skin != null && furSkin != null && skin.sharedMesh != null)
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) furSkin.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
            if (originals == null) Cache();
            for (int i = 0; i < fur.Length; i++)
            {
                var s = originals[i]; var f = fur[i];
                if (s == null || f == null || f.GetFloat("_FurLength") <= 0) continue;
                Copy(s, f, "_Color"); Copy(s, f, "_ShadeColor");
                f.SetTexture("_MainTex", s.GetTexture("_MainTex"));
                f.SetTextureScale("_MainTex", s.GetTextureScale("_MainTex"));
                f.SetTextureOffset("_MainTex", s.GetTextureOffset("_MainTex"));
                bool legacy = AvatarRenderingTarget.IsLegacy(s);
                f.SetTexture("_ShadeTex", s.GetTexture(legacy ? "_ShadeTexture" : "_ShadeTex"));
                f.SetFloat("_ShadingToonyFactor", s.GetFloat(legacy ? "_ShadeToony" : "_ShadingToonyFactor"));
                f.SetFloat("_ShadingShiftFactor", s.GetFloat(legacy ? "_ShadeShift" : "_ShadingShiftFactor"));
                f.SetFloat("_GiEqualization", legacy ? 1 - s.GetFloat("_IndirectLightIntensity") : s.GetFloat("_GiEqualization"));
                f.SetFloat("_FurBaseAlphaMode", AvatarRenderingTarget.AlphaMode(s));
                f.SetFloat("_Cutoff", s.GetFloat("_Cutoff"));
                AvatarRenderingTarget.CopyUvAnimation(s, f);
                f.SetFloat("_AvatarSceneLighting", AvatarRendering.Current.SceneLighting ? 1 : 0);
                f.SetFloat("_AvatarReceiveShadows", AvatarRendering.Current.ReceiveShadows ? 1 : 0);
                if (AvatarRendering.Current.ReceiveShadows) f.EnableKeyword("AVATAR_FUR_SHADOWS");
                else f.DisableKeyword("AVATAR_FUR_SHADOWS");
            }
        }
        static void Copy(Material source, Material target, string key) { if (source.HasProperty(key)) target.SetColor(key, source.GetColor(key)); }
        internal static bool IsFur(Material material) => material != null && material.shader != null && material.shader.name == "ValheimVRM/Fur";
    }
}
