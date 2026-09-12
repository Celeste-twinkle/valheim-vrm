using System;
using System.Collections.Generic;
using UniGLTF;
using UniVRM10;
using UnityEngine;

namespace ValheimVRM
{
    /// <summary>Caps imported MToon10 color factors in the VRM's linear space.</summary>
    public sealed class AvatarBrightness : IMaterialDescriptorGenerator
    {
        public const float BaseColorLimit = .45f;
        public const float ShadeColorLimit = .2025f;
        readonly IMaterialDescriptorGenerator generator = new BuiltInVrm10MaterialDescriptorGenerator();

        public MaterialDescriptor Get(GltfData data, int index) => WithLimit(generator.Get(data, index));
        public MaterialDescriptor GetGltfDefault(string materialName = null) => WithLimit(generator.GetGltfDefault(materialName));

        static MaterialDescriptor WithLimit(MaterialDescriptor descriptor)
        {
            if (descriptor.Shader == null || descriptor.Shader.name != "VRM10/MToon10") return descriptor;
            var actions = new List<Action<Material>>(descriptor.Actions);
            // Runs after material properties/keywords are set, before UniVRM
            // captures expression baselines. No material copies or file edits.
            actions.Add(Apply);
            return new MaterialDescriptor(descriptor.Name, descriptor.Shader, descriptor.RenderQueue,
                descriptor.TextureSlots, descriptor.FloatValues, descriptor.Colors, descriptor.Vectors,
                actions, descriptor.AsyncActions);
        }

        public static void Apply(Material material)
        {
            if (!AvatarRenderingTarget.Supports(material)) return;
            LimitProperty(material, "_Color", BaseColorLimit);
            LimitProperty(material, "_ShadeColor", ShadeColorLimit);
        }

        static void LimitProperty(Material material, string property, float limit)
        {
            if (!material.HasProperty(property)) return;
            var original = material.GetColor(property);
            var limited = LimitSrgb(original, limit);
            if (!original.Equals(limited)) material.SetColor(property, limited);
        }

        public static Color LimitSrgb(Color color, float linearLimit)
        {
            // UniVRM converts the glTF/VRM linear factors to sRGB for Unity's
            // Color properties. Compare first to leave lower values bit-exact,
            // and to make repeated application a no-op at the boundary.
            float ceiling = Mathf.LinearToGammaSpace(linearLimit);
            float peak = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (peak <= ceiling) return color;
            var linear = color.linear;
            float scale = linearLimit / Mathf.Max(linear.r, Mathf.Max(linear.g, linear.b));
            // Scale the whole RGB vector instead of clipping individual channels.
            // Assign the largest channel exactly to avoid roundoff-driven drift.
            return new Color(
                color.r == peak ? ceiling : Mathf.LinearToGammaSpace(linear.r * scale),
                color.g == peak ? ceiling : Mathf.LinearToGammaSpace(linear.g * scale),
                color.b == peak ? ceiling : Mathf.LinearToGammaSpace(linear.b * scale),
                color.a);
        }
    }
}
