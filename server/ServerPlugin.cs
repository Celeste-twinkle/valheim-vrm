using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using ValheimVRM.Sync;

namespace ValheimVRM.Server
{
    [BepInPlugin(AvatarSyncWire.ServerGuid, "ValheimVRM Server Sync", "2.0.0")]
    public sealed class ServerPlugin : BaseUnityPlugin
    {
        sealed class Session
        {
            public ZNetPeer Peer;
            public bool Handshake, Enabled;
            public bool HeightSupport;
            public bool CalibrationSupport;
            public string Calibration;
            public float Height = AvatarHeightRules.Default;
            public string Model = "", Hash = "";
            public float NextHello;
            public int HelloAttempts;
            public long SentRevision = -1;
            public readonly AvatarRequestOrder Order = new AvatarRequestOrder();
        }
        readonly Dictionary<ZRpc, Session> sessions = new Dictionary<ZRpc, Session>();
        AvatarSyncRegistry registry = new AvatarSyncRegistry();
        ConfigEntry<bool> syncEnabled;
        ZNet network;
        float nextPoll;
        bool hostEnabled;
        bool hostHeightSupport;
        bool hostCalibrationSupport;
        string hostCalibration;
        float hostHeight = AvatarHeightRules.Default;
        string hostModel = "", hostHash = "";
        AvatarRequestOrder hostOrder = new AvatarRequestOrder();
        public bool SyncAvailable => syncEnabled != null && syncEnabled.Value;

        void Awake()
        {
            syncEnabled = Config.Bind("Sync", "Enabled", true, "Relay per-player avatar selections. No VRM files or shaders are loaded by the server.");
            Logger.LogInfo("Avatar sync server 2.0.0 ready; model, height, calibration and optional part visibility use authenticated peer and character ZDO IDs.");
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
                    int version = syncEnabled.Value ? AvatarSyncRules.Version : 0;
                    peer.m_rpc.Invoke(AvatarSyncWire.CalibrationHello, version);
                    peer.m_rpc.Invoke(AvatarSyncWire.HeightHello, version);
                    peer.m_rpc.Invoke(AvatarSyncWire.SequencedHello, version);
                    peer.m_rpc.Invoke(AvatarSyncWire.Hello, version);
                }
                UpdateCharacter(peer.m_uid, peer.m_characterID, syncEnabled.Value && s.Enabled, s.Model, s.Hash, s.Height, s.Calibration);
            }
            UpdateCharacter(ZNet.GetUID(), net.LocalPlayerCharacterID, syncEnabled.Value && hostEnabled, hostModel, hostHash, hostHeight, hostCalibration);
            foreach (var s in sessions.Values)
                if (s.Handshake && s.SentRevision != registry.Revision)
                {
                    var snapshot = AvatarSyncWire.Snapshot(registry.Revision, registry.Snapshot(), s.HeightSupport, s.CalibrationSupport);
                    if (s.CalibrationSupport && snapshot.Size() > AvatarSnapshotChunks.PayloadSize)
                        foreach (var part in AvatarSnapshotChunks.Split(registry.Revision, snapshot)) s.Peer.m_rpc.Invoke(AvatarSyncWire.StateChunk, part);
                    else s.Peer.m_rpc.Invoke(AvatarSyncWire.State, snapshot);
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
                hostHeight = AvatarHeightRules.Default; hostHeightSupport = false;
                hostCalibration = null; hostCalibrationSupport = false;
                hostOrder = new AvatarRequestOrder();
            }
            return net;
        }

        void ReceiveSelection(ZRpc rpc, ZPackage package)
        {
            var net = ResetNetworkIfNeeded();
            if (net == null || !net.IsServer() || !sessions.TryGetValue(rpc, out var s) ||
                !s.Peer.IsReady() || !net.GetPeers().Contains(s.Peer)) return;
            if (!AvatarSyncWire.ReadSelection(package, out var accept, out var model, out var hash, out var sequence, out var height, out var withHeight, out var calibration) ||
                (s.HeightSupport && !withHeight) || (s.CalibrationSupport && calibration == null) ||
                !s.Order.TryAccept(sequence)) return;
            // Validate the entire packet before advancing the connection's order.
            // Keep its watermark across respawn and opt-out; reset only on a new
            // connection. Rejected requests never change state or its revision.
            s.Handshake = true; s.Enabled = accept; s.Model = model; s.Hash = hash;
            s.Height = height; s.HeightSupport = withHeight;
            s.Calibration = calibration; s.CalibrationSupport = calibration != null;
            s.SentRevision = -1;
        }

        void UpdateCharacter(long peer, ZDOID character, bool accept, string model, string hash, float height, string calibration)
        {
            var zdo = character == ZDOID.None ? null : ZDOMan.instance?.GetZDO(character);
            if (!accept || model == "" || zdo == null || zdo.GetOwner() != peer)
            { registry.Remove(peer); return; }
            registry.Set(peer, character.UserID, character.ID, model, hash, height, calibration);
        }

        // Optional listen-server bridge. These public methods use only game/BCL
        // types so the client does not require a reference to the server assembly.
        public void SetHostSelection(bool accept, string model, string hash)
        {
            ApplyHostSelection(0, accept, model, hash);
        }
        public void SetHostSelectionSequenced(long sequence, bool accept, string model, string hash)
        {
            if (sequence > 0) ApplyHostSelection(sequence, accept, model, hash);
        }
        public void SetHostSelectionWithHeight(long sequence, bool accept, string model, string hash, float height)
        {
            if (sequence > 0) ApplyHostSelection(sequence, accept, model, hash, height, true);
        }
        public void SetHostSelectionWithCalibration(long sequence, bool accept, string model, string hash, float height, string calibration)
        {
            if (sequence > 0 && calibration != null) ApplyHostSelection(sequence, accept, model, hash, height, true, calibration);
        }
        void ApplyHostSelection(long sequence, bool accept, string model, string hash, float height = AvatarHeightRules.Default, bool withHeight = false, string calibration = null)
        {
            // The client bridge may run before our first Update after joining.
            // Reset first, so the next server tick cannot erase that initial choice.
            var net = ResetNetworkIfNeeded();
            if (net == null || !net.IsServer()) return;
            if (!(model == "" && hash == "" || AvatarSyncRules.ValidModel(model) && AvatarSyncRules.ValidHash(hash)) ||
                !AvatarHeightRules.Valid(height) || (hostHeightSupport && !withHeight) ||
                (hostCalibrationSupport && calibration == null) || (calibration != null && !AvatarCalibrationCodec.TryDecode(calibration, out _)) ||
                !hostOrder.TryAccept(sequence)) return;
            hostEnabled = accept; hostModel = model; hostHash = hash;
            hostHeight = height; hostHeightSupport = withHeight;
            hostCalibration = calibration; hostCalibrationSupport = calibration != null;
        }
        public ZPackage ReadHostSnapshot()
        {
            ResetNetworkIfNeeded();
            var package = AvatarSyncWire.Snapshot(registry.Revision, registry.Snapshot());
            package.SetPos(0); return package;
        }
        public ZPackage ReadHostSnapshotWithHeight()
        {
            ResetNetworkIfNeeded();
            var package = AvatarSyncWire.Snapshot(registry.Revision, registry.Snapshot(), true);
            package.SetPos(0); return package;
        }
        public ZPackage ReadHostSnapshotWithCalibration()
        {
            ResetNetworkIfNeeded();
            var package = AvatarSyncWire.Snapshot(registry.Revision, registry.Snapshot(), true, true);
            package.SetPos(0); return package;
        }
    }
}
