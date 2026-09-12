using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ValheimVRM
{
    public static class AvatarPhysics
    {
        public sealed class Options { public float Weight = .5f; }
        public static float Weight { get; private set; } = .5f;
        static string SettingsPath => Path.Combine(Settings.ValheimVRMDir, "physics_options.json");
        internal static float ClampWeight(float value) => float.IsNaN(value) || float.IsInfinity(value) ? .5f : Mathf.Clamp01(value);

        public static void Initialize()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    Weight = ClampWeight((JsonConvert.DeserializeObject<Options>(File.ReadAllText(SettingsPath)) ?? new Options()).Weight);
            }
            catch (Exception ex) { Debug.LogWarning("[ValheimVRM] Cannot load physics options: " + ex.Message); }
        }

        // Preview while dragging; persist once the gesture finishes, not on every GUI event.
        public static void Preview(float weight) { Weight = ClampWeight(weight); }
        public static void Save()
        {
            Directory.CreateDirectory(Settings.ValheimVRMDir);
            var temporary = SettingsPath + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(new Options { Weight = Weight }, Formatting.Indented));
                if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
                else File.Move(temporary, SettingsPath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
