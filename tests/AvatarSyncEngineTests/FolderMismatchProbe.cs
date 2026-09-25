using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using ValheimVRM.Sync;

public sealed partial class AvatarSyncEngineTests
{
    static int mismatchImportCalls;
    static void CountMismatchImport() { mismatchImportCalls++; }

    IEnumerator FolderMismatchTests(GameObject prefab, AvatarSyncClient sync, AvatarSyncRegistry registry,
        Player a, Player b, string[] names, string[] hashes)
    {
        var picker = OutfitSwitcher.Instance;
        var originalCatalog = picker.Catalog;
        var catalogField = AccessTools.Field(typeof(OutfitSwitcher), "<Catalog>k__BackingField");
        var applied = (Dictionary<Player, AvatarSelection>)AccessTools.Field(typeof(AvatarSyncClient), "applied").GetValue(sync);
        var failed = (Dictionary<Player, AvatarSelection>)AccessTools.Field(typeof(AvatarSyncClient), "failed").GetValue(sync);
        var originalA = VrmManager.PlayerToVrmInstance[a]; var originalB = VrmManager.PlayerToVrmInstance[b];
        var c = MakePlayer(prefab, 303, 3003, 3);
        var vanillaRenderers = c.GetVisual().GetComponentsInChildren<Renderer>(true);
        var vanillaEnabled = vanillaRenderers.Select(r => r.enabled).ToArray();
        var vanillaOff = vanillaRenderers.Select(r => r.forceRenderingOff).ToArray();
        var players = Player.GetAllPlayers();
        Check(!players.Contains(a) && !players.Contains(b) && !players.Contains(c), "Fixtures unexpectedly registered");
        players.Add(a); players.Add(b); players.Add(c);
        foreach (var selection in registry.Snapshot()) applied[selection.Peer == 101 ? a : b] = selection;
        string name = "__sync_folder_probe_" + Guid.NewGuid().ToString("N");
        string path = Path.Combine(ValheimVRM.Settings.ValheimVRMDir, name + ".vrm");
        var patch = new Harmony("valheimvrm.tests.folder-mismatch");
        patch.Patch(AccessTools.Method(typeof(ValheimVRM.VRM), "ImportVisualAsync",
            new[] { typeof(byte[]), typeof(string), typeof(float), typeof(Action<GameObject>) }),
            prefix: new HarmonyMethod(typeof(AvatarSyncEngineTests), nameof(CountMismatchImport)));
        mismatchImportCalls = 0;
        try
        {
            registry.Set(101, 1001, 1, name, hashes[0]);
            registry.Set(303, 3003, 3, name, hashes[0]); SetState(sync, registry);
            yield return PumpRemote(sync);
            Check(failed.Count == 2 && sync.LastError.Contains("Missing local VRM"), "Missing file was not isolated per player");
            string missingNotice = sync.LastError;
            for (int i = 0; i < 20; i++) yield return PumpRemote(sync);
            Check(sync.LastError == missingNotice && failed.Count == 2, "Missing selection repeatedly retried");
            Check(VrmManager.PlayerToVrmInstance[a] == originalA && VrmManager.PlayerToVrmInstance[b] == originalB,
                "Missing A model changed an existing avatar");
            Check(!VrmManager.PlayerToVrmInstance.ContainsKey(c) && c.GetVisual().GetComponentsInChildren<Renderer>(true)
                .Select((r, i) => r.enabled == vanillaEnabled[i] && r.forceRenderingOff == vanillaOff[i]).All(x => x),
                "First missing model hid the vanilla character");

            // An empty catalog also represents a missing ValheimVRM directory.
            var empty = new AvatarCatalog(Path.Combine(output, "absent-avatar-directory"), ValheimVRM.Settings.ConfigDir); empty.Refresh();
            catalogField.SetValue(picker, empty); sync.RetryMissing();
            registry.Set(101, 1001, 1, names[0], hashes[0]); SetState(sync, registry);
            yield return PumpRemote(sync);
            Check(empty.Names.Length == 0 && failed.ContainsKey(a) && !picker.IsBusy, "Empty folder blocked the picker");
            Check(VrmManager.PlayerToVrmInstance[a] == originalA && VrmManager.PlayerToVrmInstance[b] == originalB,
                "Empty folder changed a valid avatar");
            catalogField.SetValue(picker, originalCatalog);

            // Even deliberately invalid VRM bytes must be rejected by the hash
            // before invoking UniVRM or loading the model's settings.
            File.WriteAllBytes(path, new byte[] { 0, 1, 2, 3, 4 }); picker.RefreshModels();
            registry.Set(101, 1001, 1, name, hashes[0]); SetState(sync, registry);
            yield return PumpRemote(sync); yield return PumpRemote(sync);
            Check(failed.ContainsKey(a) && failed.ContainsKey(c) && sync.LastError.Contains("differs from the sender"),
                "Uncached differing contents did not fall back");
            Check(!VrmManager.VrmDic.ContainsKey(name) && !ValheimVRM.Settings.ContainsSettings(name),
                "Mismatched bytes polluted the model/settings cache");
            Check(mismatchImportCalls == 0, "Mismatched bytes reached the importer");

            // An exclusively locked file passes existence checks but is unreadable.
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                sync.RetryMissing(); yield return PumpRemote(sync); yield return PumpRemote(sync);
                Check(failed.ContainsKey(a) && sync.LastError.Contains("Cannot read local VRM") && !picker.IsBusy,
                    "Unreadable file escaped containment or left the picker busy");
            }
            File.Delete(path); picker.RefreshModels();
            yield return PumpRemote(sync);
            Check(failed.ContainsKey(a) && sync.LastError.Contains("Missing local VRM"), "Deleted file was still imported");

            // names[0] may have been correctly released once no player selected
            // it. Exercise a guaranteed live cache entry instead of depending
            // on the former 15-second idle cache.
            registry.Set(101, 1001, 1, names[1], hashes[0]); SetState(sync, registry);
            yield return PumpRemote(sync);
            Check(failed.ContainsKey(a) && sync.LastError.Contains("Cached VRM differs"), "Different cached contents were attached");
            Check(VrmManager.PlayerToVrmInstance[a] == originalA && VrmManager.PlayerToVrmInstance[b] == originalB,
                "Cache mismatch modified A or B");
            Check(mismatchImportCalls == 0 && !picker.IsBusy && sync.Connected, "Mismatch affected connection or import state");

            // A healthy selection on another player must still proceed, and a
            // corrected, previously unimported file must load after Refresh.
            registry.Set(202, 2002, 2, names[0], hashes[0]); SetState(sync, registry);
            yield return PumpRemote(sync);
            Check(VrmManager.PlayerToName[b] == names[0] && VrmManager.PlayerToVrmInstance[a] == originalA,
                "A's mismatch prevented B from switching independently");
            int importsBeforeRecovery = mismatchImportCalls;
            File.Copy(Path.Combine(ValheimVRM.Settings.ValheimVRMDir, names[0] + ".vrm"), path);
            registry.Set(101, 1001, 1, name, hashes[0]); SetState(sync, registry); picker.RefreshModels();
            yield return PumpRemote(sync); yield return PumpRemote(sync);
            Check(VrmManager.PlayerToName[a] == name && VrmManager.PlayerToName[c] == name,
                "Adding the matching file and refreshing did not recover");
            Check(VrmManager.PlayerToVrmInstance[a] != VrmManager.PlayerToVrmInstance[c] &&
                mismatchImportCalls == importsBeforeRecovery + 1,
                "Recovered model shared player instances or imported repeatedly");
            report.Add("Folder mismatch: missing/empty/deleted/unreadable files, differing uncached/cached bytes, vanilla/existing appearance retention, no importer/cache pollution, bounded retries and independent healthy player switches passed");
            report.Add("Recovery: replacing an unimported differing file with matching bytes and refreshing attached independent avatars without reconnecting; no local selection was changed");

            // Restore the expected fixture state for the remaining respawn tests.
            registry.Remove(303);
            registry.Set(101, 1001, 1, names[1], hashes[1]); registry.Set(202, 2002, 2, names[1], hashes[1]); SetState(sync, registry);
            yield return PumpRemote(sync); yield return PumpRemote(sync); yield return PumpRemote(sync);
            Check(!VrmManager.PlayerToVrmInstance.ContainsKey(c), "Withdrawing selection did not restore vanilla");
        }
        finally
        {
            patch.UnpatchSelf();
            catalogField.SetValue(picker, originalCatalog);
            players.Remove(a); players.Remove(b); players.Remove(c);
            applied.Remove(c); failed.Remove(c);
            if (File.Exists(path)) File.Delete(path); // Unique file created only by this isolated probe.
            picker.RefreshModels();
        }
    }

    IEnumerator PumpRemote(AvatarSyncClient sync)
    {
        AccessTools.Method(typeof(AvatarSyncClient), "ApplyRemotePlayers").Invoke(sync, null);
        float deadline = Time.realtimeSinceStartup + 120;
        while (OutfitSwitcher.Instance.IsBusy)
        {
            if (Time.realtimeSinceStartup > deadline) throw new Exception("Remote mismatch/recovery timeout");
            yield return null;
        }
    }
}
