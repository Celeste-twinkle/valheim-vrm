using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using ValheimVRM.Sync;

static class SequencedRelayProbe
{
    public static void Run(string[] models, string[] hashes)
    {
        var plugin = Chainloader.PluginInfos[AvatarSyncWire.ServerGuid].Instance; plugin.enabled = false;
        var tick = AccessTools.Method(plugin.GetType(), "Update");
        Action update = () => { AccessTools.Field(plugin.GetType(), "nextPoll").SetValue(plugin, 0f); tick.Invoke(plugin, null); };
        var netField = AccessTools.Field(typeof(ZNet), "m_instance"); var serverField = AccessTools.Field(typeof(ZNet), "m_isServer");
        var zdoField = AccessTools.Field(typeof(ZDOMan), "s_instance");
        var oldNet = netField.GetValue(null); var oldServer = serverField.GetValue(null); var oldZdo = zdoField.GetValue(null);
        var holder = new GameObject("SequencedServerFixture"); holder.SetActive(false); var net = holder.AddComponent<ZNet>();
        var manager = (ZDOMan)FormatterServices.GetUninitializedObject(typeof(ZDOMan));
        var objects = new Dictionary<ZDOID, ZDO>();
        AccessTools.Field(typeof(ZDOMan), "m_objectsByID").SetValue(manager, objects);
        AccessTools.Field(typeof(ZDOMan), "m_sessionID").SetValue(manager, 900L);
        var peers = (List<ZNetPeer>)AccessTools.Field(typeof(ZNet), "m_peers").GetValue(net);
        var clients = new List<ZRpc>(); var received = new Dictionary<long, AvatarSelection[]>();
        var sockets = new Dictionary<long, ServerRelayProbe.RelaySocket>(); var byPeer = new Dictionary<long, ZRpc>();
        try
        {
            netField.SetValue(null, net); serverField.SetValue(null, true); zdoField.SetValue(null, manager);
            Action<ZDOID, long> addZdo = (id, owner) => {
                var zdo = new ZDO { m_uid = id }; AccessTools.Field(typeof(ZDO), "m_prefab").SetValue(zdo, 1);
                zdo.SetOwnerInternal(owner); objects[id] = zdo;
            };
            var hostId = new ZDOID(900, 9); addZdo(hostId, 900); AccessTools.Field(typeof(ZNet), "m_characterID").SetValue(net, hostId);
            var host = AccessTools.Method(plugin.GetType(), "SetHostSelectionSequenced");
            var legacyHost = AccessTools.Method(plugin.GetType(), "SetHostSelection");
            var readHost = AccessTools.Method(plugin.GetType(), "ReadHostSnapshot");
            Func<AvatarSelection[]> state = () => {
                Check(AvatarSyncWire.ReadSnapshot((ZPackage)readHost.Invoke(plugin, null), out _, out var s), "Server state decode failed"); return s;
            };
            Func<long> revision = () => {
                Check(AvatarSyncWire.ReadSnapshot((ZPackage)readHost.Invoke(plugin, null), out var r, out _), "Server revision decode failed"); return r;
            };
            host.Invoke(plugin, new object[] { 2L, true, models[1], hashes[1] });
            host.Invoke(plugin, new object[] { 1L, true, models[0], hashes[0] });
            legacyHost.Invoke(plugin, new object[] { false, "", "" }); update();
            Check(state().Single().Model == models[1], "Host stale/legacy request replaced newer choice");
            host.Invoke(plugin, new object[] { 3L, false, "", "" }); update();
            Check(state().Length == 0, "Host opt-out did not use ordered requests");

            Func<long, uint, bool, int, ZNetPeer> add = (uid, id, sequenced, modelIndex) => {
                var left = new ServerRelayProbe.RelaySocket(); var right = new ServerRelayProbe.RelaySocket(); left.Other = right; right.Other = left;
                var client = new ZRpc(left); clients.Add(client); byPeer[uid] = client; sockets[uid] = right;
                var peer = new ZNetPeer(right, false) { m_uid = uid, m_playerName = "Same name", m_characterID = new ZDOID(uid + 1000, id) };
                addZdo(peer.m_characterID, uid);
                bool upgraded = false;
                if (sequenced) client.Register<int>(AvatarSyncWire.SequencedHello, (rpc, v) => {
                    Check(v == AvatarSyncRules.Version, "Sequence capability version mismatch"); upgraded = true;
                    rpc.Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, models[modelIndex], hashes[modelIndex], 1));
                });
                client.Register<int>(AvatarSyncWire.Hello, (rpc, v) => {
                    if (!upgraded) rpc.Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, models[modelIndex], hashes[modelIndex]));
                });
                client.Register<ZPackage>(AvatarSyncWire.State, (rpc, p) => {
                    Check(AvatarSyncWire.ReadSnapshot(p, out _, out var s), "Snapshot malformed"); received[uid] = s;
                });
                peers.Add(peer); return peer;
            };
            Action pump = () => { update(); foreach (var c in clients) c.Update(.01f); foreach (var p in peers) p.m_rpc.Update(.01f); update(); foreach (var c in clients) c.Update(.01f); };
            Action<long, long, int> send = (uid, seq, model) => byPeer[uid].Invoke(AvatarSyncWire.Select,
                AvatarSyncWire.Selection(model >= 0, model < 0 ? "" : models[model], model < 0 ? "" : hashes[model], seq));
            Action<int, int> choices = (a, b) => Check(received.Values.All(s =>
                (a < 0 ? s.All(x => x.Peer != 101) : s.Single(x => x.Peer == 101).Model == models[a]) &&
                s.Single(x => x.Peer == 202).Model == models[b]), "Receivers disagree with latest sender choices");
            var aPeer = add(101, 1, true, 0); add(202, 2, true, 1); add(303, 3, false, 0); pump(); choices(0, 1);

            send(101, 2, 0); send(101, 3, 1); sockets[101].ReversePending(); pump(); choices(1, 1);
            long before = revision();
            send(101, 3, -1); send(101, 2, 0);
            byPeer[101].Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, models[0], hashes[0])); pump();
            choices(1, 1); Check(revision() == before, "Rejected duplicate/stale/legacy packets changed revision");
            var malformed = AvatarSyncWire.Selection(true, "../invalid", hashes[0], long.MaxValue);
            byPeer[101].Invoke(AvatarSyncWire.Select, malformed);
            var truncated = AvatarSyncWire.Selection(true, models[0], hashes[0]); truncated.Write(42);
            byPeer[101].Invoke(AvatarSyncWire.Select, truncated);
            var zero = AvatarSyncWire.Selection(true, models[0], hashes[0]); zero.Write(0L);
            byPeer[101].Invoke(AvatarSyncWire.Select, zero);
            send(101, 4, 0); pump(); choices(0, 1);
            for (int i = 0; i < 30; i++) send(101, 5 + i, i % 2);
            sockets[101].ReversePending(); pump(); choices(1, 1);
            send(202, 2, 0); pump(); choices(1, 0);
            // The old client still participates with unextended packets.
            byPeer[303].Invoke(AvatarSyncWire.Select, AvatarSyncWire.Selection(true, models[1], hashes[1])); pump();
            Check(received.Values.All(s => s.Single(x => x.Peer == 303).Model == models[1]), "Legacy client stopped synchronizing");

            aPeer.m_characterID = new ZDOID(1101, 99); addZdo(aPeer.m_characterID, 101);
            send(101, 1, 0); pump(); choices(1, 0);
            Check(received.Values.All(s => s.Single(x => x.Peer == 101).CharacterId == 99), "Respawn lost latest choice");
            send(101, 35, -1); send(101, 34, 1); pump(); choices(-1, 0);
            send(101, 36, 0); send(101, 35, -1); pump(); choices(0, 0);

            var oldRpc = aPeer.m_rpc; var oldClient = byPeer[101];
            peers.Remove(aPeer); clients.Remove(oldClient); pump();
            aPeer = add(101, 100, true, 1); pump(); choices(1, 0);
            var staleConnectionPacket = AvatarSyncWire.Selection(true, models[0], hashes[0], 999); staleConnectionPacket.SetPos(0);
            AccessTools.Method(plugin.GetType(), "ReceiveSelection").Invoke(plugin, new object[] { oldRpc, staleConnectionPacket });
            pump(); choices(1, 0); oldClient.Dispose(); oldRpc.Dispose();
            send(101, 2, 0); pump(); choices(0, 0);
            Check(received.Values.All(s => s.Single(x => x.Peer == 101).CharacterId == 100), "Reconnection reused an old character/session");
        }
        finally
        {
            netField.SetValue(null, oldNet); serverField.SetValue(null, oldServer); zdoField.SetValue(null, oldZdo);
            foreach (var c in clients) c.Dispose(); foreach (var p in peers) p.Dispose();
        }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
