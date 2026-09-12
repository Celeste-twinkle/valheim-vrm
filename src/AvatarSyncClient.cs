using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ValheimVRM.Sync;

namespace ValheimVRM
{
    public sealed class AvatarSyncClient : MonoBehaviour
    {
        public static AvatarSyncClient Instance { get; private set; }
        public bool Connected { get; private set; }
        public bool SyncEnabled => syncEnabled.Value;
        public string LastError { get; private set; } = "";
        ConfigEntry<bool> syncEnabled;
        ZNet network;
        ZRpc server;
        object hostPlugin;
        MethodInfo hostSubmit, hostRead;
        long revision = -1;
        float nextPoll;
        string lastSent;
        AvatarSelection[] states = new AvatarSelection[0];
        readonly Dictionary<Player, AvatarSelection> applied = new Dictionary<Player, AvatarSelection>();
        readonly Dictionary<Player, AvatarSelection> failed = new Dictionary<Player, AvatarSelection>();
        readonly HashSet<ZRpc> registered = new HashSet<ZRpc>();

        public void Initialize(ConfigEntry<bool> setting) { syncEnabled = setting; Instance = this; }
        public void SetEnabled(bool value)
        {
            syncEnabled.Value = value; lastSent = null; failed.Clear();
            if (!value) LastError = "";
        }
        public void RetryMissing() { failed.Clear(); LastError = ""; }

