using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ValheimVRM
{
    public static class AvatarRendering
    {
        public sealed class Options
        {
            public bool SceneLighting = true;
            public bool ReceiveShadows = true;
            public bool Bloom = false;
        }

        public static Options Current { get; private set; } = new Options();
        internal static Shader OptionsShader { get; private set; }
        internal static Shader LegacyOptionsShader { get; private set; }
        internal static Shader DepthShader { get; private set; }
        static AssetBundle bundle;
        static string SettingsPath => Path.Combine(Settings.ConfigDir, "rendering_options.json");

        public static void Initialize()
        {
            try
            {
                var path = Path.Combine(Settings.ConfigDir, "rendering_options.json");
                Current = File.Exists(path)
                    ? JsonConvert.DeserializeObject<Options>(File.ReadAllText(path)) ?? new Options() : new Options();
            }
            catch (Exception ex) { Debug.LogWarning("[ValheimVRM] Cannot load rendering options: " + ex.Message); }
            AvatarBloomController.Enabled = !Current.Bloom;
            try
            {
                using (var stream = typeof(AvatarRendering).Assembly.GetManifestResourceStream("ValheimVRM.avatar_rendering"))
                using (var bytes = new MemoryStream())
                {
                    if (stream == null) throw new FileNotFoundException("Rendering shader bundle is missing.");
                    stream.CopyTo(bytes);
                    bundle = AssetBundle.LoadFromMemory(bytes.ToArray());
                }
                OptionsShader = bundle.LoadAsset<Shader>("Assets/AvatarRendering/MToon10/vrmc_materials_mtoon.shader");
                if (OptionsShader == null || !OptionsShader.isSupported) throw new NotSupportedException("Rendering options shader is not supported.");
                LegacyOptionsShader = bundle.LoadAsset<Shader>("Assets/AvatarRendering/MToon/MToon.shader");
                if (LegacyOptionsShader == null || !LegacyOptionsShader.isSupported) throw new NotSupportedException("Legacy MToon rendering options shader is not supported.");
                DepthShader = bundle.LoadAsset<Shader>("Assets/AvatarRendering/AvatarDepth.shader");
                if (DepthShader == null || !DepthShader.isSupported) throw new NotSupportedException("Avatar depth shader is not supported.");
            }
            catch (Exception ex) { Debug.LogError("[ValheimVRM] Cannot load rendering shader: " + ex.Message); }
        }

        public static void Set(bool sceneLighting, bool receiveShadows, bool bloom)
        {
            var next = new Options { SceneLighting = sceneLighting, ReceiveShadows = receiveShadows, Bloom = bloom };
            Directory.CreateDirectory(Settings.ConfigDir);
            var temporary = SettingsPath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(next, Formatting.Indented));
                if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
                else File.Move(temporary, SettingsPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Current = next;
            AvatarBloomController.Enabled = !bloom;
            foreach (var target in UnityEngine.Object.FindObjectsByType<AvatarRenderingTarget>(FindObjectsSortMode.None)) target.Apply();
        }
    }
}
