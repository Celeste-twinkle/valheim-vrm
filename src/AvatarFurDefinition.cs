using System;
using Newtonsoft.Json.Linq;

namespace ValheimVRM
{
    // Application data, deliberately optional: ordinary VRM readers keep MToon.
    public sealed class AvatarFurDefinition
    {
        public const string Key = "ValheimVRM_fur";
        public float Length, Randomness, RootOffset, NoiseScaleX, NoiseScaleY;
        public float DirectionX, DirectionY, DirectionZ;
        public int Density, LengthImage = -1, NoiseImage = -1, MaskImage = -1;

        public static AvatarFurDefinition Parse(JToken token)
        {
            if (!(token is JObject value) || value["version"]?.Type != JTokenType.Integer || (long)value["version"] != 1) return null;
            if (value["enabled"] != null)
            {
                if (value["enabled"].Type != JTokenType.Boolean) throw new FormatException("Fur enabled must be a boolean.");
                if (!(bool)value["enabled"]) return null;
            }
            if (value["density"] != null && value["density"].Type != JTokenType.Integer) throw new FormatException("Fur density must be an integer.");
            var result = new AvatarFurDefinition {
                Length = Number(value, "length", .005f, 0, .03f),
                Density = (int)Number(value, "density", 2, 1, 3),
                Randomness = Number(value, "randomness", .5f, 0, 1),
                RootOffset = Number(value, "rootOffset", -1, -1, 0),
                NoiseScaleX = Number(value, "noiseScaleX", 1, .01f, 100),
                NoiseScaleY = Number(value, "noiseScaleY", 1, .01f, 100),
                DirectionX = Number(value, "directionX", 0, -1, 1),
                DirectionY = Number(value, "directionY", 0, -1, 1),
                DirectionZ = Number(value, "directionZ", 1, 0, 1),
                LengthImage = Image(value, "lengthImage"),
                NoiseImage = Image(value, "noiseImage"),
                MaskImage = Image(value, "maskImage")
            };
            return result.Length > 0 ? result : null;
        }

        static int Image(JObject value, string name)
        {
            var token = value[name];
            if (token == null) return -1;
            if (token.Type != JTokenType.Integer || (long)token < 0 || (long)token > 65535)
                throw new FormatException("Invalid fur image index: " + name);
            return (int)token;
        }

        static float Number(JObject value, string name, float fallback, float minimum, float maximum)
        {
            var token = value[name];
            if (token == null) return fallback;
            if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                throw new FormatException("Invalid fur number: " + name);
            float number = (float)token;
            if (float.IsNaN(number) || float.IsInfinity(number)) throw new FormatException("Non-finite fur number: " + name);
            return Math.Max(minimum, Math.Min(maximum, number));
        }
    }
}
