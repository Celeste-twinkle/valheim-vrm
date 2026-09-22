using System;
using System.Linq;
using UnityEngine;

namespace ValheimVRM
{
    public sealed partial class OutfitSwitcher
    {
        string partFilter = "";

        void DrawPartOptions(string modelName)
        {
            if (string.IsNullOrEmpty(modelName) || Player.m_localPlayer == null ||
                !VrmManager.PlayerToVrmInstance.TryGetValue(Player.m_localPlayer, out var model) || model == null) return;
            var visibility = model.GetComponent<AvatarPartVisibility>();
            if (visibility == null) return;
            GUILayout.Space(6);
            if (!Foldout("parts", Text("Model parts", "模型部件") + " · " + visibility.Parts.Count)) return;
            GUILayout.Label(Text("Each entry is a renderable mesh in the current VRM. Choices are saved per model.",
                "以下列出当前 VRM 的全部可渲染网格，开关按模型保存。"), new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Text("Show all", "全部显示"))) SetAllParts(modelName, visibility, 1);
            if (GUILayout.Button(Text("Model defaults", "模型默认"))) SetAllParts(modelName, visibility, 0);
            if (GUILayout.Button(Text("Hide all", "全部隐藏"))) SetAllParts(modelName, visibility, -1);
            GUILayout.EndHorizontal();
            GUILayout.Label(Text("Search part name / hierarchy", "搜索部件名称／层级"));
            partFilter = GUILayout.TextField(partFilter);
            foreach (var part in visibility.Parts)
            {
                if (!string.IsNullOrEmpty(partFilter) && part.Path.IndexOf(partFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool visible = visibility.DesiredVisible(part);
                bool next = GUILayout.Toggle(visible, part.Path);
                if (next == visible) continue;
                try
                {
                    AvatarPartOptions.Current.Set(modelName, part.Id, next, part.AuthoredVisible);
                    visibility.SetVisible(part.Id, next);
                }
                catch (Exception ex) { ReportError("Cannot save avatar part settings", ex); }
            }
            GUILayout.Label(Text("Combined meshes are one entry. Missing parts on another installation are ignored. Changes are shared only when both part-sync options allow it.",
                "合并网格显示为一个部件；对方不存在的部件会被忽略。仅在对应配件同步开关允许时分享或应用。"),
                new GUIStyle(GUI.skin.label) { wordWrap = true });
        }

        void SetAllParts(string modelName, AvatarPartVisibility visibility, int mode)
        {
            try
            {
                var hidden = mode < 0 ? visibility.Parts.Select(p => p.Id).ToArray() : new string[0];
                var shown = mode > 0 ? visibility.Parts.Select(p => p.Id).ToArray() : new string[0];
                AvatarPartOptions.Current.SetAll(modelName, hidden, shown); visibility.SetOverrides(hidden, shown);
            }
            catch (Exception ex) { ReportError("Cannot save avatar part settings", ex); }
        }
    }
}
