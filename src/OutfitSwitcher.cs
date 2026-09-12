using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace ValheimVRM
{
    public sealed class OutfitSwitcher : MonoBehaviour
    {
        public static OutfitSwitcher Instance { get; private set; }
        public bool IsBusy { get; private set; }
        public bool MenuOpen { get; private set; }
        public string LastError { get; private set; } = "";
        public AvatarCatalog Catalog { get; private set; }

        Rect window;
        Vector2 scroll;
        Font font;
        string loadingName;
        bool physicsDirty;
        sealed class RemoteAvatarUnavailableException : Exception
        {
            public RemoteAvatarUnavailableException(string message) : base(message) { }
        }
        bool Chinese => Localization.instance != null && Localization.instance.GetSelectedLanguage() == "Chinese";
        string Text(string english, string chinese) => Chinese ? chinese : english;

        void Awake()
        {
            Instance = this;
            Catalog = new AvatarCatalog(Settings.ValheimVRMDir);
            RefreshModels();
            try { Catalog.LoadSelections(); }
            catch (Exception ex) { ReportError("Cannot read avatar selections", ex); }
        }

        public void RefreshModels()
        {
            try
            {
                Catalog.Refresh();
                LastError = "";
                AvatarSyncClient.Instance?.RetryMissing();
            }
            catch (Exception ex) { ReportError("Cannot scan the VRM folder", ex); }
        }

        public static string ResolveModelName(string characterName)
        {
            return Instance != null ? Instance.Catalog.Resolve(characterName) : characterName;
        }

        public static string ResolveModelPath(string name)
        {
            if (Instance != null && Instance.Catalog.TryGetPath(name, out var path)) return path;
            return Path.Combine(Settings.ValheimVRMDir, name + ".vrm");
        }

        public bool RequestSwitch(string name)
        {
            var player = Player.m_localPlayer;
            if (IsBusy || player == null || player.IsDead() || player.InIntro()) return false;
            if (!Catalog.TryGetPath(name, out _)) return false;
            if (VrmManager.LoadingPlayers.Contains(player))
            {
                LastError = Text("Please wait for the current avatar to load.", "请等待当前模型载入完成。");
                return false;
            }
            IsBusy = true;
            LastError = "";
            loadingName = name;
            StartCoroutine(RunSwitch(Switch(player, name)));
            return true;
        }

        // Unity runs nested enumerators separately. Drive them here so an import
        // exception clears the busy state and is shown in the same menu.
        internal void RequestRemoteSwitch(Player player, string name, string hash, Func<bool> stillCurrent, Action<bool> completed)
        {
            if (IsBusy || player == null || player == Player.m_localPlayer) { completed(false); return; }
            IsBusy = true; LastError = ""; loadingName = name;
            StartCoroutine(RunSwitch(Switch(player, name, false, hash, stillCurrent), completed));
        }

        IEnumerator RunSwitch(IEnumerator routine, Action<bool> onCompleted = null)
        {
            bool completed = false;
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    object next = null;
                    bool more = false;
                    Exception failure = null;
                    try
                    {
                        more = stack.Peek().MoveNext();
                        if (more) next = stack.Peek().Current;
                    }
                    catch (Exception ex) { failure = ex; }
                    if (failure != null)
                    {
                        if (failure is RemoteAvatarUnavailableException)
                        {
                            LastError = failure.Message;
                            Debug.Log("[ValheimVRM Sync] " + LastError);
                        }
                        else ReportError("Avatar switch failed", failure);
                        yield break;
                    }
                    if (!more) (stack.Pop() as IDisposable)?.Dispose();
                    else if (next is IEnumerator nested) stack.Push(nested);
                    else yield return next;
                }
                completed = true;
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                IsBusy = false;
                onCompleted?.Invoke(completed);
            }
        }

        IEnumerator Switch(Player player, string name, bool localSelection = true, string expectedHash = null, Func<bool> stillCurrent = null)
        {
            Func<bool> valid = () => player != null && !player.IsDead() &&
                (localSelection ? player == Player.m_localPlayer : player != Player.m_localPlayer && stillCurrent != null && stillCurrent());
            if (!valid()) yield break;
            if (!Catalog.TryGetPath(name, out var path))
            {
                if (!localSelection) throw new RemoteAvatarUnavailableException("Missing local VRM: " + name + ". Existing appearance retained.");
                throw new FileNotFoundException("The VRM file was removed.");
            }
            bool cached = VrmManager.VrmDic.TryGetValue(name, out var candidate) && candidate != null && candidate.VisualModel != null;
            byte[] source = null, hash = null;
            if (cached)
            {
                if (expectedHash != null && (!VrmManager.VrmHashes.TryGetValue(name, out var existingHash) ||
                    BitConverter.ToString(existingHash).Replace("-", "").ToLowerInvariant() != expectedHash))
                    throw new RemoteAvatarUnavailableException("Cached VRM differs from the sender: " + name + ". Existing appearance retained; install matching files and restart.");
            }
            else
            {
                // Verify the exact bytes to be imported before touching settings,
                // Unity objects or shared caches. A different/corrupt local file
                // is a normal synchronization miss, not an importer failure.
                var read = Task.Run(() =>
                {
                    var data = File.ReadAllBytes(path);
                    using (var sha = SHA256.Create()) return new KeyValuePair<byte[], byte[]>(data, sha.ComputeHash(data));
                });
                while (!read.IsCompleted) yield return null;
                if (read.IsFaulted)
                {
                    var error = read.Exception.GetBaseException();
                    if (!localSelection && (error is IOException || error is UnauthorizedAccessException))
                        throw new RemoteAvatarUnavailableException("Cannot read local VRM: " + name + ". Existing appearance retained; check the file and refresh the list.");
                    throw error;
                }
                source = read.Result.Key; hash = read.Result.Value;
                if (expectedHash != null && BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant() != expectedHash)
                    throw new RemoteAvatarUnavailableException("VRM file differs from the sender: " + name + ". Existing appearance retained; install matching files and refresh the list.");
            }
            if (!valid()) yield break;
            if (!Settings.ContainsSettings(name)) Settings.AddSettingsFromFile(name, false);
            var settings = Settings.GetSettings(name);
            if (!cached)
            {
                GameObject loaded = null;
                yield return VRM.ImportVisualAsync(source, path, settings.ModelScale, root => loaded = root);
                if (loaded == null) throw new InvalidOperationException("Could not import the VRM. See BepInEx/LogOutput.log.");
                var animator = loaded.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    Destroy(loaded);
                    throw new InvalidOperationException("This VRM does not have a valid humanoid avatar.");
                }
                if (!valid())
                {
                    Destroy(loaded);
                    yield break;
                }
                candidate = VrmManager.RegisterVrm(new VRM(loaded, name), player.GetComponentInChildren<LODGroup>(true), player, hash);
                if (candidate == null) throw new InvalidOperationException("Could not register the VRM.");
                candidate.Src = source;
                candidate.RecalculateSrcBytesHash();
                candidate.RecalculateSettingsHash();
            }
            if (!valid()) yield break;
            if (expectedHash != null && (!VrmManager.VrmHashes.TryGetValue(name, out var cachedHash) ||
                BitConverter.ToString(cachedHash).Replace("-", "").ToLowerInvariant() != expectedHash))
                throw new RemoteAvatarUnavailableException("Cached VRM differs from the sender: " + name + ". Existing appearance retained; install matching files and restart.");
            if (!localSelection)
                (player.GetComponent<RemoteAvatarBaseline>() ?? player.gameObject.AddComponent<RemoteAvatarBaseline>()).Capture();

            VrmManager.PlayerToName.TryGetValue(player, out var priorName);
            bool hadAvatar = VrmManager.PlayerToVrmInstance.TryGetValue(player, out var priorVisual) && priorVisual != null;
            float priorScale = hadAvatar && priorName != null ? Settings.GetSettings(priorName).InteractionDistanceScale : 1f;
            float priorDistance = player.m_maxInteractDistance;
            VrmManager.PlayerToName[player] = name;
            yield return candidate.SetToPlayer(player);
            if (player == null || player.IsDead()) yield break;
            var visual = player.GetComponent<VrmController>()?.visual;
            if (visual == null || visual == priorVisual) throw new InvalidOperationException("Could not attach the selected avatar.");

            // SetToPlayer applies a multiplier; repeated selections must not compound it.
            if (localSelection)
            {
                player.m_maxInteractDistance = priorDistance * settings.InteractionDistanceScale / Mathf.Max(.001f, priorScale);
                Catalog.Select(player.GetPlayerName(), name);
            }
            Debug.Log("[ValheimVRM] " + (localSelection ? "Local" : "Remote") + " avatar selected for player instance " + player.GetInstanceID() + ": " + name);
        }

        void ReportError(string context, Exception ex)
        {
            LastError = context + ": " + ex.Message;
            Debug.LogWarning("[ValheimVRM] " + context + ": " + ex);
        }

        public void SetMenuOpen(bool value)
        {
            if (!value) SavePhysicsOptions();
            MenuOpen = value && Player.m_localPlayer != null && !Player.m_localPlayer.IsDead() && !Player.m_localPlayer.InIntro();
            if (!MenuOpen) return;
            RefreshModels();
            float width = Mathf.Min(500, Screen.width - 20);
            float height = Mathf.Min(640, Screen.height - 20);
            window = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
        }

        void Update()
        {
            if (physicsDirty && GUIUtility.hotControl == 0) SavePhysicsOptions();
            if (Player.m_localPlayer == null || Player.m_localPlayer.IsDead() || !Settings.globalSettings.EnableAvatarPicker)
            {
                MenuOpen = false;
                return;
            }
            if (ZInput.GetKeyDown(KeyCode.F8, false))
            {
                if (MenuOpen) SetMenuOpen(false);
                else if (!InventoryGui.IsVisible() && !Menu.IsVisible() && !global::Console.IsVisible() &&
                    (Chat.instance == null || !Chat.instance.HasFocus())) SetMenuOpen(true);
            }
            if (MenuOpen && ZInput.GetKeyDown(KeyCode.Escape, false)) SetMenuOpen(false);
        }

        void OnGUI()
        {
            if (!MenuOpen) return;
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial", "DejaVu Sans" }, 18);
            var previousFont = GUI.skin.font;
            try
            {
                GUI.skin.font = font;
                window = GUI.Window(0x56524d38, window, DrawWindow, Text("VRM avatars · F8", "VRM 人物外观 · F8"));
            }
            finally { GUI.skin.font = previousFont; }
        }

        void DrawWindow(int id)
        {
            var label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleLeft, richText = false };
            GUILayout.Space(8);
            GUILayout.Label(Text("Choose a local avatar. Your equipped items remain equipped.", "切换本机显示的模型，保留当前穿戴的装备。"), label);
            string current = "";
            if (Player.m_localPlayer != null) VrmManager.PlayerToName.TryGetValue(Player.m_localPlayer, out current);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            GUI.enabled = !IsBusy;
            foreach (var name in Catalog.Names)
            {
                var display = name == "___Default" ? Text("Default avatar", "默认模型") : name;
                if (GUILayout.Button((name == current ? "✓  " : "    ") + display, button, GUILayout.Height(36))) RequestSwitch(name);
            }
            GUI.enabled = true;
            GUILayout.EndScrollView();
            if (Catalog.Names.Length == 0)
                GUILayout.Label(Text("Add .vrm files to the ValheimVRM folder beside valheim.exe, then refresh.", "将 .vrm 放入 valheim.exe 旁的 ValheimVRM 文件夹，然后刷新列表。"), label);
            GUILayout.Label(IsBusy ? Text("Loading: ", "正在载入：") + loadingName : LastError, label);
            DrawRenderingOptions();
            DrawPhysicsOptions();
            DrawSyncOptions();
            GUILayout.BeginHorizontal();
            GUI.enabled = !IsBusy;
            if (GUILayout.Button(Text("Refresh list", "刷新列表"), GUILayout.Height(30))) RefreshModels();
            GUI.enabled = true;
            if (GUILayout.Button(Text("Close · Esc", "关闭 · Esc"), GUILayout.Height(30))) SetMenuOpen(false);
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, window.width, 25));
        }

        void DrawRenderingOptions()
        {
            var current = AvatarRendering.Current;
            GUILayout.Label(Text("Rendering · VRM 1.0 MToon", "渲染设置 · VRM 1.0 MToon"));
            GUI.enabled = AvatarRendering.OptionsShader != null;
            bool lighting = GUILayout.Toggle(current.SceneLighting, Text("Scene lighting", "场景光照"));
            GUI.enabled = lighting && AvatarRendering.OptionsShader != null;
            bool shadows = GUILayout.Toggle(current.ReceiveShadows, Text("Receive shadows", "接收阴影"));
            GUI.enabled = true;
            bool bloom = GUILayout.Toggle(current.Bloom, Text("Avatar bloom", "模型泛光"));
            if (lighting != current.SceneLighting || shadows != current.ReceiveShadows || bloom != current.Bloom)
            {
                try { AvatarRendering.Set(lighting, shadows, bloom); }
                catch (Exception ex) { ReportError("Cannot save rendering options", ex); }
            }
        }

        void DrawPhysicsOptions()
        {
            GUILayout.Label(Text("Physics sway weight", "物理摆动权重") + "  " + Mathf.RoundToInt(AvatarPhysics.Weight * 100) + "%");
            float weight = Mathf.Round(GUILayout.HorizontalSlider(AvatarPhysics.Weight, 0, 1) * 100) / 100;
            if (!Mathf.Approximately(weight, AvatarPhysics.Weight))
            {
                AvatarPhysics.Preview(weight);
                physicsDirty = true;
            }
            GUILayout.Label(Text("0%: no sway · 100%: original physics · Default: 50%", "0%：不摆动 · 100%：原始物理 · 默认：50%"));
        }

        void DrawSyncOptions()
        {
            var sync = AvatarSyncClient.Instance;
            if (sync == null) return;
            bool value = GUILayout.Toggle(sync.SyncEnabled, Text("Server avatar sync (when available)", "服务器外观同步（服务器支持时）"));
            if (value != sync.SyncEnabled) sync.SetEnabled(value);
            GUILayout.Label(sync.Connected && sync.SyncEnabled
                ? Text("Server sync connected · Each player's choice is independent", "服务器同步已连接 · 每位玩家独立选择")
                : Text("Local mode · Your choice stays on this computer", "本地模式 · 切换仅在本机生效"));
            if (!string.IsNullOrEmpty(sync.LastError)) GUILayout.Label(sync.LastError, new GUIStyle(GUI.skin.label) { wordWrap = true });
        }

        void SavePhysicsOptions()
        {
            if (!physicsDirty) return;
            physicsDirty = false;
            try { AvatarPhysics.Save(); }
            catch (Exception ex) { ReportError("Cannot save physics options", ex); }
        }

        void OnDestroy()
        {
            SavePhysicsOptions();
            if (font != null) Destroy(font);
            if (Instance == this) Instance = null;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsVisible))]
    static class OutfitMenuInputBlock
    {
        static void Postfix(ref bool __result)
        {
            if (OutfitSwitcher.Instance != null && OutfitSwitcher.Instance.MenuOpen) __result = true;
        }
    }
}
