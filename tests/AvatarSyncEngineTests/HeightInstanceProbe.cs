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
    IEnumerator HeightInstanceTests(AvatarSyncClient sync, AvatarSyncRegistry registry, Player a, Player b, string model, string hash)
    {
        var template = VrmManager.VrmDic[model].VisualModel;
        var templateScale = template.transform.localScale;
        var rootB = VrmManager.PlayerToVrmInstance[b];
        var settingsScale = ValheimVRM.Settings.GetSettings(model).ModelScale;
        var path = Path.Combine(ValheimVRM.Settings.ConfigDir, "avatar_heights.json");
        var before = File.Exists(path) ? File.ReadAllBytes(path) : null;
        var players = Player.GetAllPlayers(); players.Add(a); players.Add(b);
        var applied = (Dictionary<Player,AvatarSelection>)AccessTools.Field(typeof(AvatarSyncClient),"applied").GetValue(sync);
        foreach(var selection in registry.Snapshot()) applied[selection.Peer==101?a:b]=selection;
        try
        {
        foreach (float height in new[] {1.4f, 2.2f, 2f, 1.4f})
        {
            registry.Set(101,1001,1,model,hash,height); SetState(sync,registry);
            yield return PumpRemote(sync);
            var sizing = VrmManager.PlayerToVrmInstance[a].GetComponent<AvatarScale>();
            Check(Mathf.Abs(sizing.UnscaledHeight * sizing.transform.localScale.y-height)<.0001f,"Remote visual missed requested height");
            Check(sizing.TargetHeight==height,"Remote instance did not retain its height");
            Check(VrmManager.PlayerToVrmInstance[b]==rootB && rootB.GetComponent<AvatarScale>().TargetHeight==2f,"A height changed B");
            Check(template.transform.localScale==templateScale && ValheimVRM.Settings.GetSettings(model).ModelScale==settingsScale,"Height mutated shared model cache/settings");
        }
        Check(before==null ? !File.Exists(path) : File.ReadAllBytes(path).SequenceEqual(before),"Remote height changed local preferences");
        // A refresh outside the picker must still read A's synchronized height.
        yield return VrmManager.VrmDic[model].SetToPlayer(a);
        Check(VrmManager.PlayerToVrmInstance[a].GetComponent<AvatarScale>().TargetHeight==1.4f,"External avatar reattachment lost synchronized height");
        var local=Player.m_localPlayer; var picker=OutfitSwitcher.Instance;
        var heightField=AccessTools.Field(typeof(OutfitSwitcher),"<Heights>k__BackingField");
        var originalHeights=picker.Heights;
        var originalCatalog=picker.Catalog;
        var catalogField=AccessTools.Field(typeof(OutfitSwitcher),"<Catalog>k__BackingField");
        var testCatalog=new AvatarCatalog(ValheimVRM.Settings.ValheimVRMDir,Path.Combine(output,"local-height-options")); testCatalog.Refresh();
        var testHeights=new AvatarHeightOptions(Path.Combine(output,"local-height-options"));
        try
        {
            Player.m_localPlayer=a; heightField.SetValue(picker,testHeights); catalogField.SetValue(picker,testCatalog);
            Check(picker.RequestHeight(2.2f),"Local height UI request was rejected");
            float deadline=Time.realtimeSinceStartup+120;
            while(picker.IsBusy) { if(Time.realtimeSinceStartup>deadline)throw new Exception("Local height timeout"); yield return null; }
            Check(VrmManager.PlayerToVrmInstance[a].GetComponent<AvatarScale>().TargetHeight==2.2f,"Local slider did not apply selected height");
            testHeights.Load(); Check(testHeights.Get(a.GetPlayerName())==2.2f,"Local height was not persisted by character");
            Check(VrmManager.PlayerToVrmInstance[b]==rootB,"Local height changed remote B");
            report.Add("Local height UI action: RequestHeight applies 2.2 m through the production picker, persists/reloads per character and leaves B unchanged; external remote refresh retains height");
        }
        finally { Player.m_localPlayer=local; heightField.SetValue(picker,originalHeights); catalogField.SetValue(picker,originalCatalog); }
        registry.Set(101,1001,1,model,hash,2f); SetState(sync,registry); yield return PumpRemote(sync);
        report.Add("Actual remote player clones: repeated 1.4/2.2/2/1.4 m changes meet requested height; same-model B stays at 2 m; cached import, model settings and local height file unchanged");
        }
        finally { players.Remove(a); players.Remove(b); }
    }
}
