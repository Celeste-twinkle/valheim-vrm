using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ValheimVRM.Sync;

namespace ValheimVRM
{
    // Received controls belong to one rendered player instance. They never enter
    // Settings' shared model cache or the observer's personal preferences.
    public sealed class AvatarCalibrationBinding : MonoBehaviour
    {
        public string Encoded { get; private set; }
        public Settings.VrmSettingsContainer Settings { get; private set; }
        public AvatarCalibrationOptions.Profile Profile { get; private set; }
        public float PhysicsWeight { get; private set; } = .5f;
        public HashSet<string> HiddenParts { get; } = new HashSet<string>(StringComparer.Ordinal);
        public HashSet<string> ShownParts { get; } = new HashSet<string>(StringComparer.Ordinal);
        public bool PartVisibilityEnabled { get; private set; } = true;

        public bool Initialize(Settings.VrmSettingsContainer source, string encoded, bool applyParts = true)
        {
            Settings = source.CloneForInstance();
            Profile = new AvatarCalibrationOptions.Profile();
            return Apply(encoded, applyParts);
        }
        public bool Apply(string encoded, bool applyParts = true)
        {
            if (!AvatarCalibrationCodec.TryDecode(encoded, out var data)) return false;
            // Update existing objects in place: held items retain their group
            // references, and changes do not need a model reload or spring reset.
            Profile.Animations.Clear();
            HiddenParts.Clear(); ShownParts.Clear(); PartVisibilityEnabled = applyParts;
            foreach (var pair in data.Animations)
            {
                if (AvatarPartSync.IsReserved(pair.Key))
                {
                    if (applyParts) { AvatarPartSync.TryDecode(pair.Key, HiddenParts); AvatarPartSync.TryDecodeShown(pair.Key, ShownParts); }
                    continue;
                }
                if (!pair.Key.StartsWith("legacy:", StringComparison.Ordinal)) Profile.Set(pair.Key, Vector(pair.Value));
            }
            Copy(Profile.Left, data.Left); Copy(Profile.Right, data.Right); Copy(Profile.TwoHanded, data.TwoHanded);
            Copy(Profile.Back, data.Back);
            Settings.StandingHeightOffset = data.Standing; Settings.SittingHeightOffset = data.Sitting;
            Settings.ModelOffsetY = data.ModelY; Settings.EquipmentScale = data.EquipmentScale;
            Settings.LeftHandItemPos = Vector(data.LegacyLeft); Settings.RightHandItemPos = Vector(data.LegacyRight);
            Settings.SittingIdleOffset = Legacy(data, "sit");
            Settings.SittingOnChairOffset = Legacy(data, "chair"); Settings.SittingOnThroneOffset = Legacy(data, "throne");
            Settings.SittingOnShipOffset = Legacy(data, "ship"); Settings.HoldingMastOffset = Legacy(data, "mast");
            Settings.HoldingDragonOffset = Legacy(data, "dragon"); Settings.SleepingOffset = Legacy(data, "sleep");
            PhysicsWeight = data.Physics; Encoded = encoded;
            var physics = GetComponent<AvatarPhysicsWeight>();
            if (physics != null) physics.SynchronizedWeight = data.Physics;
            GetComponent<AvatarPartVisibility>()?.SetOverrides(HiddenParts, ShownParts);
            return true;
        }
        static Vector3 Legacy(AvatarCalibrationData data, string key) => data.Animations.TryGetValue("legacy:" + key, out var value) ? Vector(value) : Vector3.zero;
        static Vector3 Vector(AvatarCalibrationData.Position p) => new Vector3(p.X, p.Y, p.Z);
        static AvatarCalibrationData.Position Position(Vector3 p) => new AvatarCalibrationData.Position(p.x, p.y, p.z);
        static void Copy(AvatarCalibrationOptions.Equipment target, AvatarCalibrationData.Item source)
        { target.Scale = source.Scale; target.Position.Value = Vector(source.Offset); }
        static AvatarCalibrationData.Item Item(AvatarCalibrationOptions.Equipment source) =>
            new AvatarCalibrationData.Item { Scale = source.Multiplier, Offset = Position(source.Position.Value) };
        public static string Capture(string model, bool includeParts = true)
        {
            var settings = ValheimVRM.Settings.GetSettings(model) ?? new ValheimVRM.Settings.VrmSettingsContainer();
            var profile = AvatarCalibrationOptions.Current.Get(model);
            var data = new AvatarCalibrationData {
                Standing = AvatarHeightOffsets.Clamp(settings.StandingHeightOffset), Sitting = AvatarHeightOffsets.Clamp(settings.SittingHeightOffset),
                ModelY = settings.ModelOffsetY, EquipmentScale = settings.EquipmentScale, Physics = AvatarPhysics.Weight,
                LegacyLeft = Position(settings.LeftHandItemPos), LegacyRight = Position(settings.RightHandItemPos),
                Left = Item(profile.Left), Right = Item(profile.Right), TwoHanded = Item(profile.TwoHanded), Back = Item(profile.Back)
            };
            foreach (var pair in profile.Animations)
                if (pair.Value != null && pair.Value.Value != Vector3.zero) data.Animations[pair.Key] = Position(pair.Value.Value);
            AddLegacy(data, "sit", settings.SittingIdleOffset);
            AddLegacy(data, "chair", settings.SittingOnChairOffset); AddLegacy(data, "throne", settings.SittingOnThroneOffset);
            AddLegacy(data, "ship", settings.SittingOnShipOffset); AddLegacy(data, "mast", settings.HoldingMastOffset);
            AddLegacy(data, "dragon", settings.HoldingDragonOffset); AddLegacy(data, "sleep", settings.SleepingOffset);
            if (includeParts)
            {
                var state = AvatarPartOptions.Current.Get(model);
                var keys = AvatarPartSync.Encode(state.Hidden).Concat(AvatarPartSync.EncodeShown(state.Shown)).ToArray();
                if (data.Animations.Count + keys.Length > AvatarCalibrationCodec.MaxEntries)
                    throw new InvalidOperationException("Too many avatar part overrides to synchronize.");
                // The value is only a format-1 carrier marker. Keep it zero so
                // calibration relays predating the +/-1 m range accept it too.
                foreach (var key in keys) data.Animations[key] = new AvatarCalibrationData.Position(0, 0, 0);
            }
            return AvatarCalibrationCodec.Encode(data);
        }
        static void AddLegacy(AvatarCalibrationData data, string key, Vector3 value)
        { if (value != Vector3.zero) data.Animations["legacy:" + key] = Position(value); }
    }
}
