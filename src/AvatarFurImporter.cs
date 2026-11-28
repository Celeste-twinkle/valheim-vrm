using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UniGLTF;
using UnityEngine;

namespace ValheimVRM
{
    internal sealed class AvatarFurImporter : IMaterialDescriptorGenerator
    {
        readonly IMaterialDescriptorGenerator generator;
        readonly Dictionary<int, AvatarFurDefinition> definitions = new Dictionary<int, AvatarFurDefinition>();
        readonly Dictionary<Material, AvatarFurDefinition> bindings = new Dictionary<Material, AvatarFurDefinition>();
        JObject json;

        public static IMaterialDescriptorGenerator Wrap(IMaterialDescriptorGenerator generator, string sourceJson, out AvatarFurImporter fur)
        {
            fur = null;
            if (sourceJson.IndexOf(AvatarFurDefinition.Key, StringComparison.Ordinal) < 0) return generator;
            var candidate = new AvatarFurImporter(generator, sourceJson);
            if (candidate.definitions.Count == 0) return generator;
            fur = candidate; return candidate;
        }

        public AvatarFurImporter(IMaterialDescriptorGenerator generator, string sourceJson)
        {
            this.generator = generator;
            try
            {
                json = JObject.Parse(sourceJson);
                var materials = json["materials"] as JArray;
                if (materials == null) return;
                for (int i = 0; i < materials.Count; i++)
                {
                    try
                    {
                        var definition = AvatarFurDefinition.Parse(materials[i]["extras"]?[AvatarFurDefinition.Key]);
                        if (definition != null) definitions[i] = definition;
                    }
                    catch (Exception ex) { Debug.LogWarning("[ValheimVRM] Ignoring invalid fur material " + i + ": " + ex.Message); }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ValheimVRM] Ignoring fur metadata: " + ex.Message); }
        }

        public MaterialDescriptor GetGltfDefault(string materialName = null) => generator.GetGltfDefault(materialName);
        public MaterialDescriptor Get(GltfData data, int index)
        {
            var descriptor = generator.Get(data, index);
            if (!definitions.TryGetValue(index, out var definition)) return descriptor;
            var actions = new List<Action<Material>>(descriptor.Actions) { m => bindings[m] = definition };
            return new MaterialDescriptor(descriptor.Name, descriptor.Shader, descriptor.RenderQueue,
                descriptor.TextureSlots, descriptor.FloatValues, descriptor.Colors, descriptor.Vectors, actions, descriptor.AsyncActions);
        }

        public void Attach(RuntimeGltfInstance loaded, byte[] glb)
        {
            if (bindings.Count == 0) return;
            var sources = loaded.Root.GetComponentsInChildren<Renderer>(true).Where(source => {
                var slots = source.sharedMaterials;
                var mesh = (source as SkinnedMeshRenderer)?.sharedMesh ?? source.GetComponent<MeshFilter>()?.sharedMesh;
                return mesh != null && mesh.vertexCount <= 200000 && mesh.subMeshCount == slots.Length &&
                    slots.Any(m => AvatarRenderingTarget.Supports(m) && bindings.ContainsKey(m));
            }).ToArray();
            if (sources.Length == 0) return;
            var used = new HashSet<Material>(sources.SelectMany(r => r.sharedMaterials));
            var shader = AvatarFurResources.Acquire(loaded.Root);
            if (shader == null)
            {
                Debug.LogWarning("[ValheimVRM] GPU fur is unavailable; keeping the standard VRM material.");
                return;
            }
            var textures = new Dictionary<int, Texture2D>();
            var materials = new Dictionary<Material, Material>();
            // Bind by the importer's exact material objects, never by a model or clothing name.
            foreach (var pair in bindings)
            {
                if (!used.Contains(pair.Key) || !AvatarRenderingTarget.Supports(pair.Key)) continue;
                try
                {
                    var d = pair.Value;
                    var length = Image(d.LengthImage, loaded, glb, textures);
                    var noise = Image(d.NoiseImage, loaded, glb, textures);
                    var mask = Image(d.MaskImage, loaded, glb, textures);
                    var material = new Material(shader) { name = pair.Key.name + " (GPU fur)" };
                    material.EnableKeyword("AVATAR_FUR_ON");
                    loaded.AddResource(material);
                    material.SetFloat("_FurLength", d.Length);
                    material.SetFloat("_FurDensity", d.Density);
                    material.SetFloat("_FurRandomness", d.Randomness);
                    material.SetFloat("_FurRootOffset", d.RootOffset);
                    material.SetVector("_FurDirection", new Vector4(d.DirectionX, d.DirectionY, d.DirectionZ, 0));
                    material.SetTexture("_FurLengthMask", length ?? Texture2D.whiteTexture);
                    material.SetTexture("_FurNoise", noise ?? Texture2D.whiteTexture);
                    material.SetTextureScale("_FurNoise", new Vector2(d.NoiseScaleX, d.NoiseScaleY));
                    material.SetTexture("_FurMask", mask ?? Texture2D.whiteTexture);
                    materials[pair.Key] = material;
                }
                catch (Exception ex) { Debug.LogWarning("[ValheimVRM] Keeping standard material for " + pair.Key.name + ": " + ex.Message); }
            }
            if (materials.Count == 0)
            {
                foreach (var texture in textures.Values) UnityEngine.Object.Destroy(texture);
                UnityEngine.Object.DestroyImmediate(loaded.Root.GetComponent<AvatarFurResources>());
                return;
            }
            Material empty = null;
            foreach (var source in sources)
            {
                var original = source.sharedMaterials;
                if (!original.Any(m => m != null && materials.ContainsKey(m))) continue;
                Mesh mesh = (source as SkinnedMeshRenderer)?.sharedMesh ?? source.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.vertexCount > 200000 || mesh.subMeshCount != original.Length) continue;
                if (empty == null)
                {
                    empty = new Material(shader) { name = "ValheimVRM empty fur slot" };
                    empty.SetFloat("_FurLength", 0); loaded.AddResource(empty);
                }
                var child = new GameObject("ValheimVRM GPU fur");
                child.layer = source.gameObject.layer;
                child.transform.SetParent(source.transform, false);
                Renderer overlay;
                if (source is SkinnedMeshRenderer skin)
                {
                    var copy = child.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = mesh; copy.bones = skin.bones; copy.rootBone = skin.rootBone;
                    copy.quality = skin.quality; copy.updateWhenOffscreen = skin.updateWhenOffscreen;
                    var bounds = skin.localBounds; bounds.Expand(.12f); copy.localBounds = bounds;
                    overlay = copy;
                }
                else
                {
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    overlay = child.AddComponent<MeshRenderer>();
                }
                overlay.sharedMaterials = original.Select(m => m != null && materials.TryGetValue(m, out var fur) ? fur : empty).ToArray();
                overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                overlay.lightProbeUsage = source.lightProbeUsage; overlay.probeAnchor = source.probeAnchor;
                overlay.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                var surface = child.AddComponent<AvatarFurSurface>();
                surface.Initialize(source, overlay);
                loaded.AddRenderer(overlay);
            }
        }

