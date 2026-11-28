using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx;
using HarmonyLib;
using UniGLTF;
using UnityEngine;
using ValheimVRM;
using ValheimVRM.Sync;
using Object = UnityEngine.Object;
using Avatar = ValheimVRM.VRM;

[BepInPlugin("valheimvrm.tests.residency", "Avatar resource lifetime tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed class AvatarResidencyEngineTests : BaseUnityPlugin
{
    string output;
    readonly List<string> report = new List<string>();
    static int disposedImports;
    static readonly HashSet<int> fixturePlayers = new HashSet<int>();
    readonly List<Material> instanceMaterials = new List<Material>();
    void Awake()
    {
        output=Environment.GetEnvironmentVariable("VRM_RESIDENCY_TEST_OUTPUT");
        if(string.IsNullOrEmpty(output)){enabled=false;return;}
        Directory.CreateDirectory(output);
        var harmony=new Harmony("valheimvrm.tests.residency.isolation");
        harmony.Patch(AccessTools.PropertyGetter(typeof(FileHelpers),"CloudStorageSupported"),prefix:new HarmonyMethod(typeof(AvatarResidencyEngineTests),nameof(NoCloud)));
        harmony.Patch(AccessTools.Method(typeof(GltfData),"Dispose"),postfix:new HarmonyMethod(typeof(AvatarResidencyEngineTests),nameof(ImportDisposed)));
        harmony.Patch(AccessTools.Method(typeof(Player),"IsDead"),prefix:new HarmonyMethod(typeof(AvatarResidencyEngineTests),nameof(FixtureIsAlive)));
    }
    static bool NoCloud(ref bool __result){__result=false;return false;}
    static bool FixtureIsAlive(Player __instance,ref bool __result)
    {if(__instance!=null && fixturePlayers.Contains(__instance.GetInstanceID())){__result=false;return false;}return true;}
    static void ImportDisposed(){disposedImports++;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Sweep(){AccessTools.Method(typeof(AvatarResidency),"Collect").Invoke(null,null);}
    static void RetainRemote(params string[] models)
    {
        var states=models.Select((model,index)=>new AvatarSelection{Peer=index+1,CharacterUser=index+100,CharacterId=(uint)(index+1),Model=model}).ToArray();
        AccessTools.Method(typeof(AvatarResidency),"SetRemoteSelections").Invoke(null,new object[]{states});
    }
    static void ClearResidency(){AccessTools.Method(typeof(AvatarResidency),"ClearAll").Invoke(null,null);}
    IEnumerator Start()
    {
        if(string.IsNullOrEmpty(output))yield break;
        var stack=new Stack<IEnumerator>();stack.Push(Run());
        while(stack.Count>0)
        {
            bool more;object value=null;
            try{more=stack.Peek().MoveNext();if(more)value=stack.Peek().Current;}
            catch(Exception ex){File.WriteAllLines(Path.Combine(output,"results.txt"),report);File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());Application.Quit(1);yield break;}
            if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
            if(value is IEnumerator nested)stack.Push(nested);else yield return value;
        }
        File.WriteAllLines(Path.Combine(output,"results.txt"),report);Application.Quit(0);
    }
    IEnumerator Run()
    {
        float deadline=Time.realtimeSinceStartup+90;
        FejdStartup menu;
        while((menu=Object.FindFirstObjectByType<FejdStartup>())==null || !VRMShaders.Shaders.ContainsKey("VRM10/MToon10"))
        {if(Time.realtimeSinceStartup>deadline)throw new Exception("Menu timeout");yield return null;}
        AvatarSyncClient.Instance.enabled=false;
        RetainRemote();
        var prefab=(GameObject)AccessTools.Field(typeof(FejdStartup),"m_playerPrefab").GetValue(menu);
        var a=MakePlayer(prefab);var b=MakePlayer(prefab);
        Avatar first=null,second=null,reloaded=null;
        int before=disposedImports;
        yield return Load("Shinano_LightAdjustment",a,x=>first=x);
        Check(disposedImports==before+1,"Successful import did not dispose native GLB buffers");
        Check(first.Src==null,"Source file bytes retained with legacy sharing disabled");
        var firstResources=ResourcesOf(first);
        yield return Attach(first,a);yield return Attach(first,b);
        yield return Load("Shinano_Sleep",a,x=>second=x);
        var secondResources=ResourcesOf(second);
        yield return Attach(second,a);yield return null;
        Sweep();yield return null;yield return null;
        Check(first.VisualModel!=null && firstResources.All(x=>x!=null),"Switching A destroyed B's shared avatar assets");
        Check(second.VisualModel!=null && secondResources.All(x=>x!=null),"Current avatar was evicted");
        report.Add("Two players sharing one imported template: A changes model; B retains every mesh, texture, material and rig resource.");
        report.Add("Inactive/out-of-camera player holders retain resources; renderer visibility is not used for eviction.");

        yield return Load("Shinano_LightAdjustment",b,x=>reloaded=x);
        var reloadResources=ResourcesOf(reloaded);
        Check(first.VisualModel!=null && firstResources.All(x=>x!=null),"Registering a new same-name generation destroyed the old active generation");
        yield return Attach(reloaded,b);yield return null;
        Sweep();yield return null;yield return null;
        Check(first.VisualModel==null && firstResources.All(x=>x==null),"Old generation resources leaked");
        Check(VrmManager.VrmDic[reloaded.Name]==reloaded && VrmManager.VrmHashes.ContainsKey(reloaded.Name),"Old generation removed newer cache identity/hash");
        Check(reloadResources.All(x=>x!=null) && secondResources.All(x=>x!=null),"Unloading old generation destroyed another model");
        report.Add("Same-name replacement retains old users, then destroys all old resources without deleting the new generation/hash.");

        yield return Attach(second,b);yield return null;
        Sweep();yield return null;yield return null;
        Check(reloaded.VisualModel==null && reloadResources.All(x=>x==null),"Unused reloaded avatar resources survived collection");
        Check(!VrmManager.VrmDic.ContainsKey(reloaded.Name) && !VrmManager.VrmHashes.ContainsKey(reloaded.Name),"Unused cache entry survived");
        Check(secondResources.All(x=>x!=null),"Second avatar lost shared resources");
        RetainRemote(second.Name);
        Destroy(a.transform.parent.gameObject);Destroy(b.transform.parent.gameObject);
        yield return null;Sweep();
        yield return null;yield return null;
        Check(second.VisualModel!=null && secondResources.All(x=>x!=null),"Temporarily invisible online player lost template resources");
        Check(VrmManager.VrmDic[second.Name]==second && VrmManager.VrmHashes.ContainsKey(second.Name),"Online selection lost its cache identity/hash");
        report.Add("A synchronized player outside the local AOI retains its imported template; collection only culls rendering instances.");
        RetainRemote();Sweep();yield return null;yield return null;
        Check(second.VisualModel==null && secondResources.All(x=>x==null),"Disconnected player's template resources survived collection");
        report.Add("Removing the player from the authoritative snapshot releases its template, cache/hash and every owned native asset.");

        var c=MakePlayer(prefab);
        yield return Load("Shinano_LightAdjustment",c,x=>reloaded=x);
        var pin=(IDisposable)AccessTools.Method(typeof(AvatarResidency),"Acquire").Invoke(null,new object[]{reloaded});
        Sweep();yield return null;
        Check(reloaded.VisualModel!=null,"Pending attachment was evicted");
        pin.Dispose();yield return Attach(reloaded,c);yield return null;
        var finalResources=ResourcesOf(reloaded);
        Check(finalResources.All(x=>x!=null),"Reimport after eviction failed");
        RetainRemote(reloaded.Name);Destroy(c.transform.parent.gameObject);yield return null;Sweep();yield return null;
        Check(finalResources.All(x=>x!=null),"Session-retained reimport was released before world exit");
        ClearResidency();yield return null;yield return null;
        Check(finalResources.All(x=>x==null),"World-exit cleanup leaked");
        report.Add("Reselecting after release imports successfully; pending attachments are pinned, and local world exit clears every retained template.");

        var pixel=new Texture2D(1,1,TextureFormat.RGBA32,false);pixel.SetPixel(0,0,Color.white);pixel.Apply();
        var bytes=pixel.EncodeToPNG();Destroy(pixel);
        var info=new DeserializingTextureInfo(bytes,"image/png",UniGLTF.ColorSpace.sRGB,true,FilterMode.Bilinear,TextureWrapMode.Repeat,TextureWrapMode.Repeat);
        var texA=new ValheimVRM.TextureDeserializer().LoadTextureAsync(info,new ImmediateCaller()).GetAwaiter().GetResult();
        var texB=new ValheimVRM.TextureDeserializer().LoadTextureAsync(info,new ImmediateCaller()).GetAwaiter().GetResult();
        Check(texA!=null && texB!=null && texA!=texB,"Different VRM0 importers share destroyable texture ownership");
        Destroy(texA);yield return null;Check(texB!=null,"Destroying one importer's texture destroyed another");Destroy(texB);
        report.Add("Legacy VRM0 texture decoders have independent ownership; destroying one identical texture does not invalidate the other.");
        Check(disposedImports==before+4,"Expected all four GLB imports to dispose native buffers");
        // Valid GLB envelope with no VRM metadata: failure after parsing must
        // still release the parser's native buffers.
        byte[] invalid;
        using(var stream=new MemoryStream())
        using(var writer=new BinaryWriter(stream))
        {
            string json="{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"byteLength\":4}]}";
            while(json.Length%4!=0)json+=" ";
            var payload=System.Text.Encoding.UTF8.GetBytes(json);
            writer.Write(0x46546c67);writer.Write(2);writer.Write(32+payload.Length);
            writer.Write(payload.Length);writer.Write(0x4e4f534a);writer.Write(payload);
            writer.Write(4);writer.Write(0x004e4942);writer.Write(0);invalid=stream.ToArray();
        }
        var failed=Avatar.ImportVisualAsync(invalid,"invalid-avatar.vrm",1);
        while(!failed.IsCompleted)yield return null;
        Check(failed.IsFaulted && disposedImports==before+5,"Failed import leaked its native GLB buffers");
        _=failed.Exception;
        report.Add("A valid GLB with invalid VRM metadata fails safely and disposes its native buffers.");
        Check(instanceMaterials.All(m=>m==null), "Per-instance expression materials leaked after player removal");
        report.Add("All per-instance expression materials were destroyed with their avatars; color-sync setup creates no additional MToon10 copies.");
        report.Add("Four production VRM imports released their native GLB buffers; normal sync/local mode retains zero source-file byte arrays.");
    }
    IEnumerator Load(string name,Player player,Action<Avatar> done)
    {
        var path=Path.Combine(ValheimVRM.Settings.ValheimVRMDir,name+".vrm");var data=File.ReadAllBytes(path);
        GameObject root=null;yield return Avatar.ImportVisualAsync(data,path,1,r=>root=r);Check(root!=null,"Import failed");
        var originals=root.GetComponent<RuntimeGltfInstance>().Materials.ToArray();
        ValheimVRM.Settings.AddSettingsFromFile(name,false);
        byte[] hash;using(var sha=SHA256.Create())hash=sha.ComputeHash(data);
        var model=VrmManager.RegisterVrm(new Avatar(root,name),player.GetComponentInChildren<LODGroup>(true),player,hash);
        model.Src=data;model.RecalculateSrcBytesHash();
        Check(root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).All(m=>originals.Contains(m)),"Registration cloned unowned materials");
        done(model);
    }
    IEnumerator Attach(Avatar model,Player player)
    {
        VrmManager.PlayerToName[player]=model.Name;yield return model.SetToPlayer(player);
        var root=VrmManager.PlayerToVrmInstance[player];var originals=model.VisualModel.GetComponent<RuntimeGltfInstance>().Materials;
        var materials=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).ToArray();
        instanceMaterials.AddRange(materials.Where(m=>!originals.Contains(m)));
        root.GetComponent<MToonColorSync>()?.Setup(root);
        Check(root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).SequenceEqual(materials),"Color-sync setup cloned MToon10 expression materials");
        // The synthetic Player stays inactive to avoid world startup. Briefly
        // activate just the avatar so Unity runs its normal component lifetime.
        var parent=root.transform.parent;root.transform.SetParent(null,false);yield return null;root.transform.SetParent(parent,false);
    }
    static Object[] ResourcesOf(Avatar model)=>model.VisualModel.GetComponent<RuntimeGltfInstance>().RuntimeResources.Select(x=>x.Item2).ToArray();
    static Player MakePlayer(GameObject prefab)
    {
        var holder=new GameObject("Inactive isolated residency player");holder.SetActive(false);
        var go=Object.Instantiate(prefab,holder.transform);var p=go.GetComponent<Player>();
        var animator=go.GetComponentInChildren<Animator>(true);
        AccessTools.Field(typeof(Character),"m_animator").SetValue(p,animator);
        AccessTools.Field(typeof(Character),"m_visual").SetValue(p,animator.gameObject);
        go.AddComponent<VrmController>();fixturePlayers.Add(p.GetInstanceID());return p;
    }
}
