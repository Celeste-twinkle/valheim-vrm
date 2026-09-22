using System;
using System.Collections.Generic;
using System.Linq;

namespace ValheimVRM.Sync
{
    // Part visibility is carried inside calibration format 1 so existing servers
    // can validate and relay it. Older clients see unknown animation keys and
    // safely ignore them because no animator state has this reserved prefix.
    public static class AvatarPartSync
    {
        public const string Prefix = "parts:hidden:";
        public const string ShownPrefix = "parts:shown:";
        const int HashBytes = 32;
        const int HashesPerEntry = 8;
        static bool Hex(char value) => value >= '0' && value <= '9' || value >= 'a' && value <= 'f';
        public static bool ValidId(string value) => value != null && value.Length == HashBytes * 2 && value.All(Hex);

        public static IEnumerable<string> Encode(IEnumerable<string> hidden)
            => Encode(Prefix, hidden);
        public static IEnumerable<string> EncodeShown(IEnumerable<string> shown)
            => Encode(ShownPrefix, shown);
        static IEnumerable<string> Encode(string prefix, IEnumerable<string> values)
        {
            var ids = values?.Where(ValidId).Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToArray()
                ?? new string[0];
            for (int start = 0; start < ids.Length; start += HashesPerEntry)
            {
                int count = Math.Min(HashesPerEntry, ids.Length - start);
                var bytes = new byte[count * HashBytes];
                for (int item = 0; item < count; item++)
                    for (int i = 0; i < HashBytes; i++)
                        bytes[item * HashBytes + i] = Convert.ToByte(ids[start + item].Substring(i * 2, 2), 16);
                yield return prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }
        }

        public static bool TryDecode(string key, ISet<string> hidden)
            => TryDecode(Prefix, key, hidden);
        public static bool TryDecodeShown(string key, ISet<string> shown)
            => TryDecode(ShownPrefix, key, shown);
        public static bool IsReserved(string key) => key != null &&
            (key.StartsWith(Prefix, StringComparison.Ordinal) || key.StartsWith(ShownPrefix, StringComparison.Ordinal));
        static bool TryDecode(string prefix, string key, ISet<string> values)
        {
            if (values == null || key == null || !key.StartsWith(prefix, StringComparison.Ordinal)) return false;
            try
            {
                string payload = key.Substring(prefix.Length).Replace('-', '+').Replace('_', '/');
                payload += new string('=', (4 - payload.Length % 4) % 4);
                var bytes = Convert.FromBase64String(payload);
                if (bytes.Length == 0 || bytes.Length > HashBytes * HashesPerEntry || bytes.Length % HashBytes != 0) return false;
                for (int offset = 0; offset < bytes.Length; offset += HashBytes)
                    values.Add(string.Concat(bytes.Skip(offset).Take(HashBytes).Select(b => b.ToString("x2"))));
                return true;
            }
            catch (FormatException) { return false; }
        }
    }
}
