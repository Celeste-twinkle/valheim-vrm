using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace ValheimVRM
{
    // Model preferences contain values only, never runtime bones or corrected poses.
    public sealed class AvatarCalibrationOptions
    {
        public sealed class Offset
        {
            public float X, Y, Z;
            [JsonIgnore] public Vector3 Value
            {
                get => new Vector3(Limit(X), Limit(Y), Limit(Z));
                set { X = Limit(value.x); Y = Limit(value.y); Z = Limit(value.z); }
            }
            static float Limit(float value) => AvatarHeightOffsets.Clamp(value);
        }
        public sealed class Equipment
        {
            public float Scale = 1;
            public Offset Position = new Offset();
            [JsonIgnore] public float Multiplier => ClampScale(Scale);
        }
        public sealed class Profile
        {
            public Dictionary<string, Offset> Animations = new Dictionary<string, Offset>(StringComparer.Ordinal);
            public Equipment Left = new Equipment(), Right = new Equipment(), TwoHanded = new Equipment();
            public Vector3 Get(string key) => key != null && Animations.TryGetValue(key, out var value) && value != null
                ? value.Value : Vector3.zero;
            public void Set(string key, Vector3 value)
            {
                if (value == Vector3.zero) Animations.Remove(key);
                else Animations[key] = new Offset { Value = value };
            }
        }
        static AvatarCalibrationOptions current;
        public static AvatarCalibrationOptions Current => current ?? (current = Open(Settings.ConfigDir));
        readonly string path;
        Dictionary<string, Profile> profiles = new Dictionary<string, Profile>(StringComparer.Ordinal);
        public bool Dirty { get; private set; }
        public AvatarCalibrationOptions(string directory) { path = Path.Combine(directory, "avatar_calibration.json"); }
        static AvatarCalibrationOptions Open(string directory)
        {
            var result = new AvatarCalibrationOptions(directory);
            try { result.Load(); }
            catch (Exception ex) { Debug.LogWarning("[ValheimVRM] Cannot read calibration preferences: " + ex.Message); }
            return result;
        }
        public static float ClampScale(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp(value, .25f, 2f);
        public Profile Get(string model)
        {
            model = model ?? "";
            if (!profiles.TryGetValue(model, out var profile)) profiles[model] = profile = new Profile();
            return profile;
        }
        public void Changed() { Dirty = true; }
        public void Load()
        {
            if (!File.Exists(path)) return;
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, Profile>>(File.ReadAllText(path));
            var clean = new Dictionary<string, Profile>(StringComparer.Ordinal);
            if (loaded != null)
                foreach (var pair in loaded)
                {
                    var profile = pair.Value ?? new Profile();
                    profile.Animations = profile.Animations ?? new Dictionary<string, Offset>(StringComparer.Ordinal);
                    profile.Left = Clean(profile.Left); profile.Right = Clean(profile.Right); profile.TwoHanded = Clean(profile.TwoHanded);
                    clean[pair.Key] = profile;
                }
            profiles = clean; Dirty = false;
        }
        static Equipment Clean(Equipment value)
        {
            value = value ?? new Equipment();
            value.Scale = ClampScale(value.Scale);
            value.Position = value.Position ?? new Offset();
            value.Position.Value = value.Position.Value;
            return value;
        }
        public void Save()
        {
            if (!Dirty) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonConvert.SerializeObject(profiles, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                Dirty = false;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
