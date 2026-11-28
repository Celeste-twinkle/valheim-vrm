using System;
using System.Collections;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;

public sealed partial class AvatarSyncEngineTests
{
    void VrmOnlyRestart(GameObject prefab, string[] names)
    {
        var player = MakePlayer(prefab, 505, 5005, 5);
        Check(AvatarPhysics.Weight == .25f, "Restart lost physics weight");
        Check(!AvatarRendering.Current.SceneLighting && !AvatarRendering.Current.ReceiveShadows && AvatarRendering.Current.Bloom,
            "Restart lost rendering options");
        Check(OutfitSwitcher.ResolveModelName(player.GetPlayerName()) == names.Last(), "Restart lost the selected model");
        Check(!Directory.GetFiles(ValheimVRM.Settings.ConfigDir, "*.txt").Any(), "Mod unexpectedly generated optional TXT files");
        report.Add("Fresh process restart restored selection, physics weight and rendering options from BepInEx/config/ValheimVRM; no manual TXT files needed or generated");
    }

    IEnumerator VrmOnlyLibrary(GameObject prefab, string[] names)
    {
        var directory = ValheimVRM.Settings.ValheimVRMDir;
        var before = Directory.GetFileSystemEntries(directory).OrderBy(p => p).ToArray();
        Check(before.Length > 0 && before.All(p => Path.GetExtension(p).Equals(".vrm", StringComparison.OrdinalIgnoreCase)),
            "Probe requires a library containing only VRM files");
        Check(!Directory.Exists(ValheimVRM.Settings.ConfigDir), "Probe requires no initial configuration");
        var hashes = before.Select(p => Hash(File.ReadAllBytes(p))).ToArray();
        var player = MakePlayer(prefab, 404, 4004, 4);
        var oldLocal = Player.m_localPlayer;
        var sync = AvatarSyncClient.Instance;
        sync.enabled = false;
        Player.m_localPlayer = player;
        try
        {
            var picker = OutfitSwitcher.Instance;
            foreach (var name in names)
            {
                Check(picker.RequestSwitch(name), "Local picker rejected a VRM-only model");
                float deadline = Time.realtimeSinceStartup + 120;
                while (picker.IsBusy)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new Exception("Local VRM-only switch timeout");
                    yield return null;
                }
                Check(picker.LastError == "" && VrmManager.PlayerToName[player] == name, "Local VRM-only import failed: " + picker.LastError);
                var settings = ValheimVRM.Settings.GetSettings(name);
                Check(settings.ModelScale == 1 && settings.ModelBrightness == 1 && settings.UseMToonShader && !settings.EnablePlayerFade,
                    "Missing TXT did not use the supported default appearance");
                var root = VrmManager.PlayerToVrmInstance[player];
                Check(root.GetComponent<Animator>()?.isHuman == true, "VRM-only import lost the humanoid rig");
                Check(root.GetComponent<AvatarPhysicsWeight>() != null, "VRM-only import lost physics weight support");
            }
            var reload = new AvatarCatalog(directory, ValheimVRM.Settings.ConfigDir);
            reload.Refresh(); reload.LoadSelections();
            Check(reload.Resolve(player.GetPlayerName()) == names.Last(), "Local choice did not persist outside model library");
            AvatarPhysics.Preview(.25f); AvatarPhysics.Save(); AvatarPhysics.Preview(.9f); AvatarPhysics.Initialize();
            Check(AvatarPhysics.Weight == .25f, "Physics preference failed to persist without model-side JSON");
            AvatarRendering.Set(false, false, true);
            var render = File.ReadAllText(Path.Combine(ValheimVRM.Settings.ConfigDir, "rendering_options.json"));
            Check(render.Contains("false") && render.Contains("true") && !AvatarRendering.Current.SceneLighting && AvatarRendering.Current.Bloom,
                "Rendering preference failed to persist outside model library");
            Check(before.SequenceEqual(Directory.GetFileSystemEntries(directory).OrderBy(p => p)), "Runtime wrote non-VRM files into model library");
            Check(hashes.SequenceEqual(before.Select(p => Hash(File.ReadAllBytes(p)))), "Runtime modified source VRM files");
            report.Add("VRM-only library with no initial configuration: real local picker switched two humanoid models, built-in defaults and physics attached; selection/physics/rendering persisted under BepInEx/config; model directory and every VRM hash unchanged");
        }
        finally { Player.m_localPlayer = oldLocal; }
    }
}