        Texture2D Image(int index, RuntimeGltfInstance loaded, byte[] glb, Dictionary<int, Texture2D> cache)
        {
            if (index < 0) return null;
            if (cache.TryGetValue(index, out var found)) return found;
            var images = json["images"] as JArray;
            if (images == null || index >= images.Count) throw new FormatException("Fur image does not exist.");
            var image = images[index];
            // Only embedded PNGs: no external files or downloads are needed for the optional effect.
            if (image["uri"] != null || (string)image["mimeType"] != "image/png") throw new FormatException("Fur masks must be embedded PNGs.");
            int viewIndex = (int)image["bufferView"];
            var views = json["bufferViews"] as JArray;
            if (views == null || viewIndex < 0 || viewIndex >= views.Count) throw new FormatException("Fur image buffer is invalid.");
            var view = views[viewIndex];
            if ((int?)view["buffer"] != 0) throw new FormatException("Fur image must use the GLB buffer.");
            long offset = (long?)view["byteOffset"] ?? 0;
            int size = (int)view["byteLength"];
            int binHeader = 20 + checked((int)BitConverter.ToUInt32(glb, 12));
            long start = binHeader + 8L + offset;
            if (size < 24 || size > 8 * 1024 * 1024 || offset < 0 || binHeader + 8 > glb.Length ||
                BitConverter.ToUInt32(glb, binHeader + 4) != 0x004e4942 || start + size > glb.Length)
                throw new FormatException("Fur image buffer range is invalid.");
            var bytes = new byte[size]; Buffer.BlockCopy(glb, checked((int)start), bytes, 0, size);
            if (bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71) throw new FormatException("Invalid PNG mask.");
            uint width = BigEndian(bytes, 16), height = BigEndian(bytes, 20);
            if (width == 0 || height == 0 || width > 4096 || height > 4096) throw new FormatException("Fur mask exceeds 4096 pixels.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true) { name = "ValheimVRM fur mask " + index, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            loaded.AddResource(texture);
            // The current game's ImageConversion module targets netstandard 2.1;
            // keep this net471 plugin compatible with older Valheim assemblies too.
            var conversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", true);
            var load = conversion.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            try
            {
                if (load == null || !(bool)load.Invoke(null, new object[] { texture, bytes, true })) throw new FormatException("Cannot decode fur mask.");
            }
            catch { UnityEngine.Object.Destroy(texture); throw; }
            cache[index] = texture; return texture;
        }
        static uint BigEndian(byte[] b, int p) => ((uint)b[p] << 24) | ((uint)b[p + 1] << 16) | ((uint)b[p + 2] << 8) | b[p + 3];
    }
}