        public void Register(ZNetPeer peer)
        {
            if (peer?.m_rpc == null || !registered.Add(peer.m_rpc)) return;
            peer.m_rpc.Register<int>(AvatarSyncWire.Hello, ReceiveHello);
            peer.m_rpc.Register<ZPackage>(AvatarSyncWire.State, ReceiveState);
        }
        void ReceiveHello(ZRpc rpc, int version)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer() || ZNet.instance.GetServerPeer()?.m_rpc != rpc) return;
            ResetConnectionIfNeeded();
            Connected = version == AvatarSyncRules.Version; server = rpc; lastSent = null;
            if (!Connected) LastError = "Server sync protocol is unavailable or incompatible.";
            SendSelection();
        }
        void ReceiveState(ZRpc rpc, ZPackage package)
        {
            if (!Connected || server != rpc || ZNet.instance?.GetServerPeer()?.m_rpc != rpc) return;
            AcceptSnapshot(package);
        }
        void AcceptSnapshot(ZPackage package)
        {
            if (!AvatarSyncWire.ReadSnapshot(package, out var next, out var selections) || next < revision) return;
            revision = next; states = selections;
        }
        void ResetConnectionIfNeeded()
        {
            var net = ZNet.instance;
            var rpc = net != null && !net.IsServer() ? net.GetServerPeer()?.m_rpc : null;
            if (network == net && (net == null || net.IsServer() || server == rpc)) return;
            network = net; server = rpc; Connected = false; revision = -1;
            states = new AvatarSelection[0]; lastSent = null; hostPlugin = null; failed.Clear(); LastError = "";
            // Keep only live registrations so reconnects cannot retain stale sockets.
            registered.RemoveWhere(r => net == null || !net.GetPeers().Any(p => p.m_rpc == r));
            if (net != null && net.IsServer() && Chainloader.PluginInfos.TryGetValue(AvatarSyncWire.ServerGuid, out var plugin))
            {
                hostPlugin = plugin.Instance;
                hostSubmit = hostPlugin.GetType().GetMethod("SetHostSelection");
                hostRead = hostPlugin.GetType().GetMethod("ReadHostSnapshot");
                Connected = hostSubmit != null && hostRead != null &&
                    hostPlugin.GetType().GetProperty("SyncAvailable")?.GetValue(hostPlugin, null) is bool available && available;
            }
        }
        void Update()
        {
            if (syncEnabled == null || Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + .25f;
            ResetConnectionIfNeeded();
            if (network != null && !network.IsServer())
                foreach (var peer in network.GetPeers()) Register(peer);
            if (Connected)
            {
                SendSelection();
                if (hostPlugin != null) AcceptSnapshot((ZPackage)hostRead.Invoke(hostPlugin, null));
            }
            ApplyRemotePlayers();
        }
        void SendSelection()
        {
            if (!Connected || syncEnabled == null) return;
            string model = "", hash = "";
            var player = Player.m_localPlayer;
            if (SyncEnabled && player != null && !player.IsDead() && OutfitSwitcher.Instance != null &&
                !OutfitSwitcher.Instance.IsBusy && !VrmManager.LoadingPlayers.Contains(player) &&
                VrmManager.PlayerToName.TryGetValue(player, out var name) &&
                VrmManager.PlayerToVrmInstance.TryGetValue(player, out var visual) && visual != null &&
                VrmManager.VrmHashes.TryGetValue(name, out var bytes))
            { model = name; hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
            // During a model load/death keep the previous choice for the server to
            // rebind to the new character ID. Explicit opt-out sends an empty choice.
            if (SyncEnabled && model == "" && lastSent != null) return;
            string token = SyncEnabled + ":" + model + ":" + hash;
            if (token == lastSent) return;
            if (hostPlugin != null) hostSubmit.Invoke(hostPlugin, new object[] { SyncEnabled, model, hash });
            else server?.Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(SyncEnabled, model, hash));
            lastSent = token;
        }
        AvatarSelection Desired(Player player)
        {
            if (!Connected || !SyncEnabled || player == null || player == Player.m_localPlayer || player.IsDead()) return null;
            var view = player.GetComponent<ZNetView>(); var zdo = view?.GetZDO();
            if (zdo == null || view.IsOwner()) return null;
            var id = zdo.m_uid;
            return states.FirstOrDefault(s => s.CharacterUser == id.UserID && s.CharacterId == id.ID && s.Peer == zdo.GetOwner());
        }
        void ApplyRemotePlayers()
        {
            foreach (var stale in applied.Keys.Where(p => p == null).ToArray()) applied.Remove(stale);
            foreach (var stale in failed.Keys.Where(p => p == null).ToArray()) failed.Remove(stale);
            foreach (var player in Player.GetAllPlayers())
            {
                if (player == Player.m_localPlayer || player.IsDead()) continue;
                var desired = Desired(player);
                if (desired == null)
                {
                    failed.Remove(player);
                    if (applied.Remove(player)) RemoteAvatarBaseline.Restore(player);
                    continue;
                }
                if (applied.TryGetValue(player, out var current) && current.SameAs(desired)) continue;
                if (failed.TryGetValue(player, out var bad) && bad.SameAs(desired)) continue;
                var picker = OutfitSwitcher.Instance;
                if (picker == null || picker.IsBusy || VrmManager.LoadingPlayers.Contains(player)) continue;
                if (!picker.Catalog.TryGetPath(desired.Model, out _))
                {
                    failed[player] = desired; LastError = "Missing local VRM: " + desired.Model + ". Existing appearance retained; add the file and refresh the list.";
                    Debug.Log("[ValheimVRM Sync] " + LastError); continue;
                }
                var target = player; var selection = desired;
                Func<bool> stillCurrent = () => target != null && selection.SameAs(Desired(target));
                picker.RequestRemoteSwitch(target, selection.Model, selection.Sha256, stillCurrent, success =>
                {
                    if (target == null) return;
                    // Track the attached player even if another selection arrived
                    // during import; the next poll applies its newest state.
                    if (success && VrmManager.PlayerToName.TryGetValue(target, out var name) && name == selection.Model &&
                        VrmManager.PlayerToVrmInstance.TryGetValue(target, out var visual) && visual != null) applied[target] = selection;
                    if (!success && stillCurrent())
                    { failed[target] = selection; LastError = picker.LastError; }
                });
                break; // Serialize imports so shared model caches cannot race.
            }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    static class AvatarSyncRegisterPeer
    {
        static void Postfix(ZNetPeer peer) { AvatarSyncClient.Instance?.Register(peer); }
    }
}
