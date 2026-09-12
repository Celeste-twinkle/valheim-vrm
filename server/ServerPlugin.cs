using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using ValheimVRM.Sync;

namespace ValheimVRM.Server
{
    [BepInPlugin(AvatarSyncWire.ServerGuid, "ValheimVRM Server Sync", "1.8.2")]
    public sealed class ServerPlugin : BaseUnityPlugin
    {
        sealed class Session
        {
            public ZNetPeer Peer;
            public bool Handshake, Enabled;
            public string Model = "", Hash = "";
            public float NextHello;
            public int HelloAttempts;
            public long SentRevision = -1;
        }
        readonly Dictionary<ZRpc, Session> sessions = new Dictionary<ZRpc, Session>();
        AvatarSyncRegistry registry = new AvatarSyncRegistry();
        ConfigEntry<bool> syncEnabled;
        ZNet network;
        float nextPoll;
        bool hostEnabled;
        string hostModel = "", hostHash = "";
        public bool SyncAvailable => syncEnabled != null && syncEnabled.Value;

        void Awake()
        {
            syncEnabled = Config.Bind("Sync", "Enabled", true, "Relay per-player avatar selections. No VRM files or shaders are loaded by the server.");
            Logger.LogInfo("Avatar sync server 1.8.2 ready; player identity uses authenticated peer and character ZDO IDs.");
        }

        void Update()
        {
            if (Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + .2f;
            var net = ResetNetworkIfNeeded();
            if (net == null || !net.IsServer()) return;
            var peers = net.GetPeers().Where(p => p.IsReady()).ToArray();
            foreach (var stale in sessions.Keys.Where(r => !peers.Any(p => p.m_rpc == r)).ToArray())
            { registry.Remove(sessions[stale].Peer.m_uid); sessions.Remove(stale); }
            foreach (var peer in peers)
            {
                if (!sessions.TryGetValue(peer.m_rpc, out var s))
                {
                    s = new Session { Peer = peer }; sessions.Add(peer.m_rpc, s);
                    peer.m_rpc.Register<ZPackage>(AvatarSyncWire.Select, ReceiveSelection);
                }
                // Vanilla clients silently ignore unknown ZRpc methods. Discovery
                // is bounded, and no snapshot is sent until a client opts in.
                // Missing the addon never changes admission or disconnects a peer.
                if (!s.Handshake && s.HelloAttempts < 3 && Time.realtimeSinceStartup >= s.NextHello)
                {
                    s.HelloAttempts++;
                    s.NextHello = Time.realtimeSinceStartup + 3;
                    peer.m_rpc.Invoke(AvatarSyncWire.Hello, syncEnabled.Value ? AvatarSyncRules.Version : 0);
                }
                UpdateCharacter(peer.m_uid, peer.m_characterID, syncEnabled.Value && s.Enabled, s.Model, s.Hash);
            }
            UpdateCharacter(ZNet.GetUID(), net.LocalPlayerCharacterID, syncEnabled.Value && hostEnabled, hostModel, hostHash);
            foreach (var s in sessions.Values)
                if (s.Handshake && s.SentRevision != registry.Revision)
                {
                    s.Peer.m_rpc.Invoke(AvatarSyncWire.State, AvatarSyncWire.Snapshot(registry.Revision, registry.Snapshot()));
                    s.SentRevision = registry.Revision;
                }
        }

        ZNet ResetNetworkIfNeeded()
        {
            var net = ZNet.instance;
            if (network != net)
            {
                sessions.Clear(); registry = new AvatarSyncRegistry(); network = net;
                hostModel = hostHash = ""; hostEnabled = false;
            }
            return net;
        }

        void ReceiveSelection(ZRpc rpc, ZPackage package)
        {
            if (network == null || !network.IsServer() || !sessions.TryGetValue(rpc, out var s) || !s.Peer.IsReady()) return;
            if (!AvatarSyncWire.ReadSelection(package, out var accept, out var model, out var hash)) return;
            // Coalesce rapid changes into the most recent request. Applying state is
            // limited by Update's 5 Hz cadence; a client cannot name another player.
            s.Handshake = true; s.Enabled = accept; s.Model = model; s.Hash = hash;
            s.SentRevision = -1;
        }

        void UpdateCharacter(long peer, ZDOID character, bool accept, string model, string hash)
        {
            var zdo = character == ZDOID.None ? null : ZDOMan.instance?.GetZDO(character);
            if (!accept || model == "" || zdo == null || zdo.GetOwner() != peer)
            { registry.Remove(peer); return; }
            registry.Set(peer, character.UserID, character.ID, model, hash);
        }

        // Optional listen-server bridge. These public methods use only game/BCL
        // types so the client does not require a reference to the server assembly.
        public void SetHostSelection(bool accept, string model, string hash)
        {
            // The client bridge may run before our first Update after joining.
            // Reset first, so the next server tick cannot erase that initial choice.
            var net = ResetNetworkIfNeeded();
            if (net == null || !net.IsServer()) return;
            if (model != "" && (!AvatarSyncRules.ValidModel(model) || !AvatarSyncRules.ValidHash(hash))) return;
            hostEnabled = accept; hostModel = model; hostHash = hash;
        }
        public ZPackage ReadHostSnapshot()
        {
            ResetNetworkIfNeeded();
            var package = AvatarSyncWire.Snapshot(registry.Revision, registry.Snapshot());
            package.SetPos(0); return package;
        }
    }
}
