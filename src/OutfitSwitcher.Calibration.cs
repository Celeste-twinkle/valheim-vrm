using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    public sealed partial class OutfitSwitcher
    {
        readonly HashSet<string> expandedCalibrations = new HashSet<string>(StringComparer.Ordinal);
        string animationFilter = "";
        bool currentAnimationsOnly;

        bool Foldout(string key, string label)
        {
            bool expanded = expandedCalibrations.Contains(key);
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, richText = false };
            if (GUILayout.Button((expanded ? "▼  " : "▶  ") + label, style, GUILayout.MinHeight(28)))
            {
                if (expanded) expandedCalibrations.Remove(key); else expandedCalibrations.Add(key);
                expanded = !expanded;
            }
            return expanded;
        }

        void DrawCalibrationOptions(string modelName)
        {
            if (string.IsNullOrEmpty(modelName) || Player.m_localPlayer == null ||
                !VrmManager.PlayerToVrmInstance.TryGetValue(Player.m_localPlayer, out var model) || model == null) return;
            var animation = model.GetComponent<VRMAnimationSync>();
            var profile = AvatarCalibrationOptions.Current.Get(modelName);
            GUI.enabled = !IsBusy;
            GUILayout.Space(6);
            if (Foldout("animations", Text("Animation position calibration", "动作位置校准")))
            {
                GUILayout.Label(Text("Automatic support is calibrated at load/height change. Manual offsets: X right, Y up, Z forward (character axes).",
                    "加载或改变身高时自动标定接触位置。手动偏移：X 向右、Y 向上、Z 向前，使用角色朝向。"),
                    new GUIStyle(GUI.skin.label) { wordWrap = true });
                if (animation?.AnimationCatalog != null)
                {
                    GUILayout.Label(Text("Search state / clip name", "搜索动画状态／片段名称"));
                    animationFilter = GUILayout.TextField(animationFilter);
                    currentAnimationsOnly = GUILayout.Toggle(currentAnimationsOnly, Text("Show active states only", "仅显示当前动作"));
                    GUILayout.Label(animation.AnimationCatalog.Entries.Count + Text(" animation states", " 个动画状态"));
                    foreach (var entry in animation.AnimationCatalog.Entries)
                    {
                        bool active = animation.IsActive(entry);
                        if (currentAnimationsOnly && !active) continue;
                        bool stateMatches = Matches(entry.Path);
                        if (!stateMatches && !Array.Exists(entry.Clips, Matches)) continue;
                        if (!Foldout(entry.Path, (active ? "● " : "") + entry.Path)) continue;
                        GUILayout.BeginVertical(GUI.skin.box);
                        GUILayout.Label(Text("Automatic reference: ", "自动基准：") + ContactLabel(entry.Contact));
                        DrawAnimationOffset(profile, entry.Path);
                        if (entry.Clips.Length > 1)
                        {
                            GUILayout.Label(Text("Blend clips · Each inherits this state's automatic reference. Extra offsets follow the game's blend weights.",
                                "混合片段：共用此状态的自动基准，各片段额外偏移按游戏动画权重混合。"),
                                new GUIStyle(GUI.skin.label) { wordWrap = true });
                            foreach (string clip in entry.Clips)
                            {
                                if (!stateMatches && !Matches(clip)) continue;
                                string key = entry.ClipKey(clip);
                                if (Foldout(key, clip)) DrawAnimationOffset(profile, key);
                            }
                        }
                        GUILayout.EndVertical();
                    }
                }
            }
            if (Foldout("equipment", Text("Held item calibration", "握持道具校准")))
            {
                GUILayout.Label(Text("Automatic grip uses this avatar's palm bones. Item size follows height (2 m = 100%); sliders multiply the original size.",
                    "根据模型手掌骨骼自动校准握点；道具随身高缩放（2 米 = 100%），滑块在原始大小上等比调整。"),
                    new GUIStyle(GUI.skin.label) { wordWrap = true });
                DrawEquipment("left", Text("Left hand", "左手道具"), profile.Left);
                DrawEquipment("right", Text("Right hand", "右手道具"), profile.Right);
                DrawEquipment("two", Text("Two-handed items (including bows)", "双手道具（含弓）"), profile.TwoHanded);
                DrawEquipment("back", Text("Back equipment (optional adjustment)", "背负装备（可选微调）"), profile.Back);
                GUILayout.Label(Text("Item offsets use grip axes: X right, Y up, Z forward. A two-handed item uses only its two-handed settings.",
                    "道具偏移使用握点坐标：X 右、Y 上、Z 前。双手道具仅应用双手组设置。"),
                    new GUIStyle(GUI.skin.label) { wordWrap = true });
            }
            GUILayout.Label(Text("Saved per avatar locally. Server calibration sync shares your controls with other players after release.",
                "微调按模型保存在本机；支持校准同步时，松开滑块后会分享给其他玩家。"),
                new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUI.enabled = true;
        }

        bool Matches(string value) => string.IsNullOrEmpty(animationFilter) || value.IndexOf(animationFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        string ContactLabel(AvatarContactKind kind)
        {
            switch (kind)
            {
                case AvatarContactKind.Ground: return Text("Ground sitting / kneeling", "地面坐姿／跪姿");
                case AvatarContactKind.Seat: return Text("Seat / saddle", "座面／鞍座");
                case AvatarContactKind.Head: return Text("Head / water level", "头部／水面");
                case AvatarContactKind.Hands: return Text("Hand contact", "手部接触");
                case AvatarContactKind.Reclining: return Text("Reclining support", "躺卧接触面");
                case AvatarContactKind.Overlay: return Text("Underlying body posture", "下层全身姿态");
                default: return Text("Stable standing skeleton", "稳定站姿骨架");
            }
        }
        Vector3 PositionSliders(Vector3 value)
        {
            return new Vector3(HeightSlider(Text("X offset", "X 轴偏移"), value.x),
                HeightSlider(Text("Y offset", "Y 轴偏移"), value.y), HeightSlider(Text("Z offset", "Z 轴偏移"), value.z));
        }
        void DrawAnimationOffset(AvatarCalibrationOptions.Profile profile, string key)
        {
            Vector3 previous = profile.Get(key), next = PositionSliders(previous);
            if (GUILayout.Button(Text("Reset XYZ to 0", "三轴恢复为 0"))) next = Vector3.zero;
            if (next == previous) return;
            profile.Set(key, next); AvatarCalibrationOptions.Current.Changed();
        }
        void DrawEquipment(string key, string label, AvatarCalibrationOptions.Equipment options)
        {
            if (!Foldout("equipment/" + key, label)) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(Text("Uniform scale", "等比缩放") + "  " + Mathf.RoundToInt(options.Multiplier * 100) + "%");
            float scale = Mathf.Round(GUILayout.HorizontalSlider(options.Multiplier, .25f, 2f) * 100) / 100;
            Vector3 position = PositionSliders(options.Position.Value);
            if (GUILayout.Button(Text("Reset scale / XYZ", "恢复缩放与三轴默认值"))) { scale = 1; position = Vector3.zero; }
            if (scale != options.Scale || position != options.Position.Value)
            {
                options.Scale = scale; options.Position.Value = position; AvatarCalibrationOptions.Current.Changed();
            }
            GUILayout.EndVertical();
        }
        void SaveCalibrationOptions()
        {
            try { AvatarCalibrationOptions.Current.Save(); }
            catch (Exception ex) { ReportError("Cannot save animation / item calibration", ex); }
        }
    }
}
