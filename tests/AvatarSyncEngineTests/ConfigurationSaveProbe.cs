using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;

public sealed partial class AvatarSyncEngineTests
{
    IEnumerator ConfigurationSaveTests(Player player, string first, string second)
    {
        var picker = OutfitSwitcher.Instance;
        var previousCatalog = picker.Catalog;
        var previousLocal = Player.m_localPlayer;
        var directory = Path.Combine(output, "configuration-save");
        var catalog = new AvatarCatalog(ValheimVRM.Settings.ValheimVRMDir, directory);
        catalog.Refresh(); catalog.Select(player.GetPlayerName(), first); catalog.Select("Other character", first);
        var selectionPath = Path.Combine(directory, "avatar_selections.json");
        var catalogField = AccessTools.Field(typeof(OutfitSwitcher), "<Catalog>k__BackingField");
        try
        {
            // Invoke the production writer inside Valheim's Mono runtime. The
            // injected OS failure deterministically exercises native fallback.
            var writer = typeof(MainPlugin).Assembly.GetType("ValheimVRM.AvatarConfigFile", true);
            var save = AccessTools.Method(writer, "Save");
            var fallbackPath = Path.Combine(directory, "中文 replacement.json");
            File.WriteAllText(fallbackPath, "old value");
            int attempts = 0;
            save.Invoke(null, new object[] { fallbackPath, new Action<string>(p => File.WriteAllText(p, "new value")), "engine 1175",
                new Action<string, string, string>((source, target, backup) => { attempts++; throw new IOException("Injected remove failure", unchecked((int)0x80070497)); }) });
            Check(attempts == 3 && File.ReadAllText(fallbackPath) == "new value", "Production Mono fallback did not recover error 1175");

            Player.m_localPlayer = player; catalogField.SetValue(picker, catalog);
            Check(picker.RequestSwitch(first), "Configuration probe initial switch rejected");
            yield return WaitConfigurationSwitch(picker);
            Check(picker.LastError == "" && VrmManager.PlayerToName[player] == first, "Initial saved switch failed: " + picker.LastError);
            var saved = File.ReadAllText(selectionPath);
            File.SetAttributes(selectionPath, FileAttributes.ReadOnly);
            try
            {
                Check(picker.RequestSwitch(second), "Readonly selection probe switch rejected");
                yield return WaitConfigurationSwitch(picker);
                Check(VrmManager.PlayerToName[player] == second && player.GetComponent<VrmController>().visual != null,
                    "Persistence failure prevented applying the selected model");
                Check(catalog.Resolve(player.GetPlayerName()) == second && catalog.Resolve("Other character") == first,
                    "Persistence failure lost the session choice or changed another character");
                Check(IsSelectionSaveWarning(picker.LastError), "Failed persistence was still reported as a failed avatar switch: " + picker.LastError);
                Check(File.ReadAllText(selectionPath) == saved, "Readonly failure modified the stored selection");
                var restart = new AvatarCatalog(ValheimVRM.Settings.ValheimVRMDir, directory); restart.Refresh(); restart.LoadSelections();
                Check(restart.Resolve(player.GetPlayerName()) == first, "Failed save incorrectly persisted the new model");

                Check(picker.RequestSwitch(AvatarCatalog.OriginalModel), "Native selection with readonly configuration rejected");
                yield return WaitConfigurationSwitch(picker);
                Check(!VrmManager.PlayerToVrmInstance.ContainsKey(player) && player.GetComponent<VrmController>().visual == null &&
                    catalog.IsOriginalSelected(player.GetPlayerName()), "Readonly configuration prevented restoring the native appearance/session choice");
                Check(IsSelectionSaveWarning(picker.LastError), "Native save failure was not reported accurately");
            }
            finally { File.SetAttributes(selectionPath, FileAttributes.Normal); }

            Check(picker.RequestSwitch(first), "Unlocked selection switch rejected");
            yield return WaitConfigurationSwitch(picker);
            Check(picker.LastError == "", "Unlocked save did not clear the warning");
            using (File.Open(selectionPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Check(picker.RequestSwitch(second), "Locked selection switch rejected");
                yield return WaitConfigurationSwitch(picker);
                Check(IsSelectionSaveWarning(picker.LastError) && VrmManager.PlayerToName[player] == second &&
                    catalog.Resolve(player.GetPlayerName()) == second, "Held-open configuration broke appearance or session selection");
            }
            Check(picker.RequestSwitch(first), "Released selection switch rejected");
            yield return WaitConfigurationSwitch(picker);
            Check(picker.LastError == "", "Saving after releasing the handle still failed");
            catalog.LoadSelections();
            Check(catalog.Resolve(player.GetPlayerName()) == first && catalog.Resolve("Other character") == first,
                "Recovered save did not persist both characters independently");

            // TXT creation/updates must preserve authored parameters/comments.
            var model = "__configuration_test_" + Guid.NewGuid().ToString("N");
            var txt = ValheimVRM.Settings.PlayerSettingsPath(model, false);
            try
            {
                ValheimVRM.Settings.AddSettingsFromFile(model, false);
                var settings = ValheimVRM.Settings.GetSettings(model);
                settings.StandingHeightOffset = .17f; settings.SittingHeightOffset = -.08f;
                AvatarHeightOffsets.Save(model);
                Check(File.Exists(txt), "Saving offsets did not create the optional TXT file");
                File.AppendAllText(txt, "// preserved comment\nModelScale=1.25\n");
                settings.StandingHeightOffset = -.23f;
                AvatarHeightOffsets.Save(model); ValheimVRM.Settings.AddSettingsFromFile(model, false);
                Check(settings.StandingHeightOffset == -.23f && settings.SittingHeightOffset == -.08f && settings.ModelScale == 1.25f &&
                    File.ReadAllText(txt).Contains("// preserved comment"), "TXT replacement changed unrelated settings/comments");
            }
            finally { ValheimVRM.Settings.RemoveSettings(model); if (File.Exists(txt)) File.Delete(txt); }
            Check(Directory.GetFiles(directory, "*.tmp").Length == 0 && Directory.GetFiles(directory, "*.bak").Length == 0,
                "Successful/rejected engine saves leaked temporary or backup files");
            report.Add("Configuration persistence: production Mono recovers injected Windows 1175 with Unicode native move fallback; readonly/held-open JSON retains applied VRM and native session selections with accurate warnings; released access saves successfully; optional TXT creation/update preserves authored parameters and comments");
        }
        finally { Player.m_localPlayer = previousLocal; catalogField.SetValue(picker, previousCatalog); }
    }

    static bool IsSelectionSaveWarning(string message) => !message.Contains("Avatar switch failed") &&
        (message.Contains("selection could not be saved") || message.Contains("选择记录保存失败"));

    static IEnumerator WaitConfigurationSwitch(OutfitSwitcher picker)
    {
        float deadline = Time.realtimeSinceStartup + 120;
        while (picker.IsBusy)
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Configuration switch timed out");
            yield return null;
        }
    }
}
