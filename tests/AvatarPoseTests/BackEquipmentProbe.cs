using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

static class BackEquipmentProbe
{
    public static void Run(Animator source, Animator target, ValheimVRM.Settings.VrmSettingsContainer settings, VRMAnimationSync animation)
    {
        var prefab=(GameObject)AccessTools.Field(typeof(FejdStartup),"m_playerPrefab").GetValue(Object.FindFirstObjectByType<FejdStartup>());
        var template=prefab.GetComponentInChildren<Animator>(true); var originalEquipment=prefab.GetComponentInChildren<VisEquipment>(true);
        var fixture=new GameObject("NativeBackEquipmentProbe"); fixture.SetActive(false);
        var equipment=fixture.AddComponent<VisEquipment>(); equipment.m_isPlayer=true;
        var player=fixture.AddComponent<Player>();
        var sync=fixture.AddComponent<VRMEquipmentSync>();
        var fields=new[]{"m_leftHand","m_rightHand","m_backShield","m_backMelee","m_backTwohandedMelee","m_backBow","m_backTool","m_backAtgeir"};
        foreach(string field in fields)
        {
            var mount=(Transform)AccessTools.Field(typeof(VisEquipment),field).GetValue(originalEquipment);
            var names=new List<string>();
            for(var t=mount;t!=template.transform;t=t.parent){if(t==null)throw new Exception("Socket outside native visual");names.Insert(0,t.name);}
            var clone=source.transform.Find(string.Join("/",names));
            if(clone==null)throw new Exception("Missing native socket: "+field);
            AccessTools.Field(typeof(VisEquipment),field).SetValue(equipment,clone);
        }
        var profile=AvatarCalibrationOptions.Current.Get(settings.Name);
        var beforeScale=profile.Back.Scale; var beforeOffset=profile.Back.Position.Value;
        var postfix=AccessTools.Method(typeof(MainPlugin).Assembly.GetType("ValheimVRM.Patch_VisEquipment_UpdateLodgroup"),"Postfix");
        var tick=AccessTools.Method(typeof(VRMEquipmentSync),"LateUpdate");
        var pose=AccessTools.Method(typeof(VRMAnimationSync),"LateUpdate");
        var attach=AccessTools.Method(typeof(VisEquipment),"AttachBackItem");
        var handAttach=AccessTools.Method(typeof(VisEquipment),"AttachItem");
        VrmManager.PlayerToName[player]=settings.Name; VrmManager.PlayerToVrmInstance[player]=target.gameObject;
        if(!ValheimVRM.Settings.ContainsSettings(settings.Name)) ValheimVRM.Settings.AddSettingsFromFile(settings.Name,false);
        int checks=0;
        var heading=source.transform.parent; var headingRotation=heading.rotation;
        try
        {
            foreach(string itemName in new[]{"Hammer","Hoe","SwordIron","KnifeFlint","BowHuntsman","ShieldWood","AtgeirBronze","SledgeIron","StaffFireball"})
            {
                Check(ObjectDB.instance.GetItemPrefab(itemName)!=null,"Missing native equipment "+itemName);
                bool left=itemName=="ShieldWood" || itemName=="BowHuntsman";
                string slot=left?"m_leftBackItemInstance":"m_rightBackItemInstance";
                sync.Setup(source,target,equipment,settings);
                for(int cycle=0;cycle<3;cycle++)
                {
                    heading.rotation=headingRotation*Quaternion.Euler(0,cycle*73,0);
                    var held=(GameObject)handAttach.Invoke(equipment,new object[]{itemName.GetStableHashCode(),0,left?equipment.m_leftHand:equipment.m_rightHand,false,false,1});
                    var handField=AccessTools.Field(typeof(VisEquipment),left?"m_leftItemInstance":"m_rightItemInstance");
                    handField.SetValue(equipment,held);
                    AccessTools.Field(typeof(VisEquipment),left?"m_leftItem":"m_rightItem").SetValue(equipment,itemName.GetStableHashCode());
                    tick.Invoke(sync,null);
                    handField.SetValue(equipment,null); Object.DestroyImmediate(held);
                    var item=(GameObject)attach.Invoke(equipment,new object[]{itemName.GetStableHashCode(),0,!left,1});
                    Check(item!=null,"Game could not attach back item "+itemName);
                    AccessTools.Field(typeof(VisEquipment),slot).SetValue(equipment,item);
                    var mount=item.transform.parent;
                    var position=item.transform.localPosition; var rotation=item.transform.localRotation; var scale=item.transform.localScale;
                    profile.Back.Scale=cycle==1?1.37f:1; profile.Back.Position.Value=cycle==1?new Vector3(.07f,-.08f,.09f):Vector3.zero;
                    float factor=target.GetComponent<AvatarScale>().TargetHeight/2 * profile.Back.Multiplier;
                    foreach(string state in new[]{"Base Layer.Movement","Base Layer.Emote_sit","Base Layer.SitChair","Base Layer.In Water","Base Layer.equip_hip","Base Layer.equip_head"})
                    {
                        source.Play(state,0,0); source.Update(0);
                        for(int frame=0;frame<30;frame++)
                        {
                            source.Update(1f/60); pose.Invoke(animation,null);
                            // Repeated equipment refresh used to overwrite authored
                            // back offsets/scale and increment knife/staff rotations.
                            postfix.Invoke(null,new object[]{equipment}); tick.Invoke(sync,null);
                            var expected=mount.TransformPoint(position*factor)+source.transform.rotation*profile.Back.Position.Value;
                            Check(Vector3.Distance(item.transform.position,expected)<.00002f &&
                                Vector3.Distance(item.transform.localScale,scale*factor)<.00002f &&
                                Quaternion.Angle(item.transform.localRotation,rotation)<.025f,
                                "Back item drift/scale/rotation: "+itemName+" "+state+" cycle "+cycle);
                            if(itemName=="Hammer" && state=="Base Layer.Movement")
                                foreach(var renderer in item.GetComponentsInChildren<Renderer>())
                                    Check(renderer.bounds.center.y > target.transform.position.y-.15f,"Sheathed hammer mesh moved underground");
                            checks++;
                        }
                    }
                    sync.ResetAttachments();
                    Check(Vector3.Distance(item.transform.localPosition,position)<.00001f &&
                        Vector3.Distance(item.transform.localScale,scale)<.00001f,"Back item original transform was not restored");
                    AccessTools.Field(typeof(VisEquipment),slot).SetValue(equipment,null);
                    Object.DestroyImmediate(item);
                    sync.Setup(source,target,equipment,settings);
                }
            }
            if(checks!=4860)throw new Exception("Back-equipment coverage incomplete");
        }
        finally
        {
            heading.rotation=headingRotation;
            sync.ResetAttachments(); profile.Back.Scale=beforeScale; profile.Back.Position.Value=beforeOffset;
            VrmManager.PlayerToName.Remove(player); VrmManager.PlayerToVrmInstance.Remove(player); Object.Destroy(fixture);
        }
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
}
