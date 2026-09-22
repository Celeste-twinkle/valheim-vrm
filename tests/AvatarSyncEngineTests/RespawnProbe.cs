using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using ValheimVRM.Sync;
using Object = UnityEngine.Object;

public sealed partial class AvatarSyncEngineTests
{
    IEnumerator RespawnTests(GameObject prefab, AvatarSyncClient sync, string model, string hash)
    {
        var picker = OutfitSwitcher.Instance;
        var local = Player.m_localPlayer;
        var heights = picker.Heights; var catalog = picker.Catalog;
        var gameField = AccessTools.Field(typeof(Game), "<instance>k__BackingField"); var oldGame = gameField.GetValue(null);
        var managerField = AccessTools.Field(typeof(ZDOMan), "s_instance"); var oldManager = managerField.GetValue(null);
        var netField = AccessTools.Field(typeof(ZNet), "m_instance"); var oldNet = netField.GetValue(null);
        var fixture = new GameObject("Respawn environment"); fixture.SetActive(false);
        var game = fixture.AddComponent<Game>();
        var net = fixture.AddComponent<ZNet>();
        var dialogs = new GameObject("Inactive network dialogs", typeof(RectTransform)); dialogs.SetActive(false);
        net.m_passwordDialog = net.m_connectingDialog = (RectTransform)dialogs.transform;
        var profile = new PlayerProfile(null, FileHelpers.FileSource.Local); profile.SetName("Respawn owner");
        AccessTools.Field(typeof(Game), "m_playerProfile").SetValue(game, profile);
        var manager = (ZDOMan)FormatterServices.GetUninitializedObject(typeof(ZDOMan));
        AccessTools.Field(typeof(ZDOMan), "m_sessionID").SetValue(manager, 901L);
        var testHeights = new AvatarHeightOptions(Path.Combine(output, "respawn"));
        var testCatalog = new AvatarCatalog(ValheimVRM.Settings.ValheimVRMDir, Path.Combine(output, "respawn")); testCatalog.Refresh();
        testCatalog.Select("Respawn owner", model); testHeights.Set("Respawn owner", 1.4f);
        var settings = ValheimVRM.Settings.GetSettings(model); var controls = AvatarCalibrationOptions.Current.Get(model);
        string saved = AvatarCalibrationBinding.Capture(model);
        var backup = new GameObject("Calibration backup").AddComponent<AvatarCalibrationBinding>(); backup.Initialize(settings, saved);
        var awake = AccessTools.Method(typeof(MainPlugin).Assembly.GetType("ValheimVRM.Patch_Player_Awake"), "Postfix");
        var players = Player.GetAllPlayers(); var created = new List<Player>();
        var prefs = Directory.GetFiles(ValheimVRM.Settings.ConfigDir, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        var registry = new AvatarSyncRegistry();
        var world = UnityEngine.SceneManagement.SceneManager.CreateScene("RespawnTestWorld");
        var savedClient = new Dictionary<string, object>();
        foreach (string key in new[] { "server", "hostPlugin", "lastSent", "requestSequence", "<HeightSync>k__BackingField", "<CalibrationSync>k__BackingField", "<SequencedRequests>k__BackingField" })
            savedClient[key] = AccessTools.Field(typeof(AvatarSyncClient), key).GetValue(sync);
        var left = new MemorySocket(); var right = new MemorySocket(); left.Other = right; right.Other = left;
        var client = new ZRpc(left); var relay = new ZRpc(right); var packets = new List<string>();
        relay.Register<ZPackage>(AvatarSyncWire.Select, (rpc, p) => {
            Check(AvatarSyncWire.ReadSelection(p, out var on, out var name, out _, out _, out var height, out var withHeight, out var encoded) &&
                on && name == model && withHeight && height == 1.4f && encoded != null, "Respawn emitted a default/incomplete owner selection");
            packets.Add(encoded);
        });
        Action send = () => { AccessTools.Method(typeof(AvatarSyncClient), "SendSelection").Invoke(sync, null); relay.Update(.01f); };
        // Existing suite advanced the revision; use an independent authoritative sequence.
        AccessTools.Field(typeof(AvatarSyncClient), "revision").SetValue(sync, -1L);
        try
        {
            gameField.SetValue(null, game); managerField.SetValue(null, manager); netField.SetValue(null, net);
            AccessTools.Field(typeof(OutfitSwitcher), "<Heights>k__BackingField").SetValue(picker, testHeights);
            AccessTools.Field(typeof(OutfitSwitcher), "<Catalog>k__BackingField").SetValue(picker, testCatalog);
            AccessTools.Field(typeof(AvatarSyncClient), "server").SetValue(sync, client);
            AccessTools.Field(typeof(AvatarSyncClient), "hostPlugin").SetValue(sync, null);
            AccessTools.Field(typeof(AvatarSyncClient), "lastSent").SetValue(sync, null);
            AccessTools.Field(typeof(AvatarSyncClient), "requestSequence").SetValue(sync, 0L);
            foreach (string key in new[] { "HeightSync", "CalibrationSync", "SequencedRequests" })
                AccessTools.Field(typeof(AvatarSyncClient), "<" + key + ">k__BackingField").SetValue(sync, true);
            settings.StandingHeightOffset = .27f; settings.SittingHeightOffset = -.31f; settings.ModelOffsetY = .08f;
            var native = prefab.GetComponentInChildren<Animator>(true);
            var entries = new AvatarAnimationCatalog(native).Entries;
            foreach (var entry in entries) { controls.Set(entry.Path, new Vector3(.17f, -.23f, .36f)); foreach (var clip in entry.Clips) controls.Set(entry.ClipKey(clip), new Vector3(-.11f, .14f, .21f)); }
            int group = 0;
            foreach (var item in new[] { controls.Left, controls.Right, controls.TwoHanded, controls.Back })
            { item.Scale = .7f + .2f * group++; item.Position.Value = new Vector3(.18f * group, -.12f * group, .09f * group); }
            string expected = AvatarCalibrationBinding.Capture(model);
            string remoteA = CalibrationRelayProbe.Data(.24f, 300), remoteB = CalibrationRelayProbe.Data(-.18f, 300);
            Player previousOwner = null, previousRemote = null;
            var observerB = MakePlayer(prefab, 903, 9030, 1); created.Add(observerB); players.Add(observerB);
            registry.Set(903, 9030, 1, model, hash, 2.2f, remoteB); SetState(sync, registry); yield return PumpRemote(sync);
            var rootB = VrmManager.PlayerToVrmInstance[observerB];
            for (uint life = 1; life <= 4; life++)
            {
                if (previousOwner != null)
                {
                    ZDOExtraData.Set(previousOwner.GetZDOID(), ZDOVars.s_dead, 1);
                    ZDOExtraData.Set(previousRemote.GetZDOID(), ZDOVars.s_dead, 1);
                    var corpseVisual = VrmManager.PlayerToVrmInstance[previousRemote];
                    var corpseSettings = corpseVisual.GetComponent<AvatarCalibrationBinding>().Settings;
                    var corpse = Object.Instantiate(previousRemote.GetVisual(), fixture.transform);
                    var ragdoll = corpse.AddComponent<Ragdoll>();
                    var ragdollHook = AccessTools.Method(typeof(MainPlugin).Assembly.GetType("ValheimVRM.Patch_Humanoid_OnRagdollCreated"), "Postfix");
                    ragdollHook.Invoke(null, new object[] { previousRemote, ragdoll });
                    Check(!VrmManager.PlayerToVrmInstance.ContainsKey(previousRemote) && previousRemote.GetComponent<VrmController>().visual == null,
                        "Corpse remains registered as live avatar");
                    Check(ReferenceEquals(AccessTools.Field(typeof(VRMAnimationSync), "settings").GetValue(corpseVisual.GetComponent<VRMAnimationSync>()), corpseSettings),
                        "Remote corpse inherited observer settings");
                    Check(corpseVisual.GetComponent<AvatarScale>().TargetHeight == 1.4f, "Corpse lost received height");
                    send();
                }
                // Exercise the cold-import path as well, retaining the old template's
                // live leases so B and the corpses keep their own resource generation.
                if (life == 4) { VrmManager.VrmDic.Remove(model); VrmManager.VrmHashes.Remove(model); }
                // Cached attachment starts inside Awake, before SpawnPlayer calls SetLocalPlayer/LoadPlayerData.
                var owner = MakePlayer(prefab, 901, 9010, life); created.Add(owner);
                AccessTools.Property(typeof(ZDO), "Owner").SetValue(owner.GetComponent<ZNetView>().GetZDO(), true, null);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner.transform.root.gameObject, world);
                Player.m_localPlayer = previousOwner;
                awake.Invoke(null, new object[] { owner, owner.GetComponent<ZNetView>() });
                File.AppendAllText(Path.Combine(output, "respawn-stages.txt"), "Awake " + life + " owner=" + owner.GetComponent<ZNetView>().IsOwner() + " model=" + (VrmManager.PlayerToName.TryGetValue(owner, out var selected) ? selected : "none") + "\n");
                ZDOExtraData.Set(owner.GetZDOID(), ZDOVars.s_playerID, 901L);
                ZDOExtraData.Set(owner.GetZDOID(), ZDOVars.s_playerName, "Respawn owner"); Player.m_localPlayer = owner;
                float deadline = Time.realtimeSinceStartup + 120;
                while (!VrmManager.PlayerToVrmInstance.TryGetValue(owner, out var v) || v.GetComponent<VRMAnimationSync>() == null)
                { Check(Time.realtimeSinceStartup < deadline, "Respawn attachment timeout"); yield return null; }
                var visual = VrmManager.PlayerToVrmInstance[owner];
                Check(visual.GetComponent<AvatarScale>().TargetHeight == 1.4f, "Cached Awake respawn reset local height before SetLocalPlayer");
                Check(VrmManager.PlayerToName[owner] == model, "Respawn lost selected model");
                Check(ReferenceEquals(AccessTools.Field(typeof(VRMAnimationSync), "profile").GetValue(visual.GetComponent<VRMAnimationSync>()), controls), "Local respawn lost all animation controls");
                Check(ReferenceEquals(AccessTools.Field(typeof(VRMAnimationSync), "settings").GetValue(visual.GetComponent<VRMAnimationSync>()), settings), "Local respawn lost posture settings");
                Check(ReferenceEquals(AccessTools.Field(typeof(VRMEquipmentSync), "profile").GetValue(owner.GetComponent<VRMEquipmentSync>()), controls), "Local respawn equipment lost controls");
                Check(Mathf.Abs((float)AccessTools.Field(typeof(VRMEquipmentSync), "heightScale").GetValue(owner.GetComponent<VRMEquipmentSync>()) - .7f) < .0001f, "Respawn equipment lost height scaling");
                Check(AvatarCalibrationBinding.Capture(model) == expected, "Respawn changed personal controls");
                send(); Check(packets.Count == 1 && packets[0] == expected, "Respawn published changed/default controls or duplicate requests");
                var remote = MakePlayer(prefab, 902, 9020, life); created.Add(remote); players.Add(remote);
                registry.Set(902, 9020, life, model, hash, 1.4f, remoteA); SetState(sync, registry); yield return PumpRemote(sync);
                var rv = VrmManager.PlayerToVrmInstance[remote]; var binding = rv.GetComponent<AvatarCalibrationBinding>();
                Check(rv.GetComponent<AvatarScale>().TargetHeight == 1.4f && binding.Encoded == remoteA, "Remote respawn lost height/calibration");
                Check(ReferenceEquals(AccessTools.Field(typeof(VRMAnimationSync), "profile").GetValue(rv.GetComponent<VRMAnimationSync>()), binding.Profile), "Remote animation consumer lost binding");
                Check(ReferenceEquals(AccessTools.Field(typeof(VRMEquipmentSync), "profile").GetValue(remote.GetComponent<VRMEquipmentSync>()), binding.Profile), "Remote equipment lost controls");
                Check(VrmManager.PlayerToVrmInstance[observerB] == rootB && rootB.GetComponent<AvatarCalibrationBinding>().Encoded == remoteB, "Respawn changed independent same-model B");
                // A recreated/stale binding must not pass the unchanged-selection fast path.
                binding.Apply(AvatarCalibrationCodec.Default); yield return PumpRemote(sync);
                Check(binding.Encoded == remoteA, "Unchanged remote selection failed to repair reset calibration");
                if (life == 4)
                {
                    Object.DestroyImmediate(binding); yield return PumpRemote(sync);
                    Check(VrmManager.PlayerToVrmInstance[remote].GetComponent<AvatarCalibrationBinding>().Encoded == remoteA,
                        "Missing remote binding was not rebuilt from the retained selection");
                }
                previousOwner = owner; previousRemote = remote;
            }
            Check(prefs.All(p => File.ReadAllBytes(p.Key).SequenceEqual(p.Value)), "Respawn rewrote personal config");
            report.Add("Respawn: three cached and one cold Awake -> SetLocalPlayer/identity lifecycles retain local 1.4 m, complete animation/clip profile, standing/sitting and all four equipment groups; remote reincarnations retain 300 animation controls and independent 1.4/2.2 m same-model players; unchanged snapshots repair reset/missing bindings; corpses retain instance settings; owner RPC never publishes defaults; personal files unchanged");
        }
        finally
        {
            // Restore shared in-memory preferences without writing the user's files.
            settings.StandingHeightOffset = backup.Settings.StandingHeightOffset; settings.SittingHeightOffset = backup.Settings.SittingHeightOffset; settings.ModelOffsetY = backup.Settings.ModelOffsetY;
            controls.Animations.Clear(); foreach (var pair in backup.Profile.Animations) controls.Animations[pair.Key] = pair.Value;
            var from = new[] { backup.Profile.Left, backup.Profile.Right, backup.Profile.TwoHanded, backup.Profile.Back };
            var to = new[] { controls.Left, controls.Right, controls.TwoHanded, controls.Back };
            for (int i = 0; i < from.Length; i++) { to[i].Scale = from[i].Scale; to[i].Position.Value = from[i].Position.Value; }
            foreach (var player in created) players.Remove(player);
            Player.m_localPlayer = local; gameField.SetValue(null, oldGame); managerField.SetValue(null, oldManager); netField.SetValue(null, oldNet);
            AccessTools.Field(typeof(OutfitSwitcher), "<Heights>k__BackingField").SetValue(picker, heights);
            AccessTools.Field(typeof(OutfitSwitcher), "<Catalog>k__BackingField").SetValue(picker, catalog);
            foreach (var pair in savedClient) AccessTools.Field(typeof(AvatarSyncClient), pair.Key).SetValue(sync, pair.Value);
            client.Dispose(); relay.Dispose();
        }
    }
}
