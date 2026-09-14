using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using ValheimVRM.Sync;

static class ServerRelayProbe
{
    public static void Run(string[] models,string[] hashes)
    {
        var plugin=Chainloader.PluginInfos[AvatarSyncWire.ServerGuid].Instance;plugin.enabled=false;
        var tick=AccessTools.Method(plugin.GetType(),"Update");
        Action update=()=>{AccessTools.Field(plugin.GetType(),"nextPoll").SetValue(plugin,0f);tick.Invoke(plugin,null);};
        var netField=AccessTools.Field(typeof(ZNet),"m_instance");var serverField=AccessTools.Field(typeof(ZNet),"m_isServer");
        var zdoField=AccessTools.Field(typeof(ZDOMan),"s_instance");
        var oldNet=netField.GetValue(null);var oldServer=serverField.GetValue(null);var oldZdo=zdoField.GetValue(null);
        var holder=new GameObject("IsolatedSyncServer");holder.SetActive(false);var net=holder.AddComponent<ZNet>();
        var manager=(ZDOMan)FormatterServices.GetUninitializedObject(typeof(ZDOMan));
        var objects=new Dictionary<ZDOID,ZDO>();
        AccessTools.Field(typeof(ZDOMan),"m_objectsByID").SetValue(manager,objects);
        AccessTools.Field(typeof(ZDOMan),"m_sessionID").SetValue(manager,900L);
        var peers=(List<ZNetPeer>)AccessTools.Field(typeof(ZNet),"m_peers").GetValue(net);
        var clients=new List<ZRpc>();var received=new Dictionary<long,AvatarSelection[]>();
        try
        {
            netField.SetValue(null,net);serverField.SetValue(null,true);zdoField.SetValue(null,manager);
            var hostId=new ZDOID(900,9);
            AccessTools.Field(typeof(ZNet),"m_characterID").SetValue(net,hostId);
            var hostZdo=new ZDO{m_uid=hostId};AccessTools.Field(typeof(ZDO),"m_prefab").SetValue(hostZdo,1);hostZdo.SetOwnerInternal(900);objects[hostId]=hostZdo;
            var submitHost=AccessTools.Method(plugin.GetType(),"SetHostSelection");
            var readHost=AccessTools.Method(plugin.GetType(),"ReadHostSnapshot");
            submitHost.Invoke(plugin,new object[]{true,models[0],hashes[0]});
            update();
            Check(AvatarSyncWire.ReadSnapshot((ZPackage)readHost.Invoke(plugin,null),out _,out var hostState)&&
                hostState.Length==1&&hostState[0].Peer==900&&hostState[0].Model==models[0],"Initial listen-host choice was lost before server Update");
            submitHost.Invoke(plugin,new object[]{false,"",""});update();
            Func<long,long,uint,string,string,ZNetPeer> add=(uid,user,id,model,hash)=>{
                var left=new RelaySocket();var right=new RelaySocket();left.Other=right;right.Other=left;
                var client=new ZRpc(left);clients.Add(client);
                var peer=new ZNetPeer(right,false){m_uid=uid,m_playerName="Same name",m_characterID=new ZDOID(user,id)};
                var zdo=new ZDO{m_uid=peer.m_characterID};AccessTools.Field(typeof(ZDO),"m_prefab").SetValue(zdo,1);zdo.SetOwnerInternal(uid);objects[zdo.m_uid]=zdo;
                client.Register<int>(AvatarSyncWire.Hello,(rpc,version)=>{
                    Check(version==AvatarSyncRules.Version,"Handshake version");
                    rpc.Invoke(AvatarSyncWire.Select,AvatarSyncWire.Selection(true,model,hash));
                });
                client.Register<ZPackage>(AvatarSyncWire.State,(rpc,p)=>{
                    Check(AvatarSyncWire.ReadSnapshot(p,out _,out var state),"Malformed server snapshot");received[uid]=state;
                });
                peers.Add(peer);return peer;
            };
            Action pump=()=>{update();foreach(var c in clients)c.Update(.01f);foreach(var p in peers)p.m_rpc.Update(.01f);update();foreach(var c in clients)c.Update(.01f);};
            // This client has no mod RPC handlers at all: the same ZRpc path as
            // an unmodified game. It still exchanges ordinary game messages.
            var vanillaLeft=new RelaySocket();var vanillaRight=new RelaySocket();vanillaLeft.Other=vanillaRight;vanillaRight.Other=vanillaLeft;
            var vanilla=new ZRpc(vanillaLeft);clients.Add(vanilla);
            var vanillaPeer=new ZNetPeer(vanillaRight,false){m_uid=505,m_playerName="Unmodded player"};peers.Add(vanillaPeer);
            int normalRequests=0,normalReplies=0;
            vanillaPeer.m_rpc.Register<int>("TestNormalGameRequest",(rpc,n)=>{normalRequests+=n;rpc.Invoke("TestNormalGameReply",n);});
            vanilla.Register<int>("TestNormalGameReply",(rpc,n)=>normalReplies+=n);
            var a=add(101,1001,1,models[0],hashes[0]);var b=add(202,2002,2,models[1],hashes[1]);add(303,3003,3,"","");pump();
            Check(received.Count==3&&received.Values.All(s=>s.Length==2&&s.Single(x=>x.Peer==101).Model==models[0]&&s.Single(x=>x.Peer==202).Model==models[1]),"Production relay mixed players");
            // The game CharacterID RPC is itself client supplied. The mod must
            // additionally check that this ZDO belongs to this authenticated peer.
            a.m_characterID=b.m_characterID;pump();
            Check(received.Values.All(s=>s.Length==1&&s[0].Peer==202&&s[0].Model==models[1]),"Forged character ID changed B");
            a.m_characterID=new ZDOID(1001,99);var reborn=new ZDO{m_uid=a.m_characterID};AccessTools.Field(typeof(ZDO),"m_prefab").SetValue(reborn,1);reborn.SetOwnerInternal(101);objects[reborn.m_uid]=reborn;pump();
            Check(received.Values.All(s=>s.Single(x=>x.Peer==101).CharacterId==99),"Production respawn binding failed");
            for(int i=0;i<30;i++)clients[1].Invoke(AvatarSyncWire.Select,AvatarSyncWire.Selection(true,models[i%2],hashes[i%2]));
            pump();
            Check(received.Values.All(s=>s.Single(x=>x.Peer==101).Model==models[1]&&s.Single(x=>x.Peer==202).Model==models[1]),"Rapid selection coalescing failed");
            add(404,4004,4,"","");pump();Check(received[404].Length==2,"Late join snapshot missing");
            clients[1].Invoke(AvatarSyncWire.Select,AvatarSyncWire.Selection(false,"",""));pump();
            Check(received.Values.All(s=>s.Length==1&&s[0].Peer==202),"A opting out changed B");
            peers.Remove(b);pump();Check(received[404].Length==0,"Disconnect leaked selection");
            for(int i=0;i<60;i++)
            {
                var sessions=(System.Collections.IDictionary)AccessTools.Field(plugin.GetType(),"sessions").GetValue(plugin);
                var session=sessions[vanillaPeer.m_rpc];AccessTools.Field(session.GetType(),"NextHello").SetValue(session,0f);
                vanilla.Invoke("TestNormalGameRequest",1);pump();
                Check(vanilla.Update(.01f)==ZRpc.ErrorCode.Success&&vanillaPeer.m_rpc.Update(.01f)==ZRpc.ErrorCode.Success,"Unmodded client RPC returned an error");
            }
            Check(vanilla.IsConnected()&&vanillaPeer.IsReady()&&normalRequests==60&&normalReplies==60,"Unmodded player was disconnected or game RPC stopped");
            Check(vanillaRight.SentMethods.Count(h=>h==AvatarSyncWire.Hello.GetStableHashCode())==3,"Discovery did not stop after three unanswered hellos");
            Check(vanillaRight.SentMethods.Count(h=>h==AvatarSyncWire.SequencedHello.GetStableHashCode())==3,"Sequence discovery exceeded three rounds");
            Check(vanillaRight.SentMethods.Count(h=>h==AvatarSyncWire.HeightHello.GetStableHashCode())==3,"Height discovery exceeded three rounds");
            Check(!vanillaRight.SentMethods.Contains(AvatarSyncWire.State.GetStableHashCode()),"Unmodded client received avatar snapshots");
            Check(received[404].All(s=>s.Peer!=505),"Unmodded player acquired another player's avatar");
        }
        finally
        {
            netField.SetValue(null,oldNet);serverField.SetValue(null,oldServer);zdoField.SetValue(null,oldZdo);
            foreach(var c in clients)c.Dispose();foreach(var p in peers)p.Dispose();
            // Inactive test network never starts or saves a world.
        }
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    internal sealed class RelaySocket:ISocket
    {
        public RelaySocket Other;public readonly List<int> SentMethods=new List<int>();readonly Queue<ZPackage> packets=new Queue<ZPackage>();bool open=true;
        public bool IsConnected()=>open;public void Send(ZPackage p){var copy=new ZPackage(p.GetArray());SentMethods.Add(copy.ReadInt());copy.SetPos(0);Other.packets.Enqueue(copy);}
        public ZPackage Recv()=>packets.Count>0?packets.Dequeue():null;
        public void ReversePending() { var pending=packets.Reverse().ToArray();packets.Clear();foreach(var p in pending)packets.Enqueue(p); }
        public int GetSendQueueSize()=>0;public int GetCurrentSendRate()=>0;public bool IsHost()=>false;
        public void Dispose(){open=false;}public bool GotNewData()=>packets.Count>0;public void Close(){open=false;}
        public string GetEndPointString()=>"isolated-relay";public void GetAndResetStats(out int sent,out int received){sent=received=0;}
        public void GetConnectionQuality(out float local,out float remote,out int ping,out float sent,out float received){local=remote=1;ping=0;sent=received=0;}
        public ISocket Accept()=>null;public int GetHostPort()=>0;public bool Flush()=>true;public string GetHostName()=>"isolated-relay";public void VersionMatch(){}
    }
}
