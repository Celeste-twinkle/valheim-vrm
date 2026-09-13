using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ValheimVRM
{
    public static class AvatarHeightOffsets
    {
        public static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, -.5f, .5f);

        public static void Save(string modelName)
        {
            if (string.IsNullOrEmpty(modelName) || Path.GetFileName(modelName) != modelName) throw new ArgumentException("Invalid avatar name");
            var settings = Settings.GetSettings(modelName);
            if (settings == null || settings.Name != modelName) return;
            var path = Settings.PlayerSettingsPath(modelName, false);
            var lines = new List<string>();
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    int separator = line.IndexOf('=');
                    string key = separator >= 0 ? line.Substring(0, separator).Trim() : "";
                    if (key != nameof(settings.StandingHeightOffset) && key != nameof(settings.SittingHeightOffset)) lines.Add(line);
                }
            }
            lines.Add(nameof(settings.StandingHeightOffset) + "=" + Clamp(settings.StandingHeightOffset).ToString("R", CultureInfo.CurrentCulture));
            lines.Add(nameof(settings.SittingHeightOffset) + "=" + Clamp(settings.SittingHeightOffset).ToString("R", CultureInfo.CurrentCulture));
            Directory.CreateDirectory(Settings.ConfigDir);
            var temporary = path + ".tmp";
            try
            {
                File.WriteAllLines(temporary, lines);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
