using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRM.Sync;

namespace ValheimVRM
{
    // Apply after expressions and fur color synchronization. Per-renderer blocks
    // keep shared imports and other players' materials untouched.
    [DefaultExecutionOrder(32500)]
    public sealed class AvatarBrightnessTarget : MonoBehaviour
    {
        static readonly string[] ColorProperties = {
            "_Color", "_BaseColor", "_ShadeColor", "_EmissionColor", "_RimColor", "_MatcapColor", "_OutlineColor"
        };
        struct ColorProperty
        {
            public int Id;
            public bool Hdr;
        }
        sealed class Slot
        {
            public Renderer Renderer;
            public int Index;
            public Material Material;
            public ColorProperty[] Colors;
            public MaterialPropertyBlock Original = new MaterialPropertyBlock();
            public MaterialPropertyBlock Baseline = new MaterialPropertyBlock();
            public MaterialPropertyBlock Working = new MaterialPropertyBlock();
            public bool Applied;
        }
        readonly List<Slot> slots = new List<Slot>();
        bool followLocalSettings;
        public float SynchronizedBrightness = AvatarCalibrationData.DefaultBrightness;
        public float Brightness => AvatarRendering.ClampBrightness(followLocalSettings
            ? AvatarRendering.Current.Brightness : SynchronizedBrightness);

        public void Setup(bool local, float brightness = AvatarCalibrationData.DefaultBrightness)
        {
            Restore(); slots.Clear();
            followLocalSettings = local;
            SynchronizedBrightness = AvatarRendering.ClampBrightness(brightness);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || material.shader == null) continue;
                    var colors = new List<ColorProperty>();
                    foreach (string name in ColorProperties)
                    {
                        int index = material.shader.FindPropertyIndex(name);
                        if (index < 0 || material.shader.GetPropertyType(index) != ShaderPropertyType.Color) continue;
                        colors.Add(new ColorProperty { Id = Shader.PropertyToID(name),
                            Hdr = (material.shader.GetPropertyFlags(index) & ShaderPropertyFlags.HDR) != 0 });
                    }
                    if (colors.Count == 0) continue;
                    var slot = new Slot { Renderer = renderer, Index = i, Material = material, Colors = colors.ToArray() };
                    renderer.GetPropertyBlock(slot.Original, i);
                    slots.Add(slot);
                }
            }
            Apply();
        }

        void LateUpdate() { Apply(); }
        void OnDisable() { Restore(); }
        void OnDestroy() { Restore(); }

        public void Apply()
        {
            float brightness = Brightness;
            if (brightness == AvatarCalibrationData.DefaultBrightness) { Restore(); return; }
            foreach (var slot in slots)
            {
                if (slot.Renderer == null || slot.Material == null) continue;
                if (!slot.Applied)
                {
                    slot.Renderer.GetPropertyBlock(slot.Original, slot.Index);
                    slot.Renderer.GetPropertyBlock(slot.Baseline, slot.Index);
                    if (slot.Baseline.isEmpty) slot.Renderer.GetPropertyBlock(slot.Baseline);
                }
                slot.Renderer.GetPropertyBlock(slot.Working, slot.Index);
                // A per-material block takes precedence over the renderer-wide
                // block. Carry its properties across when there is no slot block.
                if (slot.Working.isEmpty) slot.Renderer.GetPropertyBlock(slot.Working);
                foreach (var property in slot.Colors)
                {
                    var color = slot.Material.GetColor(property.Id);
                    bool overridden = slot.Baseline.HasProperty(property.Id);
                    if (overridden) color = slot.Baseline.GetVector(property.Id);
                    var linear = property.Hdr || (overridden && QualitySettings.activeColorSpace == ColorSpace.Linear) ? color : color.linear;
                    linear.r *= brightness; linear.g *= brightness; linear.b *= brightness;
                    var output = property.Hdr || QualitySettings.activeColorSpace == ColorSpace.Linear ? linear : linear.gamma;
                    output.a = color.a;
                    // MPB.SetColor always converts sRGB in a linear project,
                    // including HDR properties. Convert our GPU-space result
                    // back first; this also replaces existing color-tagged values.
                    slot.Working.SetColor(property.Id, QualitySettings.activeColorSpace == ColorSpace.Linear ? output.gamma : output);
                }
                slot.Renderer.SetPropertyBlock(slot.Working, slot.Index);
                slot.Applied = true;
            }
        }

        void Restore()
        {
            foreach (var slot in slots)
            {
                if (!slot.Applied) continue;
                if (slot.Renderer != null) slot.Renderer.SetPropertyBlock(slot.Original.isEmpty ? null : slot.Original, slot.Index);
                slot.Applied = false;
            }
        }
    }
}
