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
    IEnumerator CalibrationInstanceTests(AvatarSyncClient sync, AvatarSyncRegistry registry, Player a, Player b, string model, string hash)
    {
        var players = Player.GetAllPlayers(); players.Add(a); players.Add(b);
        var preferences = Directory.GetFiles(ValheimVRM.Settings.ConfigDir, "*", SearchOption.AllDirectories)
            .ToDictionary(p => p, File.ReadAllBytes);
        var localSettings = ValheimVRM.Settings.GetSettings(model);
        var localProfile = AvatarCalibrationOptions.Current.Get(model);
        string localControls = AvatarCalibrationBinding.Capture(model);
        string controlA = CalibrationRelayProbe.Data(.12f), controlB = CalibrationRelayProbe.Data(-.2f);
        try
        {
            registry.Set(101, 1001, 1, model, hash, 1.4f, controlA);
            registry.Set(202, 2002, 2, model, hash, 2f, controlB); SetState(sync, registry);
            yield return PumpRemote(sync); yield return PumpRemote(sync);
            var rootA = VrmManager.PlayerToVrmInstance[a]; var rootB = VrmManager.PlayerToVrmInstance[b];
            var boundA = rootA.GetComponent<AvatarCalibrationBinding>(); var boundB = rootB.GetComponent<AvatarCalibrationBinding>();
            Check(boundA.Encoded == controlA && boundB.Encoded == controlB, "Remote controls crossed players");
            Check(!ReferenceEquals(boundA.Settings, localSettings) && !ReferenceEquals(boundA.Profile, localProfile) &&
                !ReferenceEquals(boundA.Profile, boundB.Profile), "Remote calibration aliases personal/shared settings");
            Check(boundA.Settings.StandingHeightOffset == .12f && boundB.Settings.StandingHeightOffset == -.2f &&
                boundA.Settings.SittingHeightOffset == -.12f && boundB.Settings.SittingHeightOffset == .2f, "Standing/sitting settings were not received");
            var field = AccessTools.Field(typeof(VRMAnimationSync), "profile");
            Check(ReferenceEquals(field.GetValue(rootA.GetComponent<VRMAnimationSync>()), boundA.Profile), "Animation retargeter ignored received profile");
            var right = boundA.Profile.Right;
            for (int i = 0; i < 12; i++)
            {
                float value = i % 2 == 0 ? .3f : -.1f;
                string encoded = CalibrationRelayProbe.Data(value);
                registry.Set(101, 1001, 1, model, hash, 1.4f, encoded); SetState(sync, registry);
                yield return PumpRemote(sync);
                Check(VrmManager.PlayerToVrmInstance[a] == rootA, "Manual adjustment unnecessarily reloaded remote model");
                Check(boundA.Encoded == encoded && ReferenceEquals(right, boundA.Profile.Right), "Held-item group references were replaced");
                Check(Mathf.Abs(right.Scale - (1 - value)) < .000001f && right.Position.Value == new Vector3(0, value, -value), "Item scale/XYZ was lost");
                Check(Mathf.Abs(boundA.Profile.Left.Scale - (1 + value)) < .000001f && Mathf.Abs(boundA.Profile.TwoHanded.Scale - (1 + value * 2)) < .000001f, "Left/two-handed scale was lost");
                Check(boundA.Profile.Get("Base Layer.Test 0000") == new Vector3(value, -value, value), "Animation XYZ was lost");
                Check(Mathf.Abs(boundA.Profile.Back.Scale - (1 + value)) < .000001f &&
                    boundA.Profile.Back.Position.Value == new Vector3(-value, value, value), "Back equipment settings were not received");
                Check(Mathf.Abs(rootA.GetComponent<AvatarPhysicsWeight>().SynchronizedWeight - (.5f + value)) < .000001f, "Physics weight was not applied");
                Check(VrmManager.PlayerToVrmInstance[b] == rootB && boundB.Encoded == controlB, "A's controls changed B");
                Check(AvatarCalibrationBinding.Capture(model) == localControls, "Remote controls overwrote observer preferences");
            }
            yield return VrmManager.VrmDic[model].SetToPlayer(a);
            Check(VrmManager.PlayerToVrmInstance[a].GetComponent<AvatarCalibrationBinding>().Encoded == CalibrationRelayProbe.Data(-.1f),
                "External reattachment lost received calibration");
            Check(preferences.All(p => File.Exists(p.Key) && File.ReadAllBytes(p.Key).SequenceEqual(p.Value)) &&
                Directory.GetFiles(ValheimVRM.Settings.ConfigDir, "*", SearchOption.AllDirectories).Length == preferences.Count,
                "Received calibration wrote personal config files");
            OwnerControlRequests(sync, a, model);
            registry.Set(101, 1001, 1, model, hash, 2f); registry.Set(202, 2002, 2, model, hash, 2f);
            SetState(sync, registry); yield return PumpRemote(sync); yield return PumpRemote(sync);
            Check(VrmManager.PlayerToVrmInstance[b].GetComponent<AvatarCalibrationBinding>().Encoded == AvatarCalibrationCodec.Default,
                "Sender without calibration inherited observer controls");
            report.Add("Actual same-model player instances: independent height, standing/sitting, action XYZ, left/right/two-handed/back item scales/XYZ and spring weight; 12 in-place updates retain objects/references, external refresh retains controls, observer config files unchanged; sender fallback resets defaults");
        }
        finally { players.Remove(a); players.Remove(b); }
    }
    void OwnerControlRequests(AvatarSyncClient sync, Player owner, string model)
    {
        var local = Player.m_localPlayer; var settings = ValheimVRM.Settings.GetSettings(model);
        var profile = AvatarCalibrationOptions.Current.Get(model); string key = "Base Layer.Calibration Sender Probe";
        var oldOffset = profile.Get(key); float oldStanding = settings.StandingHeightOffset, oldSitting = settings.SittingHeightOffset;
        float oldWeight = AvatarPhysics.Weight, leftScale = profile.Left.Scale, rightScale = profile.Right.Scale, bothScale = profile.TwoHanded.Scale;
        var leftOffset = profile.Left.Position.Value; var rightOffset = profile.Right.Position.Value; var bothOffset = profile.TwoHanded.Position.Value;
        float backScale = profile.Back.Scale; var backOffset = profile.Back.Position.Value;
        var saved = new Dictionary<string,object>();
        foreach (string name in new[]{"server","hostPlugin","lastSent","requestSequence","<CalibrationSync>k__BackingField","<HeightSync>k__BackingField","<SequencedRequests>k__BackingField"})
            saved[name] = AccessTools.Field(typeof(AvatarSyncClient),name).GetValue(sync);
        var left = new MemorySocket(); var right = new MemorySocket(); left.Other = right; right.Other = left;
        var client = new ZRpc(left); var relay = new ZRpc(right); var packets = new List<string>(); var sequences = new List<long>();
        relay.Register<ZPackage>(AvatarSyncWire.Select, (rpc,p) => {
            Check(AvatarSyncWire.ReadSelection(p,out var on,out var name,out _,out var seq,out var height,out var hasHeight,out var encoded) &&
                on && name == model && hasHeight && height == 1.4f && encoded != null,"Owner emitted incomplete visual state");
            packets.Add(encoded); sequences.Add(seq);
        });
        Action send = () => { AccessTools.Method(typeof(AvatarSyncClient),"SendSelection").Invoke(sync,null); relay.Update(.01f); };
        try
        {
            Player.m_localPlayer = owner;
            AccessTools.Field(typeof(AvatarSyncClient),"server").SetValue(sync,client);
            AccessTools.Field(typeof(AvatarSyncClient),"hostPlugin").SetValue(sync,null);
            AccessTools.Field(typeof(AvatarSyncClient),"lastSent").SetValue(sync,null);
            AccessTools.Field(typeof(AvatarSyncClient),"requestSequence").SetValue(sync,0L);
            foreach (string name in new[]{"CalibrationSync","HeightSync","SequencedRequests"})
                AccessTools.Field(typeof(AvatarSyncClient),"<"+name+">k__BackingField").SetValue(sync,true);
            send(); send(); Check(packets.Count == 1,"Unchanged controls sent repeatedly");
            settings.StandingHeightOffset = .19f; send();
            settings.SittingHeightOffset = -.17f; send();
            profile.Set(key,new Vector3(.1f,.2f,.3f)); send();
            profile.Left.Scale = 1.23f; profile.Left.Position.Value = new Vector3(.1f,0,0); send();
            profile.Right.Scale = .79f; profile.Right.Position.Value = new Vector3(0,.2f,0); send();
            profile.TwoHanded.Scale = 1.31f; profile.TwoHanded.Position.Value = new Vector3(0,0,.3f); send();
            profile.Back.Scale = 1.42f; profile.Back.Position.Value = new Vector3(.1f,-.2f,.3f); send();
            AvatarPhysics.Preview(.21f); send(); send();
            Check(packets.Count == 9 && sequences.SequenceEqual(Enumerable.Range(1,9).Select(i=>(long)i)), "One of the slider groups did not trigger a sequenced request");
            Check(packets.Last() == AvatarCalibrationBinding.Capture(model),"Owner request omitted current controls");
            report.Add("Production owner SendSelection: standing, sitting, animation XYZ, left/right/two-hand/back size/XYZ and physics edits emit nine complete calibration requests with strictly increasing sequence; unchanged state emits nothing");
        }
        finally
        {
            Player.m_localPlayer=local; settings.StandingHeightOffset=oldStanding; settings.SittingHeightOffset=oldSitting;
            profile.Set(key,oldOffset); profile.Left.Scale=leftScale; profile.Right.Scale=rightScale; profile.TwoHanded.Scale=bothScale;
            profile.Left.Position.Value=leftOffset; profile.Right.Position.Value=rightOffset; profile.TwoHanded.Position.Value=bothOffset;
            profile.Back.Scale=backScale; profile.Back.Position.Value=backOffset;
            AvatarPhysics.Preview(oldWeight);
            foreach(var pair in saved) AccessTools.Field(typeof(AvatarSyncClient),pair.Key).SetValue(sync,pair.Value);
            client.Dispose(); relay.Dispose();
        }
    }
}
