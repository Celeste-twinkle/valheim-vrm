using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ValheimVRM
{
    public sealed class AvatarBloomCamera : MonoBehaviour
    {
        const CameraEvent RenderEvent = CameraEvent.AfterForwardAlpha;
        readonly Dictionary<Material, Material> masks = new Dictionary<Material, Material>();
        readonly List<Material> sharedMaterials = new List<Material>();
        readonly List<Draw> draws = new List<Draw>();
        Camera owner;
        CommandBuffer commands;
        internal RenderTexture BloomMask { get; private set; }
        internal Material BloomFilter { get; private set; }
        internal bool HasVisibleAvatar { get; private set; }

        struct Draw
        {
            internal Renderer Renderer;
            internal Material Mask;
            internal int Submesh;
        }

        internal void Prepare(Camera camera)
        {
            if (commands == null)
            {
                owner = camera;
                commands = new CommandBuffer { name = "ValheimVRM: exclude avatar from bloom" };
                owner.AddCommandBuffer(RenderEvent, commands);
            }
            commands.Clear();
            HasVisibleAvatar = false;
            if (BloomFilter != null) BloomFilter.SetVector("_AvatarBloomJitter", Vector4.zero);
            if (!AvatarBloomController.Enabled || AvatarBloomController.BloomShader == null) return;

            draws.Clear();
            foreach (var target in AvatarBloomTarget.Active)
            {
                if (target == null || !target.isActiveAndEnabled) continue;
                foreach (var renderer in target.Renderers)
                {
                    if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                        renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                    renderer.GetSharedMaterials(sharedMaterials);
                    for (int index = 0; index < sharedMaterials.Count; index++)
                    {
                        var source = sharedMaterials[index];
                        if (!AvatarRenderingTarget.Supports(source)) continue;
                        draws.Add(new Draw { Renderer = renderer, Mask = GetMask(source), Submesh = index });
                    }
                }
            }
            if (draws.Count == 0) return;
            HasVisibleAvatar = true;

            EnsureBloomMask(camera.pixelWidth, camera.pixelHeight);
            // Write only coverage. Scene HDR color is never copied, compressed or redrawn.
            // The camera depth buffer keeps foreground walls and equipment out of this mask.
            commands.SetRenderTarget(new RenderTargetIdentifier(BloomMask), BuiltinRenderTextureType.CameraTarget);
            commands.ClearRenderTarget(false, true, Color.clear);
            foreach (var draw in draws) commands.DrawRenderer(draw.Renderer, draw.Mask, draw.Submesh, 0);
            commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

            foreach (var source in masks.Keys.Where(m => m == null).ToArray())
            {
                Destroy(masks[source]);
                masks.Remove(source);
            }
        }

        internal void SetTemporalJitter(Vector2 jitter)
        {
            if (BloomFilter != null) BloomFilter.SetVector("_AvatarBloomJitter", new Vector4(jitter.x, jitter.y, 0, 0));
        }

        Material GetMask(Material source)
        {
            if (!masks.TryGetValue(source, out var mask))
            {
                mask = new Material(AvatarBloomController.BloomShader) { hideFlags = HideFlags.HideAndDontSave };
                masks.Add(source, mask);
            }
            mask.SetTexture("_MainTex", source.GetTexture("_MainTex"));
            mask.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
            mask.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
            mask.SetFloat("_Opacity", source.GetColor("_Color").a);
            float alphaMode = AvatarRenderingTarget.AlphaMode(source);
            mask.SetFloat("_BloomCutoff", alphaMode > 1.5f ? .001f : alphaMode > .5f ? source.GetFloat("_Cutoff") : -1f);
            mask.SetFloat("_Transparent", alphaMode > 1.5f ? 1f : 0f);
            mask.SetFloat("_Cull", AvatarRenderingTarget.CullMode(source));
            mask.SetFloat("_ZTest", (float)(AvatarRenderingTarget.ZWrite(source) > .5f ? CompareFunction.Equal : CompareFunction.LessEqual));
            AvatarRenderingTarget.CopyUvAnimation(source, mask);
            return mask;
        }

        void EnsureBloomMask(int width, int height)
        {
            if (BloomMask != null && BloomMask.width == width && BloomMask.height == height) return;
            if (BloomMask != null) { BloomMask.Release(); Destroy(BloomMask); }
            BloomMask = new RenderTexture(width, height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
            {
                name = "ValheimVRM bloom coverage",
                // TAA resolves subpixel samples; coverage must support the same
                // subpixel lookup instead of jumping between nearest pixels.
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            BloomMask.Create();
            if (BloomFilter == null)
                BloomFilter = new Material(AvatarBloomController.BloomShader) { hideFlags = HideFlags.HideAndDontSave };
            BloomFilter.SetTexture("_AvatarBloomMask", BloomMask);
        }

        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }

        void Release()
        {
            if (commands != null)
            {
                if (owner != null) owner.RemoveCommandBuffer(RenderEvent, commands);
                commands.Release();
                commands = null;
            }
            foreach (var mask in masks.Values) if (mask != null) Destroy(mask);
            masks.Clear();
            HasVisibleAvatar = false;
            if (BloomMask != null) { BloomMask.Release(); Destroy(BloomMask); BloomMask = null; }
            if (BloomFilter != null) { Destroy(BloomFilter); BloomFilter = null; }
        }
    }
}
