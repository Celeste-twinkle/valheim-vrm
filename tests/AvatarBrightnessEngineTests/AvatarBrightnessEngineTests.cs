using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx;
using HarmonyLib;
using UniGLTF;
using UniVRM10;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

[BepInPlugin("valheimvrm.tests.brightness", "Avatar brightness import tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed class AvatarBrightnessEngineTests : BaseUnityPlugin
{
    string output;
    readonly List<string> report = new List<string>();
    void Awake()
    {
        output=Environment.GetEnvironmentVariable("VRM_BRIGHTNESS_TEST_OUTPUT");
        if(string.IsNullOrEmpty(output)){enabled=false;return;}
        Directory.CreateDirectory(output);
        new Harmony("valheimvrm.tests.brightness.isolation").Patch(AccessTools.PropertyGetter(typeof(FileHelpers),"CloudStorageSupported"),prefix:new HarmonyMethod(typeof(AvatarBrightnessEngineTests),nameof(NoCloud)));
    }
    static bool NoCloud(ref bool __result){__result=false;return false;}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
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
        while(!VRMShaders.Shaders.ContainsKey("VRM10/MToon10"))
        {if(Time.realtimeSinceStartup>deadline)throw new Exception("Shader timeout");yield return null;}
        var shader=VRMShaders.Shaders["VRM10/MToon10"];
        var m=new Material(shader);
        foreach(float limit in new[]{AvatarBrightness.BaseColorLimit,AvatarBrightness.ShadeColorLimit})
        {
            foreach(float value in new[]{0f,.01f,.1f,limit*.99999f,limit})
            {
                var input=new Color(value,value*.5f,value*.1f,.28f).gamma;
                Check(AvatarBrightness.LimitSrgb(input,limit).Equals(input),"Below/equal-threshold color changed");
            }
            foreach(float peak in new[]{limit*1.0001f,1f,4f,20f})
            {
                var input=new Color(peak*.3f,peak,peak*.7f,.28f).gamma;
                var result=AvatarBrightness.LimitSrgb(input,limit);var linear=result.linear;
                Check(Mathf.Abs(linear.g-limit)<1e-6f,"Incorrect linear-space ceiling");
                Check(Mathf.Abs(linear.r/linear.g-.3f)<1e-5f && Mathf.Abs(linear.b/linear.g-.7f)<1e-5f,"RGB ratios changed");
                Check(result.a.Equals(input.a),"Alpha changed");
                for(int i=0;i<1000;i++)Check(AvatarBrightness.LimitSrgb(result,limit).Equals(result),"Repeated limiting darkened the color");
            }
        }
        m.SetColor("_Color",new Color(1,.4f,.2f,.28f).gamma);
        var shade=new Color(.05f,.1f,.15f,1).gamma;m.SetColor("_ShadeColor",shade);
        var emission=new Color(4,2,1,1);m.SetColor("_EmissionColor",emission);
        m.renderQueue=3050;
        var storedShade=m.GetColor("_ShadeColor");var storedEmission=m.GetColor("_EmissionColor");
        report.Add("Material round-trip before cap: shade="+storedShade.ToString("R")+", supplied="+shade.ToString("R"));
        AvatarBrightness.Apply(m);
        Check(m.GetColor("_ShadeColor").Equals(storedShade),"Independent low shade changed");
        Check(m.GetColor("_EmissionColor").Equals(storedEmission) && m.renderQueue==3050,"Other material settings changed");
        Destroy(m);
        var standard=new Material(Shader.Find("Standard"));standard.color=Color.white;AvatarBrightness.Apply(standard);
        Check(standard.color.Equals(Color.white),"Unsupported shader changed");Destroy(standard);
        report.Add("Linear color limits: below/equal unchanged exactly; high/HDR RGB scaled proportionally; alpha retained; 1,000 repeat applications stable; independent shade, emission, render queue and unsupported shaders preserved.");

        var paths=new[]{
            Path.Combine(ValheimVRM.Settings.ValheimVRMDir,"Shinano_LightAdjustment.vrm"),
            Path.Combine(ValheimVRM.Settings.ValheimVRMDir,"KUMALY_2.vrm"),
            Environment.GetEnvironmentVariable("VRM_BRIGHTNESS_HIGH_AVATAR")};
        int changed=0,unchanged=0;
        for(int p=0;p<paths.Length;p++)
        {
            if(string.IsNullOrEmpty(paths[p]))continue;
            var bytes=File.ReadAllBytes(paths[p]);var hash=Hash(bytes);
            GameObject root=null;yield return ValheimVRM.VRM.ImportVisualAsync(bytes,paths[p],1,r=>root=r);
            Check(root!=null,"Production import failed: "+paths[p]);
            var imported=root.GetComponent<RuntimeGltfInstance>();
            var expected=new Dictionary<Material,Color[]>();
            using(var data=new GlbBinaryParser(bytes,paths[p]).Parse())
            {
                var generator=new BuiltInVrm10MaterialDescriptorGenerator();
                for(int i=0;i<data.GLTF.materials.Count;i++)
                {
                    var descriptor=generator.Get(data,i);
                    if(descriptor.Shader.name!="VRM10/MToon10")continue;
                    var actual=imported.Materials.Single(x=>x.name==descriptor.Name);
                    var baseline=new Material(descriptor.Shader);
                    foreach(var color in descriptor.Colors)baseline.SetColor(color.Key,color.Value);
                    var values=new Color[2];int slot=0;
                    foreach(var key in new[]{"_Color","_ShadeColor"})
                    {
                        float limit=slot==0?AvatarBrightness.BaseColorLimit:AvatarBrightness.ShadeColorLimit;
                        var original=baseline.GetColor(key);var value=actual.GetColor(key);
                        float high=Mathf.Max(original.r,Mathf.Max(original.g,original.b));
                        if(high<=Mathf.LinearToGammaSpace(limit))
                        {Check(value.Equals(original),"Already calibrated material changed: "+descriptor.Name+key);unchanged++;}
                        else
                        {
                            var color=value.linear;Check(Mathf.Abs(Mathf.Max(color.r,Mathf.Max(color.g,color.b))-limit)<1e-6f,"Imported material exceeds reference: "+descriptor.Name+key);
                            Check(original.a.Equals(value.a),"Imported alpha changed");changed++;
                        }
                        values[slot++]=value;
                    }
                    expected.Add(actual,values);Destroy(baseline);
                }
            }
            root.AddComponent<AvatarRenderingTarget>();
            foreach(bool lit in new[]{false,true,false,true})
            {
                AvatarRendering.Set(lit,true,false);
                root.GetComponent<Vrm10Instance>().Runtime.Expression.SetWeight(ExpressionKey.Happy,1);
                yield return null;
                root.GetComponent<Vrm10Instance>().Runtime.Expression.SetWeight(ExpressionKey.Happy,0);
                for(int frame=0;frame<8;frame++)yield return null;
                foreach(var pair in expected)
                {Check(pair.Key.GetColor("_Color").Equals(pair.Value[0]) && pair.Key.GetColor("_ShadeColor").Equals(pair.Value[1]),"Expression reset/render toggle lost calibrated baseline");}
            }
            Check(Hash(bytes)==hash && Hash(File.ReadAllBytes(paths[p]))==hash,"Source bytes/file/hash changed");
            report.Add(Path.GetFileName(paths[p])+": production import, source/hash preservation, expression reset and repeated scene-lighting toggles passed.");
            Destroy(root);yield return null;yield return null;
        }
        Check(unchanged>0,"No below-threshold imported material tested");
        if(!string.IsNullOrEmpty(paths[2]))Check(changed>0,"No high-brightness import tested");
        report.Add("Imported color properties: "+changed+" reduced, "+unchanged+" preserved exactly.");
    }
    static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes));}
}
