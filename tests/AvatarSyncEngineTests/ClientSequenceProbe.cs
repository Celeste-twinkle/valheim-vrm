using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using ValheimVRM.Sync;

static class ClientSequenceProbe
{
    public static void Run()
    {
        var sync = AvatarSyncClient.Instance; bool oldEnabled = sync.enabled, oldSync = sync.SyncEnabled;
        sync.enabled = false;
        var netField = AccessTools.Field(typeof(ZNet), "m_instance"); var serverField = AccessTools.Field(typeof(ZNet), "m_isServer");
        var statusField = AccessTools.Field(typeof(ZNet), "m_connectionStatus");
        var oldNet = netField.GetValue(null); var oldServer = serverField.GetValue(null); var oldStatus = statusField.GetValue(null);
        var holder = new GameObject("ClientSequenceFixture"); holder.SetActive(false); var net = holder.AddComponent<ZNet>();
        var peers = (List<ZNetPeer>)AccessTools.Field(typeof(ZNet), "m_peers").GetValue(net);
        var send = AccessTools.Method(typeof(AvatarSyncClient), "SendSelection");
        var reset = AccessTools.Method(typeof(AvatarSyncClient), "ResetConnectionIfNeeded");
        var rpcs = new List<ZRpc>(); var sequences = new List<long>();
        Func<ZRpc> connect = () => {
            var left = new ServerRelayProbe.RelaySocket(); var right = new ServerRelayProbe.RelaySocket(); left.Other = right; right.Other = left;
            var peer = new ZNetPeer(left, true) { m_uid = 900 }; var server = new ZRpc(right);
            rpcs.Add(peer.m_rpc); rpcs.Add(server); peers.Clear(); peers.Add(peer); sync.Register(peer);
            server.Register<ZPackage>(AvatarSyncWire.Select, (rpc, p) => {
                Check(AvatarSyncWire.ReadSelection(p, out _, out _, out _, out var seq, out _, out var withHeight, out var calibration), "Production client emitted malformed request");
                Check(withHeight == sync.HeightSync, "Client sent height without capability negotiation"); sequences.Add(seq);
                Check((calibration != null) == sync.CalibrationSync, "Calibration was sent without capability negotiation");
            });
            return server;
        };
        Action<ZRpc, string> hello = (server, method) => {
            server.Invoke(method, AvatarSyncRules.Version); peers[0].m_rpc.Update(.01f); server.Update(.01f);
        };
        try
        {
            netField.SetValue(null, net); serverField.SetValue(null, false);
            statusField.SetValue(null, Enum.Parse(statusField.FieldType, "Connected"));
            // No server addon: even an explicit SendSelection emits nothing.
            var server = connect(); reset.Invoke(sync, null); sync.SetEnabled(false);
            send.Invoke(sync, null); server.Update(.01f);
            Check(sequences.Count == 0 && !sync.Connected, "Local-only client sent unsolicited selections");

            // Exact old-server handshake: unextended format until capability arrives.
            hello(server, AvatarSyncWire.Hello);
            Check(sequences.SequenceEqual(new[] { 0L }) && !sync.SequencedRequests, "Old server fallback changed wire format");
            hello(server, AvatarSyncWire.SequencedHello);
            Check(sequences.Last() == 1 && sync.SequencedRequests, "Capability did not start sequence at one");
            int count = sequences.Count; hello(server, AvatarSyncWire.Hello);
            Check(sequences.Count == count && sync.SequencedRequests, "Delayed legacy hello downgraded/resubmitted the connection");
            sync.SetEnabled(true); send.Invoke(sync, null); server.Update(.01f);
            sync.SetEnabled(false); send.Invoke(sync, null); server.Update(.01f);
            Check(sequences.Skip(1).SequenceEqual(new[] { 1L, 2L, 3L }), "Opt-out reset or reused a request sequence");
            sync.RetryMissing(); send.Invoke(sync, null); server.Update(.01f);
            Check(sequences.Last() == 3 && sequences.Count == 4, "Refresh emitted a duplicate unchanged request");
            hello(server, AvatarSyncWire.SequencedHello);
            Check(sequences.Last() == 4, "Repeated discovery reset the request sequence");
            hello(server, AvatarSyncWire.HeightHello);
            Check(sync.HeightSync && sequences.Last() == 5, "Height capability failed to upgrade sequence");
            count = sequences.Count; hello(server, AvatarSyncWire.SequencedHello); hello(server, AvatarSyncWire.Hello);
            Check(sync.HeightSync && sequences.Count == count, "Late capability downgraded height negotiation");
            var accept = AccessTools.Method(typeof(AvatarSyncClient), "AcceptSnapshot");
            var latest = AvatarSyncWire.Snapshot(10, new AvatarSelection[0], true); latest.SetPos(0); accept.Invoke(sync,new object[]{latest});
            var legacy = AvatarSyncWire.Snapshot(11, new AvatarSelection[0]); legacy.SetPos(0); accept.Invoke(sync,new object[]{legacy});
            Check((long)AccessTools.Field(typeof(AvatarSyncClient), "revision").GetValue(sync)==10, "Late legacy snapshot replaced height state");
            hello(server, AvatarSyncWire.CalibrationHello);
            Check(sync.CalibrationSync && sequences.Last() == 6, "Calibration capability did not upgrade monotonically");
            count = sequences.Count; hello(server, AvatarSyncWire.HeightHello);
            Check(sync.CalibrationSync && sequences.Count == count, "Late height hello downgraded calibration support");
            var calibrated = AvatarSyncWire.Snapshot(20, new AvatarSelection[0], true, true); calibrated.SetPos(0); accept.Invoke(sync,new object[]{calibrated});
            var heightOnly = AvatarSyncWire.Snapshot(21, new AvatarSelection[0], true); heightOnly.SetPos(0); accept.Invoke(sync,new object[]{heightOnly});
            Check((long)AccessTools.Field(typeof(AvatarSyncClient), "revision").GetValue(sync)==20, "Older snapshot format erased calibration");
            var entry = new AvatarSelection { Peer=101, CharacterUser=1001, CharacterId=1,
                Model="ChunkProbe", Sha256=new string('a',64), Calibration=CalibrationRelayProbe.Data(.1f,768) };
            var chunks=AvatarSnapshotChunks.Split(22,AvatarSyncWire.Snapshot(22,new[]{entry},true,true)).Reverse().ToArray();
            foreach(var chunk in chunks.Take(chunks.Length-1)) server.Invoke(AvatarSyncWire.StateChunk,chunk);
            peers[0].m_rpc.Update(.01f);
            Check((long)AccessTools.Field(typeof(AvatarSyncClient),"revision").GetValue(sync)==20,"Partial production-client snapshot applied");
            server.Invoke(AvatarSyncWire.StateChunk,chunks.Last()); peers[0].m_rpc.Update(.01f);
            Check((long)AccessTools.Field(typeof(AvatarSyncClient),"revision").GetValue(sync)==22 &&
                ((AvatarSelection[])AccessTools.Field(typeof(AvatarSyncClient),"states").GetValue(sync)).Single().Calibration==entry.Calibration,
                "Production client did not atomically accept reversed calibration chunks");

            // Replacement RPC on the same ZNet is a fresh connection; old RPCs
            // cannot negotiate or reset its order, even before a polling tick.
            var oldPeer = peers[0]; var oldRpcServer = server;
            server = connect(); hello(server, AvatarSyncWire.SequencedHello);
            Check(sequences.Last() == 1 && sync.Connected, "Reconnect did not reset sequence for the new RPC");
            Check(!sync.HeightSync, "Height capability leaked into legacy reconnect");
            Check(!sync.CalibrationSync, "Calibration capability leaked into legacy reconnect");
            count = sequences.Count;
            oldRpcServer.Invoke(AvatarSyncWire.SequencedHello, AvatarSyncRules.Version); oldPeer.m_rpc.Update(.01f); oldRpcServer.Update(.01f);
            Check(sequences.Count == count, "Old connection hello was accepted");
            sync.SetEnabled(true); send.Invoke(sync, null); server.Update(.01f);
            Check(sequences.Last() == 2, "New connection sequence did not advance independently");

            // Never wrap a signed long into a value that looks newer or legacy.
            AccessTools.Field(typeof(AvatarSyncClient), "requestSequence").SetValue(sync, long.MaxValue);
            sync.SetEnabled(false); count = sequences.Count; send.Invoke(sync, null); server.Update(.01f);
            Check(sequences.Count == count && sync.LastError.Contains("exhausted"), "Client sequence overflow was sent");
        }
        finally
        {
            netField.SetValue(null, oldNet); serverField.SetValue(null, oldServer); statusField.SetValue(null, oldStatus);
            reset.Invoke(sync, null); sync.SetEnabled(oldSync); sync.enabled = oldEnabled;
            foreach (var rpc in rpcs) rpc.Dispose();
        }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
