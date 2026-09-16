using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using ValheimVRM.Sync;
using Object = UnityEngine.Object;

[BepInPlugin("valheimvrm.tests.sync", "Avatar sync engine tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed partial class AvatarSyncEngineTests : BaseUnityPlugin
{
    string output;
    readonly List<string> report = new List<string>();
    void Awake()
    {
        output=Environment.GetEnvironmentVariable("VRM_SYNC_TEST_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled=false; return; }
        Directory.CreateDirectory(output);
        var getter=AccessTools.PropertyGetter(typeof(FileHelpers),"CloudStorageSupported");
        if(getter!=null)new Harmony("valheimvrm.tests.sync.isolation").Patch(getter,prefix:new HarmonyMethod(typeof(AvatarSyncEngineTests),nameof(NoCloud)));
    }
    static bool NoCloud(ref bool __result) { __result=false; return false; }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    IEnumerator Start()
    {
        if(string.IsNullOrEmpty(output))yield break;
        var stack=new Stack<IEnumerator>();stack.Push(Run());
        while(stack.Count>0)
        {
            bool more;object value=null;
            try { more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current; }
            catch(Exception ex) { File.WriteAllLines(Path.Combine(output,"results.txt"),report);File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());Application.Quit(1);yield break; }
            if(!more){stack.Pop();continue;}
            if(value is IEnumerator nested)stack.Push(nested);else yield return value;
        }
        report.Add("AVATAR_SYNC_ENGINE_TESTS_PASSED");
        File.WriteAllLines(Path.Combine(output,"results.txt"),report);Logger.LogInfo("AVATAR_SYNC_ENGINE_TESTS_PASSED");Application.Quit(0);
    }
    IEnumerator Run()
    {
        OffsetRangeProbe.Run(output);
        report.Add("Offset limits: +/-75 and +/-100 cm survive local save/reload for animation and all item groups; local clamp endpoints/nonfinite handling passed");
        float deadline=Time.realtimeSinceStartup+90;
        FejdStartup menu;
        while((menu=Object.FindFirstObjectByType<FejdStartup>())==null)
        { if(Time.realtimeSinceStartup>deadline)throw new Exception("Menu timeout");yield return null; }
        var names=new[]{"Shinano_LightAdjustment","Shinano_Sleep"};
        var fixtureNames=Environment.GetEnvironmentVariable("VRM_SYNC_TEST_MODELS");
        if(!string.IsNullOrEmpty(fixtureNames))
        {
            names=fixtureNames.Split('|');
            Check(names.Length==2 && names[0]!=names[1],"Two distinct fixture model names are required");
        }
        var hashes=names.Select(n=>Hash(File.ReadAllBytes(Path.Combine(ValheimVRM.Settings.ValheimVRMDir,n+".vrm")))).ToArray();
        WireTests(names,hashes);
        report.Add("Real ZRpc serialization over isolated in-memory sockets: two senders, three receivers, snapshots and malformed data passed");
        ServerRelayProbe.Run(names,hashes);
        report.Add("Unmodded ZRpc client: no avatar handlers, three discovery rounds maximum (twelve compatibility/capability packets), zero avatar snapshots, 60 ordinary request/reply exchanges, no errors or disconnects");
        report.Add("Production server plugin: handshake, authenticated ZDO ownership, forged character rejection, respawn, rapid changes, late join, opt-out and disconnect passed over real ZRpc");
        SequencedRelayProbe.Run(names, hashes);
        report.Add("Sequenced production relay: actual reversed ZRpc delivery, duplicate/legacy downgrade rejection, malformed packet isolation, independent players, opt-out ordering, respawn watermark retention, reconnect reset, old connection rejection and listen-host ordering passed");
        ClientSequenceProbe.Run();
        HeightRelayProbe.Run(names[0], hashes[0]);
        CalibrationRelayProbe.Run(names[0], hashes[0]);
        report.Add("Calibration relay: same-model player isolation, reversed requests/chunks, malformed/downgrade rejection, late join, respawn, opt-out, legacy peers and listen-host bridge passed");
        report.Add("Height relay: same model with independent 1.4/2/2.2 m heights, reversed delivery, stale/duplicate/invalid height rejection, legacy fallback, opt-out and host bridge passed");
        report.Add("Production client: old-server format, capability upgrade, monotonic 1/2/3/4 requests, late legacy hello, opt-out/refresh, new RPC reset, stale connection rejection, overflow guard and no-addon local mode passed");
        var prefab=(GameObject)AccessTools.Field(typeof(FejdStartup),"m_playerPrefab").GetValue(menu);
        if (Environment.GetEnvironmentVariable("VRM_ONLY_LIBRARY_TEST") == "restart") VrmOnlyRestart(prefab, names);
        if (Environment.GetEnvironmentVariable("VRM_ONLY_LIBRARY_TEST") == "1") yield return VrmOnlyLibrary(prefab, names);
        var a=MakePlayer(prefab,101,1001,1);var b=MakePlayer(prefab,202,2002,2);
        var sync=AvatarSyncClient.Instance;sync.enabled=false;
        AccessTools.Field(typeof(AvatarSyncClient),"<Connected>k__BackingField").SetValue(sync,true);
        var registry=new AvatarSyncRegistry();registry.Set(101,1001,1,names[0],hashes[0]);registry.Set(202,2002,2,names[1],hashes[1]);
        SetState(sync,registry);
        var selectionPath=Path.Combine(ValheimVRM.Settings.ConfigDir, "avatar_selections.json");
        var before=File.Exists(selectionPath)?File.ReadAllBytes(selectionPath):null;
        float capsuleA=a.GetComponent<CapsuleCollider>().height;
        yield return Switch(sync,a,names[0],hashes[0]);
        Check(!VrmManager.PlayerToVrmInstance.ContainsKey(b),"A's switch created B's model");
        var rootA=VrmManager.PlayerToVrmInstance[a];
        yield return Switch(sync,b,names[1],hashes[1]);
        var rootB=VrmManager.PlayerToVrmInstance[b];
        Check(rootA!=rootB && rootA.transform.IsChildOf(a.transform) && rootB.transform.IsChildOf(b.transform),"Avatar instances crossed players");
        Check(VrmManager.PlayerToName[a]==names[0] && VrmManager.PlayerToName[b]==names[1],"Selections crossed players");
        foreach(var r in rootA.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            Check(r.bones.All(t=>t==null||t.IsChildOf(rootA.transform)),"A's skin references another avatar");
        Check(a.GetComponent<CapsuleCollider>().height==capsuleA,"Remote visual changed player collision");
        registry.Set(101,1001,1,names[1],hashes[1]);SetState(sync,registry);
        yield return Switch(sync,a,names[1],hashes[1]);
        Check(VrmManager.PlayerToVrmInstance[b]==rootB,"A changing to B's asset replaced B's instance");
        Check(VrmManager.PlayerToVrmInstance[a]!=rootB,"Shared asset merged player instances");
        report.Add("Same-named game Player fixtures: A and B own distinct VRM clones; A switching to B's asset leaves B's object/bones untouched; remote collider unchanged");
        yield return HeightInstanceTests(sync, registry, a, b, names[1], hashes[1]);
        yield return CalibrationInstanceTests(sync, registry, a, b, names[1], hashes[1]);
        yield return NativeChoiceTests(sync, a, b, names[1]);
        rootB = VrmManager.PlayerToVrmInstance[b];
        yield return FolderMismatchTests(prefab, sync, registry, a, b, names, hashes);
        rootB = VrmManager.PlayerToVrmInstance[b];
        var desired=AccessTools.Method(typeof(AvatarSyncClient),"Desired");
        registry.Set(101,1001,99,names[0],hashes[0]);SetState(sync,registry);
        Check(desired.Invoke(sync,new object[]{a})==null,"Old incarnation still matches respawn state");
        var respawn=MakePlayer(prefab,101,1001,99);
        Check(((AvatarSelection)desired.Invoke(sync,new object[]{respawn})).Model==names[0],"Respawn did not match new ID");
        Check(((AvatarSelection)desired.Invoke(sync,new object[]{b})).Model==names[1],"Respawn changed B");
        // A stale snapshot must not roll back a later authoritative revision.
        var stale=AvatarSyncWire.Snapshot(0,new AvatarSelection[0]);stale.SetPos(0);
        AccessTools.Method(typeof(AvatarSyncClient),"AcceptSnapshot").Invoke(sync,new object[]{stale});
        Check(desired.Invoke(sync,new object[]{b})!=null,"Stale snapshot cleared B");
        var duplicate=AvatarSyncWire.Snapshot(registry.Revision,new AvatarSelection[0]);duplicate.SetPos(0);
        AccessTools.Method(typeof(AvatarSyncClient),"AcceptSnapshot").Invoke(sync,new object[]{duplicate});
        Check(desired.Invoke(sync,new object[]{b})!=null,"Equal-revision snapshot replaced current state");
        var local=Player.m_localPlayer;Player.m_localPlayer=b;
        Check(desired.Invoke(sync,new object[]{b})==null,"Remote snapshot overwrites the local player");Player.m_localPlayer=local;
        sync.SetEnabled(false);Check(desired.Invoke(sync,new object[]{b})==null,"Local mode still accepts remote state");sync.SetEnabled(true);
        RemoteAvatarBaseline.Restore(a);
        Check(!VrmManager.PlayerToVrmInstance.ContainsKey(a) && VrmManager.PlayerToVrmInstance[b]==rootB,"Restoring A affected B");
        Check(before==null?!File.Exists(selectionPath):File.ReadAllBytes(selectionPath).SequenceEqual(before),"Remote switch overwrote local saved choice");
        report.Add("Respawn IDs, stale snapshots, local-player exclusion, opt-out restoration and local selection persistence passed");
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VRM_ONLY_LIBRARY_TEST")))
            Check(Directory.GetFileSystemEntries(ValheimVRM.Settings.ValheimVRMDir).All(p => Path.GetExtension(p).Equals(".vrm", StringComparison.OrdinalIgnoreCase)),
                "Remote sync wrote non-VRM files into the model directory");
        // Keep inactive fixtures isolated; process exits immediately after the probe.
    }
    IEnumerator Switch(AvatarSyncClient sync,Player target,string name,string hash)
    {
        var desired=AccessTools.Method(typeof(AvatarSyncClient),"Desired");
        var selection=(AvatarSelection)desired.Invoke(sync,new object[]{target});
        bool done=false,success=false;
        AccessTools.Method(typeof(OutfitSwitcher),"RequestRemoteSwitch").Invoke(OutfitSwitcher.Instance,new object[]{target,name,hash,
            new Func<bool>(()=>selection.SameAs((AvatarSelection)desired.Invoke(sync,new object[]{target}))),new Action<bool>(ok=>{success=ok;done=true;}),selection.Height,selection.Calibration ?? AvatarCalibrationCodec.Default});
        float deadline=Time.realtimeSinceStartup+120;
        while(!done){if(Time.realtimeSinceStartup>deadline)throw new Exception("Remote import timeout");yield return null;}
        Check(success,"Remote import failed: "+OutfitSwitcher.Instance.LastError);
    }
    static Player MakePlayer(GameObject prefab,long owner,long user,uint id)
    {
        var holder=new GameObject("SyncTestInactive");holder.SetActive(false);
        var go=Object.Instantiate(prefab,holder.transform);go.name="Same character name";
        var player=go.GetComponent<Player>();var view=go.GetComponent<ZNetView>();
        var zdo=new ZDO{m_uid=new ZDOID(user,id)};
        AccessTools.Field(typeof(ZDO),"m_prefab").SetValue(zdo,1);
        ZDOExtraData.SetOwner(zdo.m_uid,ZDOID.AddUser(owner));
        AccessTools.Property(typeof(ZDO),"Owned").SetValue(zdo,true,null);
        AccessTools.Field(typeof(ZNetView),"m_zdo").SetValue(view,zdo);
        AccessTools.Field(typeof(Character),"m_nview").SetValue(player,view);
        var animator=go.GetComponentInChildren<Animator>(true);
        AccessTools.Field(typeof(Character),"m_animator").SetValue(player,animator);
        AccessTools.Field(typeof(Character),"m_visual").SetValue(player,animator.gameObject);
        go.AddComponent<VrmController>();
        return player;
    }
    static void SetState(AvatarSyncClient client,AvatarSyncRegistry registry)
    {
        var p=AvatarSyncWire.Snapshot(registry.Revision,registry.Snapshot(),true,true);p.SetPos(0);
        AccessTools.Method(typeof(AvatarSyncClient),"AcceptSnapshot").Invoke(client,new object[]{p});
    }
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}

    static void WireTests(string[] names,string[] hashes)
    {
        var registry=new AvatarSyncRegistry();var clients=new List<ZRpc>();var servers=new List<ZRpc>();
        var received=new AvatarSelection[3][];
        for(int i=0;i<3;i++)
        {
            int index=i;var left=new MemorySocket();var right=new MemorySocket();left.Other=right;right.Other=left;
            var client=new ZRpc(left);var server=new ZRpc(right);clients.Add(client);servers.Add(server);
            long authenticated=(i+1)*101;
            server.Register<ZPackage>(AvatarSyncWire.Select,(rpc,p)=>{
                if(AvatarSyncWire.ReadSelection(p,out var enabled,out var model,out var hash))
                {if(enabled&&model!="")registry.Set(authenticated,authenticated+1000,(uint)index+1,model,hash);else registry.Remove(authenticated);}
            });
            client.Register<ZPackage>(AvatarSyncWire.State,(rpc,p)=>{Check(AvatarSyncWire.ReadSnapshot(p,out _,out var entries),"Wire snapshot rejected");received[index]=entries;});
        }
        clients[0].Invoke(AvatarSyncWire.Select,AvatarSyncWire.Selection(true,names[0],hashes[0]));
        clients[1].Invoke(AvatarSyncWire.Select,AvatarSyncWire.Selection(true,names[1],hashes[1]));
        foreach(var server in servers)server.Update(.01f);
        foreach(var server in servers)server.Invoke(AvatarSyncWire.State,AvatarSyncWire.Snapshot(registry.Revision,registry.Snapshot()));
        foreach(var client in clients)client.Update(.01f);
        Check(received.All(r=>r!=null&&r.Length==2&&r.Single(s=>s.Peer==101).Model==names[0]&&r.Single(s=>s.Peer==202).Model==names[1]),"Clients disagree about player selections");
        Check(!AvatarSyncWire.ReadSelection(new ZPackage(new byte[]{1,2,3}),out _,out _,out _),"Truncated packet accepted");
        var bad=AvatarSyncWire.Selection(true,"../bad",hashes[0]);bad.SetPos(0);
        Check(!AvatarSyncWire.ReadSelection(bad,out _,out _,out _),"Unsafe name accepted");
        foreach(var rpc in clients.Concat(servers))rpc.Dispose();
    }
    sealed class MemorySocket:ISocket
    {
        public MemorySocket Other;readonly Queue<ZPackage> packets=new Queue<ZPackage>();bool open=true;
        public bool IsConnected()=>open;public void Send(ZPackage p)=>Other.packets.Enqueue(new ZPackage(p.GetArray()));
        public ZPackage Recv()=>packets.Count>0?packets.Dequeue():null;
        public int GetSendQueueSize()=>0;public int GetCurrentSendRate()=>0;public bool IsHost()=>false;
        public void Dispose(){open=false;}public bool GotNewData()=>packets.Count>0;public void Close(){open=false;}
        public string GetEndPointString()=>"isolated-test";public void GetAndResetStats(out int sent,out int received){sent=received=0;}
        public void GetConnectionQuality(out float local,out float remote,out int ping,out float sent,out float received){local=remote=1;ping=0;sent=received=0;}
        public ISocket Accept()=>null;public int GetHostPort()=>0;public bool Flush()=>true;public string GetHostName()=>"isolated-test";public void VersionMatch(){}
    }
}
