using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ValheimVRM
{
    // Native clothing and wearable accessories belong to the hidden Valheim body.
    // Held and sheathed items remain visible and are positioned by VRMEquipmentSync.
    internal static class NativeEquipmentVisibility
    {
        static readonly HashSet<string> Handled = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_hairItemInstance", "m_beardItemInstance", "m_helmetItemInstance",
            "m_chestItemInstances", "m_legItemInstances", "m_shoulderItemInstances",
            "m_utilityItemInstances"
        };

        static readonly FieldInfo[] AdditionalWearables = AccessTools.GetDeclaredFields(typeof(VisEquipment))
            .Where(field => (field.Name.EndsWith("ItemInstance", StringComparison.Ordinal) ||
                             field.Name.EndsWith("ItemInstances", StringComparison.Ordinal)) &&
                            !Handled.Contains(field.Name) && !IsHeldOrBack(field.Name))
            .ToArray();

        internal static void Apply(VisEquipment equipment, Settings.VrmSettingsContainer settings)
        {
            if (equipment == null || settings == null) return;
            SetField(equipment, "m_hairItemInstance", false);
            SetField(equipment, "m_beardItemInstance", false);
            SetField(equipment, "m_chestItemInstances", settings.ChestVisible);
            SetField(equipment, "m_legItemInstances", settings.LegsVisible);
            SetField(equipment, "m_shoulderItemInstances", settings.ShouldersVisible);
            SetField(equipment, "m_utilityItemInstances", settings.UtilityVisible);

            var helmet = AccessTools.Field(typeof(VisEquipment), "m_helmetItemInstance")?.GetValue(equipment) as GameObject;
            if (helmet != null)
            {
                SetVisible(helmet, settings.HelmetVisible);
                if (settings.HelmetVisible)
                {
                    helmet.transform.localScale = settings.HelmetScale;
                    helmet.transform.localPosition = settings.HelmetOffset;
                }
            }

            // Valheim 1.0.12 added m_trinketItemInstances. Treat any unrecognized
            // non-hand/non-back instance slot as a native wearable so future slots
            // cannot leak through the replacement avatar.
            foreach (var field in AdditionalWearables)
                foreach (var item in Instances(field.GetValue(equipment))) SetVisible(item, false);
        }

        static bool IsHeldOrBack(string fieldName)
        {
            return fieldName == "m_leftItemInstance" || fieldName == "m_rightItemInstance" ||
                   fieldName.IndexOf("BackItemInstance", StringComparison.Ordinal) >= 0;
        }

        static void SetField(VisEquipment equipment, string fieldName, bool visible)
        {
            var field = AccessTools.Field(typeof(VisEquipment), fieldName);
            if (field == null) return;
            foreach (var item in Instances(field.GetValue(equipment))) SetVisible(item, visible);
        }

        static IEnumerable<GameObject> Instances(object value)
        {
            if (value is GameObject one) { yield return one; yield break; }
            if (!(value is IEnumerable<GameObject> many)) yield break;
            foreach (var item in many) if (item != null) yield return item;
        }

        static void SetVisible(GameObject item, bool visible)
        {
            if (item == null) return;
            // Include inactive variants because the game may activate them after
            // this pass without rebuilding the owning equipment instance.
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
        }
    }
}
