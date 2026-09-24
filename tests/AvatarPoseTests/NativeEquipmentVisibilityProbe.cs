using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

static class NativeEquipmentVisibilityProbe
{
    public static void Run()
    {
        var fixture = new GameObject("NativeEquipmentVisibilityProbe");
        fixture.SetActive(false);
        var equipment = fixture.AddComponent<VisEquipment>();
        var apply = typeof(MainPlugin).Assembly.GetType("ValheimVRM.NativeEquipmentVisibility")
            ?.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic);
        Check(apply != null, "Native equipment visibility policy is missing");

        var hand = Item("held hand item", true);
        var back = Item("sheathed back item", true);
        var trinket = Item("wearable trinket", false);
        var chest = Item("optional native chest", true);
        hand.transform.SetParent(fixture.transform, false);
        back.transform.SetParent(fixture.transform, false);
        trinket.transform.SetParent(fixture.transform, false);
        chest.transform.SetParent(fixture.transform, false);
        try
        {
            AccessTools.Field(typeof(VisEquipment), "m_leftItemInstance").SetValue(equipment, hand);
            AccessTools.Field(typeof(VisEquipment), "m_rightBackItemInstance").SetValue(equipment, back);
            var trinketField = AccessTools.Field(typeof(VisEquipment), "m_trinketItemInstances");
            Check(trinketField != null, "Current Valheim trinket slot is missing from the integration target");
            trinketField.SetValue(equipment, new List<GameObject> { trinket });
            AccessTools.Field(typeof(VisEquipment), "m_chestItemInstances").SetValue(equipment, new List<GameObject> { chest });

            var settings = new ValheimVRM.Settings.VrmSettingsContainer { ChestVisible = true };
            apply.Invoke(null, new object[] { equipment, settings });
            Check(Enabled(hand) && Enabled(back), "Held or back equipment was hidden with native wearables");
            Check(Enabled(chest), "An explicitly enabled native armor slot was hidden");
            Check(!Enabled(trinket), "A wearable trinket remained visible with the native body hidden");

            settings.ChestVisible = false;
            apply.Invoke(null, new object[] { equipment, settings });
            Check(!Enabled(chest), "A disabled native armor slot remained visible");
            Check(!Enabled(trinket), "Repeated equipment refresh restored a hidden trinket");
        }
        finally { Object.DestroyImmediate(fixture); }
    }

    static GameObject Item(string name, bool activeChild)
    {
        var root = new GameObject(name);
        var child = new GameObject(name + " renderer");
        child.transform.SetParent(root.transform, false);
        child.AddComponent<MeshRenderer>().enabled = true;
        child.SetActive(activeChild);
        return root;
    }

    static bool Enabled(GameObject item) => item.GetComponentInChildren<Renderer>(true).enabled;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
