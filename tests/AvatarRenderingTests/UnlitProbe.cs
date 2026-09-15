using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRM;
using Object=UnityEngine.Object;

static class UnlitProbe
{
    public static IEnumerator Run(string output)
    {
        foreach(var e in Object.FindObjectsByType<EnvMan>(FindObjectsSortMode.None))e.enabled=false;
        foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
        foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))l.enabled=false;
        RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray*.3f;
        var bundle=AssetBundle.LoadFromFile(Environment.GetEnvironmentVariable("VRM_RENDER_TEST_BUNDLE"));
        var unlit=bundle.LoadAsset<Shader>("Assets/RenderingTests/UniUnlit.shader");
        var consumer=new Material(bundle.LoadAsset<Shader>("Assets/RenderingTests/DepthConsumer.shader"));
        var lines=new List<string>();
        lines.Add("Runtime shader inventory: "+string.Join(", ",VRMShaders.Shaders.Keys));
        var avatar=GameObject.CreatePrimitive(PrimitiveType.Quad);avatar.layer=30;avatar.transform.localScale=Vector3.one*2;
        var mesh=Object.Instantiate(avatar.GetComponent<MeshFilter>().sharedMesh);avatar.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=avatar.GetComponent<Renderer>();
        var surface=avatar.AddComponent<AvatarRenderingTarget>();avatar.AddComponent<AvatarBloomTarget>();
        var background=GameObject.CreatePrimitive(PrimitiveType.Cube);background.layer=30;background.transform.position=new Vector3(0,0,4);background.transform.localScale=new Vector3(20,20,.1f);
        var backgroundMaterial=new Material(Shader.Find("Standard"));backgroundMaterial.color=Color.gray*.2f;background.GetComponent<Renderer>().sharedMaterial=backgroundMaterial;
        var camera=new GameObject("Depth consumer camera").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
        camera.transform.position=new Vector3(0,0,-4);camera.fieldOfView=40;camera.nearClipPlane=.1f;camera.farClipPlane=20;
        camera.renderingPath=RenderingPath.DeferredShading;camera.depthTextureMode=DepthTextureMode.Depth;camera.allowHDR=true;
        var rt=new RenderTexture(192,192,24,RenderTextureFormat.ARGBFloat);rt.Create();camera.targetTexture=rt;
        var cb=new CommandBuffer();int tmp=Shader.PropertyToID("_DepthConsumerTestCopy");
        cb.GetTemporaryRT(tmp,-1,-1,0,FilterMode.Point,RenderTextureFormat.ARGBFloat);
        cb.Blit(BuiltinRenderTextureType.CameraTarget,tmp);
        cb.Blit(tmp,BuiltinRenderTextureType.CameraTarget,consumer);cb.ReleaseTemporaryRT(tmp);
        camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,cb);
        AvatarBloomController.Enabled=true;
        int checks=0;
        foreach(var shader in new[]{unlit,Shader.Find("Standard"),Shader.Find("Unlit/Texture"),Shader.Find("Unlit/Transparent"),Shader.Find("Unlit/Transparent Cutout")}.Where(s=>s!=null))
        {
            var material=new Material(shader);renderer.sharedMaterial=material;
            lines.Add(shader.name+" maxLOD="+shader.maximumLOD+" globalLOD="+Shader.globalMaximumLOD+" shader keywords="+string.Join(",",shader.keywordSpace.keywordNames));
            bool fixedShader=shader.name.StartsWith("Unlit/");
            foreach(int requestedMode in fixedShader?new[]{shader.name.Contains("Cutout")?1:shader.name.Contains("Transparent")?2:0}:shader.name=="Standard"?new[]{0,1,2,3}:new[]{0,1,2})
            foreach(float opacity in new[]{0f,.35f,1f})
            foreach(bool vertexAlpha in shader==unlit?new[]{false,true}:new[]{false})
            foreach(bool smoothnessAlpha in shader.name=="Standard"?new[]{false,true}:new[]{false})
            {
                int mode=requestedMode;
                if(!fixedShader)
                {
                    material.SetFloat(shader==unlit?"_BlendMode":"_Mode",mode);
                    material.SetFloat("_SrcBlend",mode==2?(float)BlendMode.SrcAlpha:1);material.SetFloat("_DstBlend",mode>=2?(float)BlendMode.OneMinusSrcAlpha:0);
                    material.SetFloat("_ZWrite",mode>=2?0:1);
                    material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHABLEND_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    if(mode==1)material.EnableKeyword("_ALPHATEST_ON");if(mode==2)material.EnableKeyword("_ALPHABLEND_ON");if(mode==3)material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                }
                if(smoothnessAlpha)material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");else material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                if(shader.name=="Standard")material.SetFloat("_SmoothnessTextureChannel",smoothnessAlpha?1:0);
                material.SetOverrideTag("RenderType",mode==0?"Opaque":mode==1?"TransparentCutout":"Transparent");
                if(shader==unlit){if(vertexAlpha)material.EnableKeyword("_VERTEXCOL_MUL");else material.DisableKeyword("_VERTEXCOL_MUL");}
                mesh.colors=Enumerable.Repeat(new Color(1,1,1,vertexAlpha?opacity:1),mesh.vertexCount).ToArray();
                var texture=new Texture2D(1,1,TextureFormat.RGBA32,false,true);texture.SetPixel(0,0,new Color(.2f,.3f,.4f,vertexAlpha?1:opacity));texture.Apply();
                material.SetTexture("_MainTex",texture);if(material.HasProperty("_Color"))material.SetColor("_Color",Color.white);
                if(material.HasProperty("_Cutoff"))material.SetFloat("_Cutoff",.5f);
                material.renderQueue=mode==0?2000:mode==1?2450:3000;
                consumer.SetVector("_Tint",Vector4.zero);surface.enabled=false;yield return null;
                var reference=Capture(camera,rt);
                renderer.enabled=false;yield return null;var backgroundOnly=Capture(camera,rt);renderer.enabled=true;yield return null;
                bool nativeVisible=MaxDifference(reference,backgroundOnly)>.001f;
                lines.Add("native probe "+shader.name+" mode="+mode+" alpha="+opacity+" smoothness="+smoothnessAlpha+" keywords="+string.Join(",",material.shaderKeywords)+" source="+reference[96*192+96]+" background="+backgroundOnly[96*192+96]);
                File.WriteAllLines(Path.Combine(output,"results.txt"),lines);
                if(shader==unlit && mode==0 && opacity==1 && !vertexAlpha)
                {
                    consumer.SetFloat("_EffectDepth",6);consumer.SetVector("_Tint",new Vector4(.17f,.08f,.03f,0));
                    Check(MaxDifference(reference,Capture(camera,rt))>.01f,"Missing-depth control did not reproduce background overlay");
                    consumer.SetVector("_Tint",Vector4.zero);
                    lines.Add("UniUnlit missing-depth control reproduces background overlay");
                }
                surface.enabled=true;surface.Apply();var corrected=Capture(camera,rt);
                float delta=MaxDifference(reference,corrected);
                Check(delta<.001f,"Surface bridge changed "+shader.name+" color: "+delta);
                Check(material.shader==shader,"Original shader was replaced");
                // Behind-model cloud tint and water tint use the same camera
                // depth contract. Front effects must still be visible.
                foreach(float depth in new[]{6f,2f})
                foreach(var tint in new[]{new Vector4(.17f,.08f,.03f,0),new Vector4(.02f,.13f,.21f,0)})
                {
                    consumer.SetFloat("_EffectDepth",depth);consumer.SetVector("_Tint",tint);
                    var actual=Capture(camera,rt);float difference=MaxDifference(corrected,actual);
                    // For native deferred cutouts the existing shader/depth
                    // passes own clipping; do not impose a forward-pass alpha
                    // interpretation on their GBuffer surface.
                    bool solid=mode==0 || (mode==1?nativeVisible:opacity==1);
                    if(depth==6 && solid)Check(difference<.001f,"Background cloud/water covers solid "+shader.name+" mode="+mode+" error="+difference);
                    if(depth==6 && !solid)Check(difference>.005f,"Lost real background through transparent "+shader.name);
                    if(depth==2 && mode<2)Check(difference>.005f,"Foreground depth effect was incorrectly hidden");
                    checks++;
                }
                consumer.SetVector("_Tint",Vector4.zero);
                if(mode>=2)
                {
                    material.renderQueue=2450;var early=Capture(camera,rt);
                    Check(material.renderQueue>2500 && MaxDifference(corrected,early)<.001f,"Invalid transparent queue was not repaired");
                }
                var bloom=camera.GetComponent<AvatarBloomCamera>();
                var mask=(RenderTexture)AccessTools.Property(typeof(AvatarBloomCamera),"BloomMask").GetValue(bloom);
                Check(mask!=null,"Missing bloom mask for "+shader.name);
                float covered=Read(mask)[96*192+96].r;
                // The game's retained Standard variants sample albedo alpha
                // for transparency even if an unused smoothness keyword is set.
                float coverage=opacity;
                float expected=mode==0?1:mode==1?(nativeVisible?1:0):coverage;
                Check(Math.Abs(covered-expected)<.015f,"Incorrect texture/vertex alpha bloom mask "+shader.name+": "+covered+" expected "+expected);
                lines.Add(shader.name+" mode="+mode+" alpha="+opacity+" vertex="+vertexAlpha+" smoothnessAlpha="+smoothnessAlpha+" nativeVisible="+nativeVisible+" color/depth/queue/bloom PASS");
                File.WriteAllLines(Path.Combine(output,"results.txt"),lines);Object.Destroy(texture);
            }
            Object.Destroy(material);
        }
        lines.Add("Depth consumer checks="+checks+"; behind solid blocked, foreground and real transparency preserved");
        lines.Add("AVATAR_SURFACE_TESTS_PASSED");File.WriteAllLines(Path.Combine(output,"results.txt"),lines);
        camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,cb);cb.Release();rt.Release();Object.Destroy(rt);
        Object.Destroy(camera.gameObject);Object.Destroy(avatar);Object.Destroy(background);Object.Destroy(backgroundMaterial);Object.Destroy(consumer);Object.Destroy(mesh);bundle.Unload(false);
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static Color[] Capture(Camera camera,RenderTexture rt){camera.Render();return Read(rt);}
    static Color[] Read(RenderTexture rt)
    {
        var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGBAFloat,false,true);
        t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();RenderTexture.active=old;var colors=t.GetPixels();Object.Destroy(t);return colors;
    }
    static float MaxDifference(Color[] a,Color[] b)
    {float delta=0;for(int y=65;y<127;y++)for(int x=65;x<127;x++){int i=y*192+x;delta=Math.Max(delta,Math.Max(Math.Abs(a[i].r-b[i].r),Math.Max(Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b))));}return delta;}
}
