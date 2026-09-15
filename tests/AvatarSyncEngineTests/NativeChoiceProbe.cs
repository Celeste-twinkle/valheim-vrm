using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using ValheimVRM.Sync;

public sealed partial class AvatarSyncEngineTests
{
    IEnumerator NativeChoiceTests(AvatarSyncClient sync, Player a, Player b, string model)
    {
        var picker=OutfitSwitcher.Instance; var previousCatalog=picker.Catalog; var previousLocal=Player.m_localPlayer;
        var catalog=new AvatarCatalog(ValheimVRM.Settings.ValheimVRMDir,Path.Combine(output,"native-choice")); catalog.Refresh();
        var catalogField=AccessTools.Field(typeof(OutfitSwitcher)," <Catalog>k__BackingField".Trim());
        var rootB=VrmManager.PlayerToVrmInstance[b];
        var baseline=a.GetComponent<RemoteAvatarBaseline>();
        float originalDistance=(float)AccessTools.Field(typeof(RemoteAvatarBaseline),"interactionDistance").GetValue(baseline);
        var originalCenter=(Vector3)AccessTools.Field(typeof(RemoteAvatarBaseline),"capsuleCenter").GetValue(baseline);
        float originalHeight=(float)AccessTools.Field(typeof(RemoteAvatarBaseline),"capsuleHeight").GetValue(baseline);
        var capsule=a.GetComponent<CapsuleCollider>();
        var body=a.GetComponent<Rigidbody>();
        var visual=a.GetVisual(); var animator=visual.GetComponent<Animator>();
        Check(a.m_eye!=null,"Native camera pivot missing");
        var originalEye=a.m_eye.localPosition;
        var states=new Dictionary<string,object>();
        foreach(string key in new[]{"server","hostPlugin","lastSent","requestSequence","<SequencedRequests>k__BackingField","<HeightSync>k__BackingField","<CalibrationSync>k__BackingField"})
            states[key]=AccessTools.Field(typeof(AvatarSyncClient),key).GetValue(sync);
        var left=new MemorySocket(); var right=new MemorySocket(); left.Other=right; right.Other=left;
        var client=new ZRpc(left); var relay=new ZRpc(right); var packets=new List<string>(); long previousSequence=0;
        relay.Register<ZPackage>(AvatarSyncWire.Select,(rpc,p)=>{
            Check(AvatarSyncWire.ReadSelection(p,out var enabled,out var name,out _,out var sequence,out _,out var height,out var calibration) &&
                sequence>previousSequence && height && calibration!=null,"Native choice packet invalid or unsequenced");
            previousSequence=sequence;
            Check(enabled,"Native choice disabled viewing other players"); packets.Add(name);
        });
        Action send=()=>{AccessTools.Method(typeof(AvatarSyncClient),"SendSelection").Invoke(sync,null);relay.Update(.01f);};
        try
        {
            Player.m_localPlayer=a; catalogField.SetValue(picker,catalog);
            AccessTools.Field(typeof(AvatarSyncClient),"server").SetValue(sync,client);
            AccessTools.Field(typeof(AvatarSyncClient),"hostPlugin").SetValue(sync,null);
            AccessTools.Field(typeof(AvatarSyncClient),"lastSent").SetValue(sync,null);
            AccessTools.Field(typeof(AvatarSyncClient),"requestSequence").SetValue(sync,0L);
            foreach(string name in new[]{"SequencedRequests","HeightSync","CalibrationSync"})
                AccessTools.Field(typeof(AvatarSyncClient),"<"+name+">k__BackingField").SetValue(sync,true);
            send(); Check(packets.Count==1 && packets[0]==model,"Initial model choice not sent");
            for(int cycle=0;cycle<4;cycle++)
            {
                // Exercise all native state that local avatar attachment changes.
                capsule.height=3; capsule.center=Vector3.one; a.m_maxInteractDistance=9;
                body.centerOfMass=Vector3.one; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var camera=a.GetComponent<VRMEyePositionSync>() ?? a.gameObject.AddComponent<VRMEyePositionSync>();
                camera.Setup(VrmManager.PlayerToVrmInstance[a].GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head),.1f);
                Check(picker.RequestSwitch(AvatarCatalog.OriginalModel),"Native option rejected");
                while(picker.IsBusy)yield return null;
                Check(!VrmManager.PlayerToVrmInstance.ContainsKey(a) && !VrmManager.PlayerToName.ContainsKey(a) &&
                    a.GetComponent<VrmController>().visual==null,"Native choice retained VRM binding");
                Check(capsule.height==originalHeight && capsule.center==originalCenter && a.m_maxInteractDistance==originalDistance,"Native physical state not restored");
                Check(Vector3.Distance(a.m_eye.localPosition,originalEye)<.000001f,"Native camera pivot was not restored");
                foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    Check(!renderer.forceRenderingOff,"Original renderer remained hidden");
                Check(VrmManager.PlayerToVrmInstance[b]==rootB && sync.SyncEnabled,"Native choice changed B or disabled sync");
                catalog.LoadSelections();
                Check(catalog.IsOriginalSelected(a.GetPlayerName()) && OutfitSwitcher.ResolveModelName(a.GetPlayerName())=="","Native choice did not persist");
                send(); int count=packets.Count; send();
                Check(packets[count-1]=="" && packets.Count==count,"Native choice did not withdraw once");
                Check(!picker.RequestHeight(1.4f),"Height adjustment reattached a VRM in native mode");
                Check(picker.RequestSwitch(model),"Could not switch from native back to VRM");
                float deadline=Time.realtimeSinceStartup+120;
                while(picker.IsBusy){if(Time.realtimeSinceStartup>deadline)throw new Exception("Native-to-VRM timeout");yield return null;}
                Check(VrmManager.PlayerToVrmInstance.ContainsKey(a) && !catalog.IsOriginalSelected(a.GetPlayerName()),"VRM selection failed to clear native preference");
                send(); Check(packets[packets.Count-1]==model,"Returning to VRM did not resubmit selection");
            }
            report.Add("Native choice: four VRM/native/VRM cycles restore original renderers, capsule, camera pivot and interaction distance; explicit empty preference survives reload; native suppresses height reattachment; increasing sequenced empty selection withdraws once while B and sync remain unchanged");
        }
        finally
        {
            Player.m_localPlayer=previousLocal; catalogField.SetValue(picker,previousCatalog);
            foreach(var pair in states)AccessTools.Field(typeof(AvatarSyncClient),pair.Key).SetValue(sync,pair.Value);
            client.Dispose();relay.Dispose();
        }
    }
}
