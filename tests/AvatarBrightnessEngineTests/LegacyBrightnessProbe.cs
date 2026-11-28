using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UniGLTF;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRM;
using VRM;
using Object = UnityEngine.Object;

static class LegacyBrightnessProbe
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static Material[] Materials(GameObject root) => root.GetComponentsInChildren<Renderer>(true)
        .SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
    static float Peak(Color color) => Mathf.Max(color.r, Mathf.Max(color.g, color.b));
    public static IEnumerator Run(List<string> report, string output)
    {
        bool baseline = Environment.GetEnvironmentVariable("VRM_BRIGHTNESS_BASELINE") == "1";
        foreach (var env in Object.FindObjectsByType<EnvMan>(FindObjectsSortMode.None)) env.enabled = false;
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
        RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.reflectionIntensity = 0; QualitySettings.pixelLightCount = 8;
        var sun = new GameObject("LegacyBrightnessSun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.cullingMask = 1 << 30;
        sun.transform.rotation = Quaternion.Euler(30, 180, 0); sun.shadows = LightShadows.None;
        sun.renderMode = LightRenderMode.ForcePixel; RenderSettings.sun = sun;
        var point = new GameObject("LegacyBrightnessPoint").AddComponent<Light>();
        point.type = LightType.Point; point.range = 6; point.cullingMask = 1 << 30;
        point.transform.position = new Vector3(1, 1.5f, 2); point.renderMode = LightRenderMode.ForcePixel;
        var camera = new GameObject("LegacyBrightnessCamera").AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 30; camera.allowHDR = true;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.nearClipPlane = .1f; camera.farClipPlane = 20; camera.fieldOfView = 35;
        camera.transform.position = new Vector3(0, 1.05f, 4);
        camera.transform.LookAt(new Vector3(0, 1.05f, 0));
        var target = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGBFloat); target.Create(); camera.targetTexture = target;
        var paths = Environment.GetEnvironmentVariable("VRM_BRIGHTNESS_LEGACY_MODELS").Split('|');
        foreach (var path in paths)
        {
            var bytes = File.ReadAllBytes(path);
            AvatarRendering.Set(true, true, false);
            GameObject root = null;
            yield return ValheimVRM.VRM.ImportVisualAsync(bytes, path, 1, r => root = r);
            Check(root != null, "Legacy production import failed");
            root.transform.position = Vector3.zero;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 30;
            foreach (var spring in root.GetComponentsInChildren<VRMSpringBone>(true)) spring.enabled = false;
            var materials = Materials(root);
            Check(materials.All(m => m.shader.name == "VRM/MToon"), "Fixture is not native legacy MToon");
            int reduced = 0, preserved = 0;
            using (var data = new GlbBinaryParser(bytes, path).Parse())
            {
                var generator = new BuiltInVrmMaterialDescriptorGenerator(new VRMData(data).VrmExtension);
                for (int i = 0; i < data.GLTF.materials.Count; i++)
                {
                    var descriptor = generator.Get(data, i);
                    if (descriptor.Shader.name != "VRM/MToon") continue;
                    var actual = root.GetComponent<RuntimeGltfInstance>().Materials.Single(m => m.name == descriptor.Name);
                    var original = new Material(descriptor.Shader);
                    foreach (var pair in descriptor.Vectors) original.SetVector(pair.Key, pair.Value);
                    foreach (var pair in descriptor.Colors) original.SetColor(pair.Key, pair.Value);
                    foreach (var key in new[] {"_Color", "_ShadeColor"})
                    {
                        var value = original.GetColor(key);
                        float limit = key == "_Color" ? AvatarBrightness.BaseColorLimit : AvatarBrightness.ShadeColorLimit;
                        bool high = Peak(value) > Mathf.LinearToGammaSpace(limit);
                        if (!baseline)
                        {
                            var capped=actual.GetColor(key);
                            Check(high ? Mathf.Abs(Peak(capped.linear)-limit)<1e-6f && capped.a==value.a : capped.Equals(value),
                                "Legacy cap mismatch: " + descriptor.Name + key + " actual=" + capped.ToString("R") + " original=" + value.ToString("R"));
                        }
                        if (high) reduced++; else preserved++;
                    }
                    Check(actual.GetColor("_EmissionColor").Equals(original.GetColor("_EmissionColor")), "Emission changed");
                    Object.Destroy(original);
                }
            }
            root.AddComponent<AvatarRenderingTarget>(); root.AddComponent<AvatarBloomTarget>();
            var colors = materials.Select(m => m.GetColor("_Color")).ToArray();
            var driver = root.AddComponent<MToonColorSync>(); driver.Setup(root);
            Shader.SetGlobalColor("_SunColor", Color.white * 3); Shader.SetGlobalColor("_AmbientColor", Color.white * .5f);
            for (int frame = 0; frame < 8; frame++) yield return null;
            float driverDelta = 0;
            var driven = Materials(root);
            foreach (var material in driven)
            {
                int index = Array.FindIndex(materials, m => m.name.Replace(" (Instance)", "") == material.name.Replace(" (Instance)", ""));
                Check(index >= 0, "Driver created an unknown material");
                driverDelta = Mathf.Max(driverDelta, Mathf.Abs(material.GetColor("_Color").r-colors[index].r));
            }
            if (!baseline)
            {
                Check(driverDelta == 0 && driven.SequenceEqual(materials), "Already lit MToon was multiplied/cloned by the legacy color driver");
                var proxy = root.GetComponent<VRMBlendShapeProxy>();
                proxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Joy), 1);
                yield return null;
                proxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Joy), 0);
                yield return null;
                for (int i = 0; i < materials.Length; i++) Check(materials[i].GetColor("_Color").Equals(colors[i]), "Expression reset changed baseline");
                driver.Setup(root);
                Check(Materials(root).SequenceEqual(materials), "Repeat driver setup allocated MToon materials");
            }
            report.Add(Path.GetFileName(path) + $": {(baseline ? "baseline" : "fixed")} {reduced} above-limit properties, {preserved} below-limit; global sun driver maxDelta={driverDelta:F6}");
            foreach (var renderingPath in new[] {RenderingPath.Forward, RenderingPath.DeferredShading})
            {
                camera.renderingPath = renderingPath;
                Color[] unlitNight = null;
                foreach (bool lit in new[] {true, false})
                foreach (bool day in new[] {false, true})
                {
                    sun.intensity = day ? 1.5f : .12f; point.intensity = day ? 3 : .02f;
                    RenderSettings.ambientLight = Color.white * (day ? .5f : .02f);
                    Shader.SetGlobalColor("_SunColor", Color.white * (day ? 2.5f : .15f));
                    Shader.SetGlobalColor("_AmbientColor", Color.white * (day ? .5f : .02f));
                    AvatarRendering.Set(lit, true, false);
                    for (int frame = 0; frame < 3; frame++) yield return null;
                    string name = Path.GetFileNameWithoutExtension(path) + "-" + renderingPath + (lit ? "-lit" : "-unlit") + (day ? "-day" : "-night");
                    var pixels = Capture(camera, target, Path.Combine(output, name + ".png"));
                    var visible = pixels.Where(c => Peak(c) > .01f).ToArray();
                    Check(visible.Length > 500, "No rendered avatar pixels");
                    report.Add(name + $": mean={visible.Average(c=>Peak(c)):F6}, over1={visible.Count(c=>Peak(c)>1f)}/{visible.Length}");
                    if (!lit && !day) unlitNight = pixels;
                    if (!lit && day)
                    {
                        float difference = pixels.Zip(unlitNight, (a,b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b))).Max();
                        report.Add(name + $": unlit day/night maxDelta={difference:F8}");
                        if (!baseline) Check(difference < .0001f, "Scene-lighting off still reacts to sun/ambient/additional lights");
                    }
                }
            }
            if (!baseline)
            {
                // Shader round trips must retain expression material identities and authored coverage.
                var queues=materials.Select(m=>m.renderQueue).ToArray();
                var textures=materials.Select(m=>m.GetTexture("_MainTex")).ToArray();
                var keywords=materials.Select(m=>string.Join("|",m.shaderKeywords.OrderBy(k=>k))).ToArray();
                for(int repeat=0;repeat<5;repeat++)
                foreach(bool lighting in new[]{false,true}) foreach(bool shadows in new[]{false,true})
                {
                    AvatarRendering.Set(lighting,shadows,false);
                    Check(Materials(root).SequenceEqual(materials),"Rendering toggle replaced materials");
                    for(int i=0;i<materials.Length;i++)
                    {
                        var m=materials[i];
                        Check(m.shader.name==(lighting&&shadows?"VRM/MToon":"ValheimVRM/MToonOptions"),"Wrong legacy shader after toggle");
                        Check(m.renderQueue==queues[i] && m.GetTexture("_MainTex")==textures[i] && m.GetColor("_Color").Equals(colors[i]),"Toggle lost material state");
                        Check(string.Join("|",m.shaderKeywords.OrderBy(k=>k))==keywords[i],"Toggle lost legacy shader keywords");
                    }
                }
                report.Add(Path.GetFileName(path)+": 20 render toggle combinations, material/texture/alpha/queue/keywords and expression baseline preserved");
            }
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)),"Input VRM was modified");
            Object.Destroy(root); yield return null; yield return null;
            File.WriteAllLines(Path.Combine(output,"results.txt"),report);
        }
        Object.Destroy(camera.gameObject); target.Release(); Object.Destroy(target);
        Object.Destroy(sun.gameObject); Object.Destroy(point.gameObject);
        AvatarRendering.Set(true,true,false);
    }
    static Color[] Capture(Camera camera, RenderTexture target, string path)
    {
        camera.Render(); var previous=RenderTexture.active; RenderTexture.active=target;
        var read=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
        read.ReadPixels(new Rect(0,0,target.width,target.height),0,0); read.Apply();
        var colors=read.GetPixels();
        var png=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        png.SetPixels(colors.Select(c=>c.gamma).ToArray()); png.Apply(); File.WriteAllBytes(path,png.EncodeToPNG());
        Object.Destroy(png); Object.Destroy(read); RenderTexture.active=previous; return colors;
    }
}
