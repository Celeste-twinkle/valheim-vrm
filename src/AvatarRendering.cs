using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using ValheimVRM.Sync;

namespace ValheimVRM
{
    public static class AvatarRendering
    {
        public sealed class Options
        {
            public bool SceneLighting = true;
            public bool ReceiveShadows = true;
            public bool Bloom = false;
            public float Brightness = AvatarCalibrationData.DefaultBrightness;
        }

        public static Options Current { get; private set; } = new Options();
        internal static Shader OptionsShader { get; private set; }
        internal static Shader LegacyOptionsShader { get; private set; }
        internal static Shader DepthShader { get; private set; }
        static AssetBundle bundle;
        static string SettingsPath => Path.Combine(Settings.ConfigDir, "rendering_options.json");

        public static void Initialize()
        {
            LoadPreferences();
            LoadShaders();
        }

        internal static void LoadPreferences()
        {
            try
            {
                var path = Path.Combine(Settings.ConfigDir, "rendering_options.json");
                Current = File.Exists(path)
                    ? JsonConvert.DeserializeObject<Options>(File.ReadAllText(path)) ?? new Options() : new Options();
            }
            catch (Exception ex) { Debug.LogWarning("[ValheimVRM] Cannot load rendering options: " + ex.Message); }
            Current.Brightness = ClampBrightness(Current.Brightness);
            AvatarBloomController.Enabled = !Current.Bloom;
        }

        static void LoadShaders()
        {
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
            var next = new Options { SceneLighting = sceneLighting, ReceiveShadows = receiveShadows, Bloom = bloom, Brightness = Current.Brightness };
            AvatarConfigFile.WriteAllText(SettingsPath, JsonConvert.SerializeObject(next, Formatting.Indented), "rendering options");
            Current = next;
            AvatarBloomController.Enabled = !bloom;
            foreach (var target in UnityEngine.Object.FindObjectsByType<AvatarRenderingTarget>(FindObjectsSortMode.None)) target.Apply();
        }

        public static float ClampBrightness(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? AvatarCalibrationData.DefaultBrightness
            : Mathf.Clamp(value, AvatarCalibrationData.MinimumBrightness, AvatarCalibrationData.MaximumBrightness);

        // Match the other F8 sliders: preview live, write once on release.
        public static void PreviewBrightness(float value) { Current.Brightness = ClampBrightness(value); }
        public static void SaveBrightness()
        {
            AvatarConfigFile.WriteAllText(SettingsPath, JsonConvert.SerializeObject(Current, Formatting.Indented), "rendering options");
        }
    }
}
