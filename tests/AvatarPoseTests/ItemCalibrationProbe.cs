using System;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

static class ItemCalibrationProbe
{
    public static void Run(Animator source, Animator target, ValheimVRM.Settings.VrmSettingsContainer settings)
    {
        var fixture = new GameObject("ItemCalibrationProbe"); fixture.SetActive(false);
        var equipment = fixture.AddComponent<VisEquipment>();
        equipment.m_leftHand = source.GetBoneTransform(HumanBodyBones.LeftHand).Find("LeftHand_Attach");
        equipment.m_rightHand = source.GetBoneTransform(HumanBodyBones.RightHand).Find("RightHand_Attach");
        var sync = fixture.AddComponent<VRMEquipmentSync>();
        var options = AvatarCalibrationOptions.Current.Get(settings.Name);
        options.Left.Scale = 1.35f; options.Left.Position.Value = new Vector3(.03f, -.04f, .05f);
        options.Right.Scale = .6f; options.Right.Position.Value = new Vector3(-.07f, .04f, .02f);
        options.TwoHanded.Scale = 1.75f; options.TwoHanded.Position.Value = new Vector3(.02f, .07f, -.08f);
        foreach (string name in new[] { "ShieldWood", "SwordIron", "BowHuntsman", "AtgeirBronze" })
        {
            var prefab = ObjectDB.instance.GetItemPrefab(name);
            if (prefab == null) throw new Exception("Missing native test item: " + name);
            var type = prefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_itemType;
            bool left = name == "ShieldWood" || name == "BowHuntsman";
            bool two = name == "BowHuntsman" || name == "AtgeirBronze";
            if (VRMEquipmentSync.IsTwoHanded(type) != two) throw new Exception("Wrong native item category: " + name);
            var selected = two ? options.TwoHanded : left ? options.Left : options.Right;
            var mount = left ? equipment.m_leftHand : equipment.m_rightHand;
            var item = new GameObject(name + "_attachment").transform; item.SetParent(mount, false);
            // Deliberately non-unit authored transforms catch overwritten baselines.
            var originalPosition = new Vector3(.013f, -.027f, .019f);
            var originalScale = new Vector3(.8f, .9f, 1.1f);
            var originalRotation = Quaternion.Euler(7, 13, -19);
            item.localPosition = originalPosition; item.localRotation = originalRotation; item.localScale = originalScale;
            AccessTools.Field(typeof(VisEquipment), left ? "m_leftItem" : "m_rightItem").SetValue(equipment, name.GetStableHashCode());
            AccessTools.Field(typeof(VisEquipment), left ? "m_leftItemInstance" : "m_rightItemInstance").SetValue(equipment, item.gameObject);
            sync.Setup(source, target, equipment, settings);
            float factor = target.GetComponent<AvatarScale>().TargetHeight / 2f * selected.Multiplier;
            for (int frame = 0; frame < 90; frame++)
            {
                AccessTools.Method(typeof(VRMEquipmentSync), "LateUpdate").Invoke(sync, null);
                var expected = mount.TransformPoint(originalPosition * factor) + mount.rotation * selected.Position.Value;
                if (Vector3.Distance(item.position, expected) > .00001f ||
                    Vector3.Distance(item.localScale, originalScale * factor) > .00001f ||
                    Quaternion.Angle(item.localRotation, originalRotation) > .02f)
                    throw new Exception("Item height/category/absolute transform failure: " + name);
            }
            // Changes apply immediately and only once, then return to authored data.
            selected.Scale = .5f;
            AccessTools.Method(typeof(VRMEquipmentSync), "LateUpdate").Invoke(sync, null);
            if (Vector3.Distance(item.localScale, originalScale * target.GetComponent<AvatarScale>().TargetHeight / 2f * .5f) > .00001f)
                throw new Exception("Item slider did not preview");
            sync.ResetAttachments();
            if (item.localPosition != originalPosition || item.localScale != originalScale ||
                Quaternion.Angle(item.localRotation, originalRotation) > .02f) throw new Exception("Item baseline not restored");
            AccessTools.Field(typeof(VisEquipment), left ? "m_leftItemInstance" : "m_rightItemInstance").SetValue(equipment, null);
            Object.Destroy(item.gameObject);
        }
        options.Left.Scale = options.Right.Scale = options.TwoHanded.Scale = 1;
        options.Left.Position.Value = options.Right.Position.Value = options.TwoHanded.Position.Value = Vector3.zero;
        Object.Destroy(fixture);
    }
}
