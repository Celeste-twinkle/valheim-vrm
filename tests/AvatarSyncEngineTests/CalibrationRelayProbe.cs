using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using ValheimVRM.Sync;

static class CalibrationRelayProbe
{
    public static string Data(float value, int entries = 1)
    {
        var data = new AvatarCalibrationData { Standing = value, Sitting = -value, Physics = .5f + value };
        data.Left.Scale = 1 + value; data.Right.Scale = 1 - value; data.TwoHanded.Scale = 1 + value * 2;
        data.Left.Offset = new AvatarCalibrationData.Position(value, 0, -value);
        data.Right.Offset = new AvatarCalibrationData.Position(0, value, -value);
        data.TwoHanded.Offset = new AvatarCalibrationData.Position(-value, value, 0);
        if (entries == 1) data.Back = new AvatarCalibrationData.Item { Scale = 1 + value,
            Offset = new AvatarCalibrationData.Position(-value, value, value) };
        for (int i = 0; i < entries; i++)
            data.Animations["Base Layer.Test " + i.ToString("D4") + (entries > 1 ? new string('x', 100) : "")] =
                new AvatarCalibrationData.Position(value, -value, value);
        return AvatarCalibrationCodec.Encode(data);
    }
    public static void Run(string model, string hash)
    {
        var plugin = Chainloader.PluginInfos[AvatarSyncWire.ServerGuid].Instance;
        var tick = AccessTools.Method(plugin.GetType(), "Update");
        Action update = () => { AccessTools.Field(plugin.GetType(), "nextPoll").SetValue(plugin, 0f); tick.Invoke(plugin, null); };
        var netField = AccessTools.Field(typeof(ZNet), "m_instance"); var serverField = AccessTools.Field(typeof(ZNet), "m_isServer");
        var zdoField = AccessTools.Field(typeof(ZDOMan), "s_instance");
        var oldNet = netField.GetValue(null); var oldServer = serverField.GetValue(null); var oldZdo = zdoField.GetValue(null);
        var holder = new GameObject("CalibrationRelayFixture"); holder.SetActive(false); var net = holder.AddComponent<ZNet>();
        var manager = (ZDOMan)FormatterServices.GetUninitializedObject(typeof(ZDOMan));
        var objects = new Dictionary<ZDOID, ZDO>();
        AccessTools.Field(typeof(ZDOMan), "m_objectsByID").SetValue(manager, objects);
        AccessTools.Field(typeof(ZDOMan), "m_sessionID").SetValue(manager, 900L);
        var peers = (List<ZNetPeer>)AccessTools.Field(typeof(ZNet), "m_peers").GetValue(net);
        var clients = new Dictionary<long, ZRpc>(); var sockets = new Dictionary<long, ServerRelayProbe.RelaySocket>();
        var inboxes = new Dictionary<long, ServerRelayProbe.RelaySocket>();
        var received = new Dictionary<long, AvatarSelection[]>();
        int chunkPackets = 0;
        string a = Data(.12f), b = Data(-.2f), newer = Data(.3f), large = Data(.08f, 768);
        try
        {
            netField.SetValue(null, net); serverField.SetValue(null, true); zdoField.SetValue(null, manager);
            Action<ZDOID,long> addZdo = (id, uid) => {
                var zdo = new ZDO { m_uid = id }; AccessTools.Field(typeof(ZDO), "m_prefab").SetValue(zdo, 1);
                zdo.SetOwnerInternal(uid); objects[id] = zdo;
            };
            Action<long,bool> connect = (uid, calibrated) => {
                var left = new ServerRelayProbe.RelaySocket(); var right = new ServerRelayProbe.RelaySocket(); left.Other = right; right.Other = left;
                var client = new ZRpc(left); clients[uid] = client; sockets[uid] = right; inboxes[uid] = left;
                var peer = new ZNetPeer(right, false) { m_uid = uid, m_characterID = new ZDOID(uid + 1000, 1), m_playerName = "Same name" };
                addZdo(peer.m_characterID, uid); peers.Add(peer);
                long revision = -1; var assembly = new AvatarSnapshotAssembly();
                Action<ZPackage> accept = p => {
                    Check(AvatarSyncWire.ReadSnapshot(p, out var next, out var states, out var height, out var calibration), "Invalid calibration snapshot");
                    Check(height && calibration == calibrated, "Wrong capability snapshot");
                    if (next > revision) { received[uid] = states; revision = next; }
                };
                client.Register<int>(calibrated ? AvatarSyncWire.CalibrationHello : AvatarSyncWire.HeightHello, (rpc, version) => {
                    Check(version == 1, "Invalid calibration hello");
                    rpc.Invoke(AvatarSyncWire.Select, calibrated
                        ? AvatarSyncWire.Selection(true, model, hash, 1, uid == 101 ? 1.4f : 2, uid == 101 ? a : b)
                        : AvatarSyncWire.Selection(true, model, hash, 1, 2));
                });
                client.Register<ZPackage>(AvatarSyncWire.State, (rpc, p) => accept(p));
                client.Register<ZPackage>(AvatarSyncWire.StateChunk, (rpc, p) => {
                    chunkPackets++;
                    if (assembly.Add(p, revision, out var complete)) accept(complete);
                });
            };
            Action pump = () => {
                update(); foreach (var c in clients.Values) c.Update(.01f);
                foreach (var p in peers) p.m_rpc.Update(.01f); update();
                foreach (var socket in inboxes.Values) socket.ReversePending();
                foreach (var c in clients.Values) c.Update(.01f);
            };
            connect(101, true); connect(202, true); connect(404, false); pump();
            Check(received[202].Single(s => s.Peer == 101).Calibration == a && received[101].Single(s => s.Peer == 202).Calibration == b, "Players' calibration crossed");
            clients[101].Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, model, hash, 2, 2, b));
            clients[101].Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, model, hash, 3, 1.4f, newer));
            sockets[101].ReversePending(); pump();
            Check(received[202].Single(s => s.Peer == 101).Calibration == newer && received[202].Single(s => s.Peer == 101).Height == 1.4f, "Reordered calibration/height request won");
            var malformed = AvatarSyncWire.Selection(true, model, hash, long.MaxValue, 2); malformed.Write("not base64");
            clients[101].Invoke(AvatarSyncWire.Select, malformed);
            clients[101].Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, model, hash, 99, 2));
            clients[101].Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, model, hash, 4, 1.4f, large)); pump();
            Check(received[202].Single(s => s.Peer == 101).Calibration == large && chunkPackets > 0, "Malformed/downgraded packet advanced sequence or chunks failed");
            Check(received[404].All(s => s.Calibration == null), "Calibration leaked to an old client");
            connect(303, true); pump();
            Check(received[303].Single(s => s.Peer == 101).Calibration == large, "Late join missed latest calibration");
            var respawnId = new ZDOID(1101, 99); peers[0].m_characterID = respawnId; addZdo(respawnId, 101); pump();
            Check(received[202].Single(s => s.Peer == 101).CharacterId == 99 && received[202].Single(s => s.Peer == 101).Calibration == large, "Respawn lost calibration");
            clients[101].Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, "", "", 5, 2, a));
            clients[101].Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, model, hash, 4, 2, a)); pump();
            Check(received[202].All(s => s.Peer != 101), "Old calibration revived a player who chose the native model");
            var hostId = new ZDOID(900, 1); addZdo(hostId, 900); AccessTools.Field(typeof(ZNet), "m_characterID").SetValue(net, hostId);
            var host = AccessTools.Method(plugin.GetType(), "SetHostSelectionWithCalibration");
            host.Invoke(plugin, new object[] { 2L, true, model, hash, 2f, newer });
            host.Invoke(plugin, new object[] { 1L, true, model, hash, 2f, a }); pump();
            Check(received[202].Single(s => s.Peer == 900).Calibration == newer, "Listen-host calibration ordering failed");
            CheckChunks(large, model, hash);
        }
        finally
        {
            netField.SetValue(null, oldNet); serverField.SetValue(null, oldServer); zdoField.SetValue(null, oldZdo);
            foreach (var client in clients.Values) client.Dispose(); foreach (var peer in peers) peer.Dispose();
        }
    }
    static void CheckChunks(string large, string model, string hash)
    {
        var states = Enumerable.Range(1, 4).Select(i => new AvatarSelection { Peer = i, CharacterUser = i, CharacterId = 1, Model = model, Sha256 = hash, Calibration = large }).ToArray();
        var first = AvatarSnapshotChunks.Split(10, AvatarSyncWire.Snapshot(10, states, true, true)).ToArray();
        var second = AvatarSnapshotChunks.Split(11, AvatarSyncWire.Snapshot(11, states, true, true)).Reverse().ToArray();
        var receiver = new AvatarSnapshotAssembly(); ZPackage completed;
        first[0].SetPos(0); Check(!receiver.Add(first[0], -1, out completed), "Partial snapshot applied");
        second[0].SetPos(0); Check(!receiver.Add(second[0], -1, out completed), "Partial newer snapshot applied");
        foreach (var p in first) { p.SetPos(0); Check(!receiver.Add(p, -1, out completed), "Older chunks completed over newer revision"); }
        int finished = 0;
        foreach (var p in second) { p.SetPos(0); if (receiver.Add(p, -1, out completed)) finished++; }
        Check(finished == 1, "Duplicate/out-of-order chunks did not complete exactly once");
        var malformed = new ZPackage(); malformed.Write(12L); malformed.Write(0); malformed.Write(1); malformed.Write(10); malformed.Write(int.MaxValue); malformed.SetPos(0);
        Check(!receiver.Add(malformed, 11, out _), "Oversized chunk allocation accepted");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
