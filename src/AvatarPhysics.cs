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
        static string SettingsPath => Path.Combine(Settings.ConfigDir, "physics_options.json");
        internal static float ClampWeight(float value) => float.IsNaN(value) || float.IsInfinity(value) ? .5f : Mathf.Clamp01(value);

        public static void Initialize()
        {
            try
            {
                var path = Path.Combine(Settings.ConfigDir, "physics_options.json");
                Weight = File.Exists(path)
                    ? ClampWeight((JsonConvert.DeserializeObject<Options>(File.ReadAllText(path)) ?? new Options()).Weight) : .5f;
            }
            catch (Exception ex) { Debug.LogWarning("[ValheimVRM] Cannot load physics options: " + ex.Message); }
        }

        // Preview while dragging; persist once the gesture finishes, not on every GUI event.
        public static void Preview(float weight) { Weight = ClampWeight(weight); }
        public static void Save()
        {
            AvatarConfigFile.WriteAllText(SettingsPath,
                JsonConvert.SerializeObject(new Options { Weight = Weight }, Formatting.Indented), "physics options");
        }
    }
}
