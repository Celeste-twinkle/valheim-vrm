using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRM;
using Object = UnityEngine.Object;

[BepInPlugin("valheimvrm.tests.fur", "Avatar fur regression tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed class AvatarFurTests : BaseUnityPlugin
{
    string output;
    readonly List<string> report = new List<string>();
    void Awake()
    {
        output = Environment.GetEnvironmentVariable("VRM_FUR_TEST_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        Directory.CreateDirectory(output);
        new Harmony("valheimvrm.tests.fur.isolation").Patch(AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupported"),
            prefix: new HarmonyMethod(typeof(AvatarFurTests), nameof(NoCloud)));
    }
    static bool NoCloud(ref bool __result) { __result = false; return false; }
    void Log(string text) { report.Add(text); File.WriteAllLines(Path.Combine(output, "results.txt"), report); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static int Owners => (int)AccessTools.Field(typeof(AvatarFurResources), "owners").GetValue(null);
    static int Bundles => AssetBundle.GetAllLoadedAssetBundles().Count(b => b.name == "avatar_fur");
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            bool more; object next = null;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Application.Quit(1); yield break; }
            if (!more) stack.Pop(); else if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Log("AVATAR_FUR_TESTS_PASSED"); Application.Quit(0);
    }
    IEnumerator Run()
    {
        float deadline = Time.realtimeSinceStartup + 90;
        while (Object.FindFirstObjectByType<FejdStartup>() == null || !VRMShaders.Shaders.ContainsKey("VRM10/MToon10"))
        { Check(Time.realtimeSinceStartup < deadline, "Menu timeout"); yield return null; }
        Check(Bundles == 0 && Owners == 0, "Optional fur package loaded before any fur model");
        if (Environment.GetEnvironmentVariable("VRM_FUR_LIGHTING_ONLY") == "1")
        {
            var owner=new GameObject("Lighting test lease");
            var shader=(Shader)AccessTools.Method(typeof(AvatarFurResources),"Acquire").Invoke(null,new object[]{owner});
            yield return Lighting(shader); Object.Destroy(owner); yield return null; yield break;
        }
        string path = Environment.GetEnvironmentVariable("VRM_FUR_TEST_MODEL");
        byte[] source = File.ReadAllBytes(path);
        foreach (string mode in new[] { "absent", "disabled", "zero", "unknown", "invalid-type", "missing-image", "active" })
        {
            byte[] bytes = Variant(source, mode);
            GameObject model = null;
            yield return ValheimVRM.VRM.ImportVisualAsync(bytes, path, 1, go => model = go);
            Check(model != null, "Import failed: " + mode);
            var surfaces = model.GetComponentsInChildren<AvatarFurSurface>(true);
            if (mode != "active")
            {
                Check(surfaces.Length == 0 && Owners == 0 && Bundles == 0, mode + " allocated fur resources");
                Check(!Resources.FindObjectsOfTypeAll<Material>().Any(m => m.name.EndsWith(" (GPU fur)")), mode + " allocated fur material");
                Log(mode + ": no fur component, material, lease or bundle");
            }
            else
            {
                Check(surfaces.Length > 0 && Owners == 1 && Bundles == 1, "Fur was not enabled");
                foreach (var surface in surfaces)
                {
                    var original = (Renderer)AccessTools.Field(typeof(AvatarFurSurface), "source").GetValue(surface);
                    var overlay = surface.GetComponent<SkinnedMeshRenderer>();
                    if (overlay != null) Check(overlay.sharedMesh == ((SkinnedMeshRenderer)original).sharedMesh, "Fur copied the mesh");
                    Check(surface.GetComponent<Renderer>().sharedMaterials.Any(m => m.IsKeywordEnabled("AVATAR_FUR_ON")), "Fur macro not enabled");
                }
                var ownedMaterials = surfaces.SelectMany(s => s.GetComponent<Renderer>().sharedMaterials).Distinct().ToArray();
                var ownedTextures = ownedMaterials.SelectMany(m => new[] {m.GetTexture("_FurLengthMask"),m.GetTexture("_FurNoise"),m.GetTexture("_FurMask")})
                    .Where(t => t != null && t.name.StartsWith("ValheimVRM fur mask")).Distinct().ToArray();
                Log("active: shared base mesh, macro enabled; surfaces=" + surfaces.Length + " masks=" + ownedTextures.Length);
                var clone = Object.Instantiate(model);
                AccessTools.Method(typeof(ValheimVRM.VRM), "PrepareVrm10Clone").Invoke(null, new object[] { model, clone });
                clone.transform.position = new Vector3(5, 0, 0);
                AvatarScale.ApplyHeight(clone, 1.4f);
                yield return null;
                Check(Owners == 1, "Clone incorrectly owns the import's shader lease");
                foreach (var surface in clone.GetComponentsInChildren<AvatarFurSurface>(true))
                {
                    var original = (Renderer)AccessTools.Field(typeof(AvatarFurSurface), "source").GetValue(surface);
                    Check(original.transform.IsChildOf(clone.transform), "Clone fur points to another player's source renderer");
                    var skin = original as SkinnedMeshRenderer;
                    var overlay = surface.GetComponent<SkinnedMeshRenderer>();
                    if (skin != null)
                    {
                        Check(overlay.bones.SequenceEqual(skin.bones), "Clone fur bones differ from its own clothes");
                        foreach (var bone in overlay.bones) Check(bone == null || bone.IsChildOf(clone.transform), "Foreign player bone");
                        if (skin.sharedMesh.blendShapeCount > 0)
                        {
                            skin.SetBlendShapeWeight(0, 37); AccessTools.Method(typeof(AvatarFurSurface), "LateUpdate").Invoke(surface, null);
                            Check(Math.Abs(overlay.GetBlendShapeWeight(0) - 37) < .01, "Fur lost blend shape deformation");
                        }
                    }
                }
                Object.Destroy(clone); yield return null; yield return null;
                Check(Owners == 1 && Bundles == 1, "Destroying a clone unloaded live fur resources");
                Log("clone: own renderer/bones at 1.4m, shared resources survive clone destruction");
                yield return Render(model, surfaces);
                yield return Lighting(surfaces[0].GetComponent<Renderer>().sharedMaterials.First(m => m.GetFloat("_FurLength") > 0).shader);
                Object.Destroy(model); yield return null; yield return null;
                Check(Owners == 0 && Bundles == 0, "Fur bundle retained after last owner release");
                Check(ownedMaterials.All(m => m == null) && ownedTextures.All(t => t == null), "Fur materials/textures leaked");
                Log("unload: materials, textures and optional bundle released");
                continue;
            }
            Object.Destroy(model); yield return null; yield return null;
        }
    }

    IEnumerator Render(GameObject model, AvatarFurSurface[] surfaces)
    {
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
        foreach (var e in Object.FindObjectsByType<EnvMan>(FindObjectsSortMode.None)) e.enabled = false;
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray * .3f; RenderSettings.fog = false;
        var light = new GameObject("FurTestLight").AddComponent<Light>(); light.type = LightType.Directional;
        light.intensity = 1; light.transform.rotation = Quaternion.Euler(30,180,0); light.cullingMask = 1 << 30;
        AvatarRendering.Current.SceneLighting = true; AvatarRendering.Current.ReceiveShadows = false;
        model.AddComponent<AvatarRenderingTarget>(); model.AddComponent<AvatarBloomTarget>();
        var camera = new GameObject("FurTestCamera").AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f,.1f,.13f); camera.nearClipPlane = .05f; camera.farClipPlane = 50;
        camera.fieldOfView = 32; camera.renderingPath = RenderingPath.DeferredShading;
        camera.transform.position = model.transform.position + new Vector3(0,1.35f,3.6f);
        camera.transform.LookAt(model.transform.position + new Vector3(0,1.05f,0));
        var rt = new RenderTexture(800,1000,24,RenderTextureFormat.ARGB32); rt.Create(); camera.targetTexture=rt;
        // Freeze simulation so the A/B image measures only fur, not hair movement.
        foreach (var b in model.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(b is AvatarRenderingTarget) && !(b is AvatarBloomTarget) && !(b is AvatarFurSurface)) b.enabled = false;
        foreach (var s in surfaces) AccessTools.Method(typeof(AvatarFurSurface), "LateUpdate").Invoke(s, null);
        yield return null;
        var materials = surfaces.SelectMany(s=>s.GetComponent<Renderer>().sharedMaterials).Where(m=>m.GetFloat("_FurLength")>0).Distinct().ToArray();
        var withFur = Capture(camera,rt,"fur-on.png");
        foreach(var material in materials) material.DisableKeyword("AVATAR_FUR_ON");
        var macroOff = Capture(camera,rt,"fur-macro-off.png");
        foreach(var s in surfaces) { s.enabled=false; s.GetComponent<Renderer>().enabled=false; }
        var baseOnly = Capture(camera,rt,"fur-base-only.png");
        long macroDifference=0,furDifference=0;int changed=0;
        for(int i=0;i<baseOnly.Length;i++)
        {
            int off=Math.Abs(macroOff[i].r-baseOnly[i].r)+Math.Abs(macroOff[i].g-baseOnly[i].g)+Math.Abs(macroOff[i].b-baseOnly[i].b);
            int on=Math.Abs(withFur[i].r-baseOnly[i].r)+Math.Abs(withFur[i].g-baseOnly[i].g)+Math.Abs(withFur[i].b-baseOnly[i].b);
            macroDifference+=off;furDifference+=on;if(on>3)changed++;
        }
        Check(macroDifference==0,"Disabled macro changed visible color: "+macroDifference);
        Check(changed>50,"Enabled fur has no visible effect");
        Log("render: disabled macro equals base pixel-for-pixel; fur changed "+changed+" pixels, RGB delta="+furDifference);
        Object.Destroy(camera.gameObject); Object.Destroy(light.gameObject); rt.Release(); Object.Destroy(rt);
    }
    IEnumerator Lighting(Shader furShader)
    {
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
        foreach (var e in Object.FindObjectsByType<EnvMan>(FindObjectsSortMode.None)) e.enabled = false;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray*.3f;RenderSettings.fog=false;
        AvatarRendering.Current.SceneLighting=true;AvatarRendering.Current.ReceiveShadows=false;
        var root = new GameObject("Fur lighting parity");
        var cloth = new GameObject("Uniform cloth"); cloth.transform.SetParent(root.transform, false); cloth.layer = 29;
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>();
        var uv = new List<Vector2>(); var triangles = new List<int>();
        const int side = 20;
        for (int y=0;y<=side;y++) for(int x=0;x<=side;x++)
        { vertices.Add(new Vector3(x/(float)side-.5f,y/(float)side-.5f,0)); normals.Add(Vector3.back); tangents.Add(new Vector4(1,0,0,-1)); uv.Add(new Vector2(x/(float)side,y/(float)side)); }
        for (int y=0;y<side;y++) for(int x=0;x<side;x++)
        { int a=y*(side+1)+x;triangles.AddRange(new[]{a,a+side+1,a+1,a+1,a+side+1,a+side+2}); }
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        cloth.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=cloth.AddComponent<MeshRenderer>();
        var material=new Material((Shader)AccessTools.Property(typeof(AvatarRendering), "OptionsShader").GetValue(null));
        material.SetColor("_Color",new Color(.45f,.45f,.45f,1)); material.SetColor("_ShadeColor",new Color(.15f,.15f,.15f,1));
        material.SetFloat("_AvatarSceneLighting",1);material.SetFloat("_ShadingToonyFactor",.2f);material.SetFloat("_ShadingShiftFactor",-.15f);
        renderer.sharedMaterial=material;
        var overlay=new GameObject("Fur");overlay.transform.SetParent(cloth.transform,false);overlay.layer=29;
        overlay.AddComponent<MeshFilter>().sharedMesh=mesh;var furRenderer=overlay.AddComponent<MeshRenderer>();
        var fur=new Material(furShader);fur.SetFloat("_FurLength",.01f);fur.SetFloat("_FurDensity",3);fur.SetFloat("_FurRandomness",.5f);fur.SetFloat("_FurRootOffset",-1);fur.EnableKeyword("AVATAR_FUR_ON");
        furRenderer.sharedMaterial=fur;var surface=overlay.AddComponent<AvatarFurSurface>();surface.Initialize(renderer,furRenderer);
        var bump=new Texture2D(2,2,TextureFormat.RGBA32,false,true);bump.SetPixels(Enumerable.Repeat(new Color(1,.5f,1,.85f),4).ToArray());bump.Apply();
        var shadeMap=new Texture2D(2,2,TextureFormat.RGBA32,false,true);shadeMap.SetPixels(Enumerable.Repeat(Color.gray*.5f,4).ToArray());shadeMap.Apply();
        var matcap=new Texture2D(2,2,TextureFormat.RGBA32,false,true);matcap.SetPixels(Enumerable.Repeat(new Color(.02f,.015f,.01f,1),4).ToArray());matcap.Apply();
        var camera=new GameObject("Lighting camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
        camera.enabled=false;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        camera.renderingPath=RenderingPath.DeferredShading;camera.orthographic=true;camera.orthographicSize=.65f;camera.transform.position=new Vector3(0,0,-3);
        var rt=new RenderTexture(320,320,24,RenderTextureFormat.ARGBFloat);rt.Create();camera.targetTexture=rt;
        var sun=new GameObject("Sun").AddComponent<Light>();sun.transform.SetParent(root.transform,false);sun.type=LightType.Directional;sun.intensity=2;sun.cullingMask=1<<29;sun.renderMode=LightRenderMode.ForcePixel;
        sun.transform.rotation=Quaternion.Euler(0,55,0);
        var extra=new GameObject("Additional light").AddComponent<Light>();extra.transform.SetParent(root.transform,false);extra.type=LightType.Directional;extra.intensity=1;extra.cullingMask=1<<29;extra.renderMode=LightRenderMode.ForcePixel;
        bool baseline=Environment.GetEnvironmentVariable("VRM_FUR_LIGHTING_BASELINE")=="1";
        foreach(bool legacy in new[]{false,true})
        {
        material.shader=(Shader)AccessTools.Property(typeof(AvatarRendering), legacy ? "LegacyOptionsShader" : "OptionsShader").GetValue(null);
        material.SetFloat(legacy?"_ShadeToony":"_ShadingToonyFactor",.2f);material.SetFloat(legacy?"_ShadeShift":"_ShadingShiftFactor",-.15f);
        for(int mode=0;mode<9;mode++)
        {
            sun.intensity=mode==4?.7f:2;
            extra.enabled=mode==2 || mode==5;
            extra.type=mode==5?LightType.Point:LightType.Directional;extra.transform.position=new Vector3(.1f,.1f,-.5f);extra.range=3;
            material.SetColor("_EmissionColor",mode==3?new Color(.08f,.03f,.02f,1):Color.black);
            material.EnableKeyword("_MTOON_EMISSIVEMAP");
            material.SetTexture(legacy?"_ShadingGradeTexture":"_ShadingShiftTex",mode==4?shadeMap:Texture2D.whiteTexture);
            material.SetFloat(legacy?"_ShadingGradeRate":"_ShadingShiftTexScale",mode==4?.3f:0);
            material.EnableKeyword("_MTOON_PARAMETERMAP");
            material.SetTexture(legacy?"_SphereAdd":"_MatcapTex",mode==6 || mode==7?matcap:Texture2D.blackTexture);
            if(!legacy){material.SetColor("_MatcapColor",Color.white);material.EnableKeyword("_MTOON_RIMMAP");}
            material.SetColor("_RimColor",mode==6 || mode==8?new Color(.03f,.025f,.02f,1):Color.black);
            material.SetFloat("_RimLightingMix",.25f);material.SetFloat("_RimFresnelPower",1);material.SetFloat("_RimLift",.5f);
            if(mode==1){material.SetTexture("_BumpMap",bump);material.SetFloat("_BumpScale",1);material.EnableKeyword("_NORMALMAP");}
            else {material.SetTexture("_BumpMap",null);material.DisableKeyword("_NORMALMAP");}
            AccessTools.Method(typeof(AvatarFurSurface),"LateUpdate").Invoke(surface,null);yield return null;
            var on=CaptureLinear(camera,rt);
            fur.DisableKeyword("AVATAR_FUR_ON");var off=CaptureLinear(camera,rt);fur.EnableKeyword("AVATAR_FUR_ON");
            double difference=0,reference=0,max=0;int count=0;float maxOn=0,maxOff=0;
            for(int y=65;y<255;y++)for(int x=65;x<255;x++)
            {int p=y*320+x;double delta=Math.Abs(on[p].r-off[p].r);difference+=delta;reference+=off[p].r;if(delta>max){max=delta;maxOn=on[p].r;maxOff=off[p].r;}count++;}
            double relative=difference/Math.Max(reference,.000001);
            Log("lighting "+(legacy?"VRM0 ":"VRM1 ")+new[]{"directional","normal-map","two-lights","emission","shading-map","point-light","matcap-rim","matcap","rim"}[mode]+": mean relative error="+relative.ToString("F6")+", max linear error="+max.ToString("F6")+", max on/off="+maxOn+"/"+maxOff);
            Check(baseline || relative<.015,"Fur and cloth respond differently to "+mode+" lighting: "+relative);
        }
        }
        Object.Destroy(root);Object.Destroy(material);Object.Destroy(fur);Object.Destroy(bump);Object.Destroy(shadeMap);Object.Destroy(matcap);Object.Destroy(mesh);rt.Release();Object.Destroy(rt);yield return null;
    }
    static Color[] CaptureLinear(Camera camera,RenderTexture rt)
    {
        camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBAFloat,false,true);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();RenderTexture.active=previous;
        var pixels=tex.GetPixels();Object.Destroy(tex);return pixels;
    }
    Color32[] Capture(Camera camera,RenderTexture rt,string name)
    {
        camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();RenderTexture.active=previous;
        var conversion=Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",true);
        File.WriteAllBytes(Path.Combine(output,name),(byte[])conversion.GetMethod("EncodeToPNG",new[]{typeof(Texture2D)}).Invoke(null,new object[]{tex}));
        var pixels=tex.GetPixels32();Object.Destroy(tex);return pixels;
    }
    static byte[] Variant(byte[] original,string mode)
    {
        int size=BitConverter.ToInt32(original,12);var json=JObject.Parse(Encoding.UTF8.GetString(original,20,size));
        foreach(var material in (JArray)json["materials"])
        {
            var data=material["extras"]?[AvatarFurDefinition.Key] as JObject;if(data==null)continue;
            if(mode=="absent")((JObject)material["extras"]).Remove(AvatarFurDefinition.Key);
            if(mode=="disabled")data["enabled"]=false;
            if(mode=="zero")data["length"]=0;
            if(mode=="unknown")data["version"]=2;
            if(mode=="invalid-type")data["density"]="3";
            if(mode=="missing-image")data["lengthImage"]=65535;
        }
        byte[] text=Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
        int padded=(text.Length+3)&~3;var result=new byte[20+padded+original.Length-20-size];
        Buffer.BlockCopy(original,0,result,0,20);Buffer.BlockCopy(BitConverter.GetBytes(result.Length),0,result,8,4);
        Buffer.BlockCopy(BitConverter.GetBytes(padded),0,result,12,4);Buffer.BlockCopy(text,0,result,20,text.Length);
        for(int i=text.Length;i<padded;i++)result[20+i]=32;
        Buffer.BlockCopy(original,20+size,result,20+padded,original.Length-20-size);return result;
    }
}
