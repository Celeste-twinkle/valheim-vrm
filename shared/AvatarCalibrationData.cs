using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ValheimVRM.Sync
{
    // BCL-only wire data; the server never references Unity avatars or client DLLs.
    public sealed class AvatarCalibrationData
    {
        public struct Position
        {
            public float X, Y, Z;
            public Position(float x, float y, float z) { X = x; Y = y; Z = z; }
        }
        public sealed class Item
        {
            public float Scale = 1;
            public Position Offset;
        }
        public float Standing, Sitting, ModelY, Physics = .5f, EquipmentScale = 1;
        public Position LegacyLeft, LegacyRight;
        public Item Left = new Item(), Right = new Item(), TwoHanded = new Item();
        public readonly Dictionary<string, Position> Animations = new Dictionary<string, Position>(StringComparer.Ordinal);
        // The reserved positional-settings namespace permits additive controls
        // without changing format 1. Existing relays preserve these entries.
        public Item Back
        {
            get => new Item {
                Scale = Animations.TryGetValue("legacy:back.scale", out var scale) ? scale.X : 1,
                Offset = Animations.TryGetValue("legacy:back.offset", out var offset) ? offset : default(Position)
            };
            set {
                if (value.Scale == 1) Animations.Remove("legacy:back.scale");
                else Animations["legacy:back.scale"] = new Position(value.Scale, 0, 0);
                if (value.Offset.X == 0 && value.Offset.Y == 0 && value.Offset.Z == 0) Animations.Remove("legacy:back.offset");
                else Animations["legacy:back.offset"] = value.Offset;
            }
        }
    }

    public static class AvatarCalibrationCodec
    {
        public const int MaxEntries = 768, MaxBytes = 128 * 1024, MaxEncodedLength = (MaxBytes + 2) / 3 * 4;
        public static readonly string Default = Encode(new AvatarCalibrationData());
        static bool Range(float value, float min, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
        static bool Valid(AvatarCalibrationData.Position p, float limit) => Range(p.X, -limit, limit) && Range(p.Y, -limit, limit) && Range(p.Z, -limit, limit);
        static bool Valid(AvatarCalibrationData.Item item) => item != null && Range(item.Scale, .25f, 2) && Valid(item.Offset, .5f);
        static void Write(BinaryWriter writer, AvatarCalibrationData.Position p) { writer.Write(p.X); writer.Write(p.Y); writer.Write(p.Z); }
        static AvatarCalibrationData.Position Read(BinaryReader reader) => new AvatarCalibrationData.Position(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        static void Write(BinaryWriter writer, AvatarCalibrationData.Item item) { writer.Write(item.Scale); Write(writer, item.Offset); }
        static AvatarCalibrationData.Item ReadItem(BinaryReader reader) => new AvatarCalibrationData.Item { Scale = reader.ReadSingle(), Offset = Read(reader) };
        public static string Encode(AvatarCalibrationData data)
        {
            if (data == null || !Range(data.Standing, -.5f, .5f) || !Range(data.Sitting, -.5f, .5f) ||
                !Range(data.ModelY, -5, 5) || !Range(data.Physics, 0, 1) || !Range(data.EquipmentScale, .01f, 10) ||
                !Valid(data.LegacyLeft, 5) || !Valid(data.LegacyRight, 5) ||
                !Valid(data.Left) || !Valid(data.Right) || !Valid(data.TwoHanded) || !Valid(data.Back) || data.Animations.Count > MaxEntries)
                throw new ArgumentException("Invalid avatar calibration.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write((byte)1);
                writer.Write(data.Standing); writer.Write(data.Sitting); writer.Write(data.ModelY);
                writer.Write(data.Physics); writer.Write(data.EquipmentScale);
                Write(writer, data.LegacyLeft); Write(writer, data.LegacyRight);
                Write(writer, data.Left); Write(writer, data.Right); Write(writer, data.TwoHanded);
                writer.Write(data.Animations.Count);
                foreach (var pair in data.Animations.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Key.Length > 384 || pair.Key.Any(char.IsControl) ||
                        !Valid(pair.Value, pair.Key.StartsWith("legacy:", StringComparison.Ordinal) ? 5 : .5f))
                        throw new ArgumentException("Invalid animation calibration.");
                    writer.Write(pair.Key); Write(writer, pair.Value);
                }
                writer.Flush();
                if (stream.Length > MaxBytes) throw new ArgumentException("Avatar calibration is too large.");
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        public static bool TryDecode(string encoded, out AvatarCalibrationData data)
        {
            data = null;
            if (string.IsNullOrEmpty(encoded) || encoded.Length > MaxEncodedLength) return false;
            try
            {
                var bytes = Convert.FromBase64String(encoded);
                if (bytes.Length > MaxBytes) return false;
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadByte() != 1) return false;
                    var result = new AvatarCalibrationData {
                        Standing = reader.ReadSingle(), Sitting = reader.ReadSingle(), ModelY = reader.ReadSingle(),
                        Physics = reader.ReadSingle(), EquipmentScale = reader.ReadSingle(),
                        LegacyLeft = Read(reader), LegacyRight = Read(reader),
                        Left = ReadItem(reader), Right = ReadItem(reader), TwoHanded = ReadItem(reader)
                    };
                    int count = reader.ReadInt32();
                    if (count < 0 || count > MaxEntries) return false;
                    string previous = null;
                    for (int i = 0; i < count; i++)
                    {
                        string key = reader.ReadString();
                        if (previous != null && StringComparer.Ordinal.Compare(previous, key) >= 0) return false;
                        result.Animations.Add(key, Read(reader)); previous = key;
                    }
                    if (stream.Position != stream.Length || Encode(result) != encoded) return false;
                    data = result; return true;
                }
            }
            catch (Exception) { return false; }
        }
    }
}
