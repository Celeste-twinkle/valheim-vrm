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
    IEnumerator AvatarPartTests(Player a, Player b, string model)
    {
        var rootA = VrmManager.PlayerToVrmInstance[a]; var rootB = VrmManager.PlayerToVrmInstance[b];
        var partsA = rootA.GetComponent<AvatarPartVisibility>(); var partsB = rootB.GetComponent<AvatarPartVisibility>();
        Check(partsA != null && partsB != null && partsA.Parts.Count > 0 && partsA.Parts.Count == partsB.Parts.Count,
            "Actual VRM parts were not enumerated consistently");
        Check(partsA.Parts.Select(p => p.Id).Distinct().Count() == partsA.Parts.Count, "Part IDs are not unique");
        Check(partsA.Parts.All(p => p.Path.Length > 0) && partsA.Parts.All(p => !p.Path.Contains("ValheimVRM GPU fur")),
            "Part list contains a nameless/duplicate fur overlay");
        var selected = partsA.Parts.Where(p => p.Enabled && partsB.Parts.Any(q => q.Id == p.Id && q.Enabled)).Take(1).ToArray();
        Check(selected.Length == 1, "VRM fixture needs one visible part");
        string shownFixtureId = new string('e', 64);
        var currentField = AccessTools.Field(typeof(AvatarPartOptions), "current");
        var previousOptions = currentField.GetValue(null);
        var options = new AvatarPartOptions(Path.Combine(output, "part-options")); currentField.SetValue(null, options);
        var binding = rootB.GetComponent<AvatarCalibrationBinding>(); string oldEncoded = binding.Encoded;
        var sync = AvatarSyncClient.Instance; bool oldShare = sync.SharePartSettings, oldApply = sync.ApplyRemotePartSettings;
        var oldLocal = Player.m_localPlayer;
        GameObject hiddenFixture = null;
        try
        {
            options.Set(model, selected[0].Id, false, selected[0].AuthoredVisible);
            options.Set(model, shownFixtureId, true, false);
            var localState = options.Get(model); partsA.SetOverrides(localState.Hidden, localState.Shown);
            Check(selected.All(p => partsA.IsHidden(p.Id) && !p.Enabled) && localState.Shown.SetEquals(new[] { shownFixtureId }),
                "Local part switches did not apply hidden/shown overrides");
            var reloaded = new AvatarPartOptions(Path.Combine(output, "part-options")); reloaded.Load();
            var reloadedState = reloaded.Get(model);
            Check(reloadedState.Hidden.SetEquals(selected.Select(p => p.Id)) && reloadedState.Shown.SetEquals(new[] { shownFixtureId }),
                "Part choices did not persist per model");
            string shared = AvatarCalibrationBinding.Capture(model, true);
            string privateState = AvatarCalibrationBinding.Capture(model, false);
            AvatarCalibrationCodec.TryDecode(shared, out var sharedData); AvatarCalibrationCodec.TryDecode(privateState, out var privateData);
            Check(sharedData.Animations.Keys.Any(AvatarPartSync.IsReserved) &&
                privateData.Animations.Keys.All(k => !AvatarPartSync.IsReserved(k)), "Share-my-parts option did not control outgoing state");
            Check(binding.Apply(shared, true), "Remote part payload was rejected");
            var sameB = selected.Select(p => partsB.Parts.Single(q => q.Id == p.Id)).ToArray();
            Check(sameB.All(p => p.Hidden && !p.Enabled) &&
                binding.HiddenParts.SetEquals(selected.Select(p => p.Id)) && binding.ShownParts.SetEquals(new[] { shownFixtureId }),
                "Remote same-model part settings were lost or crossed");
            Check(binding.Profile.Animations.Keys.All(k => !AvatarPartSync.IsReserved(k)), "Part keys polluted animation controls");
            Player.m_localPlayer = a;
            sync.SetApplyRemotePartSettings(false);
            Check(!sync.ApplyRemotePartSettings && sameB.All(p => !p.Hidden && p.Enabled) && binding.ShownParts.Count == 0,
                "Ignore-other-parts option did not restore authored visibility");
            sync.SetApplyRemotePartSettings(true);
            Check(sameB.All(p => p.Hidden && !p.Enabled) && binding.ShownParts.SetEquals(new[] { shownFixtureId }),
                "Re-enabling other-player parts did not reapply retained state");
            sync.SetSharePartSettings(false); Check(!sync.SharePartSettings, "Share-my-parts option was not persisted");
            sync.SetSharePartSettings(true); Check(sync.SharePartSettings, "Share-my-parts option could not be re-enabled");
            Check(partsA.IsHidden(selected[0].Id), "Applying remote settings changed local choices");
            var unknown = new string('f', 64); var data = new AvatarCalibrationData();
            foreach (var key in AvatarPartSync.Encode(new[] { unknown })) data.Animations[key] = new AvatarCalibrationData.Position(0, 0, 0);
            Check(binding.Apply(AvatarCalibrationCodec.Encode(data), true) && binding.HiddenParts.Contains(unknown) &&
                partsB.Parts.All(p => !p.Hidden), "Unknown remote part was not ignored safely");

            var isolated = new GameObject("Part visibility fixture"); hiddenFixture = new GameObject("Inactive part fixture");
            hiddenFixture.transform.SetParent(isolated.transform, false); hiddenFixture.SetActive(false);
            var mesh = hiddenFixture.AddComponent<MeshRenderer>(); var fixtureVisibility = isolated.AddComponent<AvatarPartVisibility>();
            fixtureVisibility.Initialize(new string[0]); var inactivePart = fixtureVisibility.Parts.Single();
            Check(!inactivePart.AuthoredVisible && !inactivePart.Visible, "Inactive authored part was not identified");
            fixtureVisibility.SetVisible(inactivePart.Id, true);
            Check(inactivePart.Shown && inactivePart.Visible && mesh.gameObject.activeInHierarchy,
                "Originally inactive part could not be explicitly shown");
            fixtureVisibility.SetVisible(inactivePart.Id, false);
            Check(!inactivePart.Shown && !inactivePart.Visible && !mesh.gameObject.activeInHierarchy,
                "Clearing an explicit show did not restore the model default");
            UnityEngine.Object.DestroyImmediate(isolated); hiddenFixture = null;
            report.Add("Avatar parts: every actual renderer listed with stable unique IDs; visible and authored-inactive nodes can be hidden/shown or restored to model defaults; local overrides persist per model; compact format-1 state is optional on send/apply, isolated between same-model players, excluded from animation controls, restores authored visibility and ignores missing parts");
        }
        finally
        {
            if (hiddenFixture != null) UnityEngine.Object.DestroyImmediate(hiddenFixture.transform.root.gameObject);
            partsA.SetOverrides(new string[0]);
            if (oldEncoded != null) binding.Apply(oldEncoded, true); else partsB.SetOverrides(new string[0]);
            sync.SetSharePartSettings(oldShare); sync.SetApplyRemotePartSettings(oldApply);
            Player.m_localPlayer = oldLocal;
            currentField.SetValue(null, previousOptions);
        }
        yield break;
    }
}
