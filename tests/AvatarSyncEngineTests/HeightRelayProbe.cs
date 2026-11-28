using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using ValheimVRM.Sync;

static class HeightRelayProbe
{
    public static void Run(string model, string hash)
    {
        var plugin = Chainloader.PluginInfos[AvatarSyncWire.ServerGuid].Instance;
        var tick = AccessTools.Method(plugin.GetType(), "Update");
        Action update = () => { AccessTools.Field(plugin.GetType(), "nextPoll").SetValue(plugin, 0f); tick.Invoke(plugin, null); };
        var netField = AccessTools.Field(typeof(ZNet), "m_instance"); var serverField = AccessTools.Field(typeof(ZNet), "m_isServer");
        var zdoField = AccessTools.Field(typeof(ZDOMan), "s_instance");
        var oldNet = netField.GetValue(null); var oldServer = serverField.GetValue(null); var oldZdo = zdoField.GetValue(null);
        var holder = new GameObject("HeightRelayFixture"); holder.SetActive(false); var net = holder.AddComponent<ZNet>();
        var manager = (ZDOMan)FormatterServices.GetUninitializedObject(typeof(ZDOMan));
        var objects = new Dictionary<ZDOID, ZDO>();
        AccessTools.Field(typeof(ZDOMan), "m_objectsByID").SetValue(manager, objects);
        AccessTools.Field(typeof(ZDOMan), "m_sessionID").SetValue(manager, 900L);
        var peers = (List<ZNetPeer>)AccessTools.Field(typeof(ZNet), "m_peers").GetValue(net);
        var clients = new Dictionary<long, ZRpc>();
        var sockets = new Dictionary<long, ServerRelayProbe.RelaySocket>();
        var received = new Dictionary<long, AvatarSelection[]>();
        try
        {
            netField.SetValue(null, net); serverField.SetValue(null, true); zdoField.SetValue(null, manager);
            Action<ZDOID,long> addZdo = (id, uid) => {
                var zdo = new ZDO { m_uid = id }; AccessTools.Field(typeof(ZDO), "m_prefab").SetValue(zdo, 1);
                zdo.SetOwnerInternal(uid); objects[id] = zdo;
            };
            foreach (long uid in new[] {101L,202L,303L})
            {
                bool supportsHeight = uid != 303;
                float initialHeight = uid == 101 ? 1.4f : 2f;
                var left = new ServerRelayProbe.RelaySocket(); var right = new ServerRelayProbe.RelaySocket(); left.Other=right; right.Other=left;
                var client = new ZRpc(left); clients[uid]=client; sockets[uid]=right;
                var peer = new ZNetPeer(right,false) { m_uid=uid, m_characterID=new ZDOID(uid+1000,1), m_playerName="Same name" };
                addZdo(peer.m_characterID,uid); peers.Add(peer);
                client.Register<int>(supportsHeight ? AvatarSyncWire.HeightHello : AvatarSyncWire.Hello,(rpc,v) => {
                    Check(v==AvatarSyncRules.Version,"Height hello protocol invalid");
                    rpc.Invoke(AvatarSyncWire.Select, supportsHeight ? AvatarSyncWire.Selection(true,model,hash,1,initialHeight) : AvatarSyncWire.Selection(true,model,hash));
                });
                client.Register<ZPackage>(AvatarSyncWire.State,(rpc,p) => {
                    Check(AvatarSyncWire.ReadSnapshot(p,out _,out var entries,out var withHeight),"Invalid height snapshot");
                    Check(withHeight==supportsHeight,"Server sent new snapshot to an old client"); received[uid]=entries;
                });
            }
            Action pump = () => { update(); foreach(var c in clients.Values)c.Update(.01f); foreach(var p in peers)p.m_rpc.Update(.01f); update(); foreach(var c in clients.Values)c.Update(.01f); };
            Action<float,float> heights = (a,b) => {
                foreach(long uid in new[]{101L,202L})
                    Check(received[uid].Single(s=>s.Peer==101).Height==a && received[uid].Single(s=>s.Peer==202).Height==b,"Per-player heights disagree");
                Check(received[303].All(s=>s.Height==2f),"Legacy snapshot did not use default height");
            };
            Action<long,long,float> send = (uid,seq,height) => clients[uid].Invoke(AvatarSyncWire.Select,AvatarSyncWire.Selection(true,model,hash,seq,height));
            pump(); heights(1.4f,2f);
            send(101,2,2f); send(101,3,2.2f); sockets[101].ReversePending(); pump(); heights(2.2f,2f);
            send(101,2,1.4f); send(101,3,1.4f); pump(); heights(2.2f,2f);
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,1.39f,2.21f})
            {
                var malformed=AvatarSyncWire.Selection(true,model,hash,long.MaxValue); malformed.Write(invalid);
                clients[101].Invoke(AvatarSyncWire.Select,malformed);
            }
            var trailing=AvatarSyncWire.Selection(true,model,hash,long.MaxValue,2f); trailing.Write(1);
            clients[101].Invoke(AvatarSyncWire.Select,trailing);
            send(101,4,1.4f); send(202,2,2.2f); pump(); heights(1.4f,2.2f);
            clients[101].Invoke(AvatarSyncWire.Select,AvatarSyncWire.Selection(true,model,hash,99)); pump(); heights(1.4f,2.2f);
            clients[101].Invoke(AvatarSyncWire.Select,AvatarSyncWire.Selection(false,"","",5,1.4f));
            send(101,4,2f); pump(); Check(received[202].All(s=>s.Peer!=101),"Late height request undid opt-out");
            send(101,6,2f); pump(); heights(2f,2.2f);
            var newId=new ZDOID(1101,99); peers[0].m_characterID=newId; addZdo(newId,101); pump();
            Check(received[202].Single(s=>s.Peer==101).CharacterId==99 && received[202].Single(s=>s.Peer==101).Height==2f,"Respawn lost height");
            var hostId=new ZDOID(900,1); addZdo(hostId,900); AccessTools.Field(typeof(ZNet),"m_characterID").SetValue(net,hostId);
            var host=AccessTools.Method(plugin.GetType(),"SetHostSelectionWithHeight");
            host.Invoke(plugin,new object[]{2L,true,model,hash,1.4f}); host.Invoke(plugin,new object[]{1L,true,model,hash,2.2f}); pump();
            Check(received[202].Single(s=>s.Peer==900).Height==1.4f,"Listen host height order failed");
        }
        finally
        {
            netField.SetValue(null,oldNet); serverField.SetValue(null,oldServer); zdoField.SetValue(null,oldZdo);
            foreach(var c in clients.Values)c.Dispose(); foreach(var p in peers)p.Dispose();
        }
    }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
}
