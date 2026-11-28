using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.PostProcessing;
using ValheimVRM;
using Object = UnityEngine.Object;

[BepInPlugin("valheimvrm.tests.antialiasing", "Avatar antialiasing regression tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public class AvatarAntialiasingTests : BaseUnityPlugin
{
    string output;
    static bool forceLegacy;
    static bool? projectionUsed;
    static string filteredCapture;
    readonly List<string> report = new List<string>();
    void Awake()
    {
        output = Environment.GetEnvironmentVariable("VRM_AA_TEST_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        Directory.CreateDirectory(output);
        var patches = new Harmony("valheimvrm.tests.antialiasing.isolation");
        patches.Patch(AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupported"), prefix: new HarmonyMethod(typeof(AvatarAntialiasingTests), nameof(NoCloud)));
        patches.Patch(AccessTools.Method(typeof(TaaComponent), "SetProjectionMatrix"), postfix: new HarmonyMethod(typeof(AvatarAntialiasingTests), nameof(Projection)) { priority = Priority.Last });
        patches.Patch(AccessTools.Method(typeof(PatchAvatarBloom), "Prefix"), postfix: new HarmonyMethod(typeof(AvatarAntialiasingTests), nameof(FilteredInput)));
    }
    static bool NoCloud(ref bool __result) { __result = false; return false; }
    static void FilteredInput(RenderTexture __2) { if (filteredCapture != null && __2 != null) Save(__2, filteredCapture); }
    static void Projection(TaaComponent __instance)
    {
        if (forceLegacy) __instance.context.camera.useJitteredProjectionMatrixForTransparentRendering = false;
        projectionUsed = __instance.context.camera.useJitteredProjectionMatrixForTransparentRendering;
    }
    void Log(string line) { report.Add(line); File.WriteAllLines(Path.Combine(output, "results.txt"), report); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            object value = null; bool more;
            try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString()); Logger.LogError(e); Application.Quit(1); yield break; }
            if (!more) stack.Pop(); else if (value is IEnumerator nested) stack.Push(nested); else yield return value;
        }
        Log("AVATAR_ANTIALIASING_TESTS_PASSED"); Application.Quit(0);
    }
    IEnumerator Run()
    {
        FejdStartup menu; float deadline = Time.realtimeSinceStartup + 90;
        while ((menu = Object.FindFirstObjectByType<FejdStartup>()) == null || !VRMShaders.Shaders.ContainsKey("VRM10/MToon10"))
        { if (Time.realtimeSinceStartup > deadline) throw new Exception("Menu timeout"); yield return null; }
        report.Add("GPU " + SystemInfo.graphicsDeviceName + " API " + SystemInfo.graphicsDeviceType + " skin " + QualitySettings.skinWeights);
        // Do not inherit the isolated game's previous F8 test preferences.
        AvatarRendering.Current.SceneLighting = true;
        AvatarRendering.Current.ReceiveShadows = true;
        QualitySettings.pixelLightCount = 8;
        foreach (var pp in Object.FindObjectsByType<PostProcessingBehaviour>(FindObjectsSortMode.None)) report.Add("GAME AA " + pp.profile.antialiasing.settings.method);
        var prefab = (GameObject)AccessTools.Field(typeof(FejdStartup), "m_playerPrefab").GetValue(menu);
        var template = prefab.GetComponentInChildren<Animator>(true);
        var holder = new GameObject("LayerProbeHolder"); holder.SetActive(false);
        var sourceObject = Object.Instantiate(template.gameObject, holder.transform);
        foreach (var behaviour in sourceObject.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        foreach (var renderer in sourceObject.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        holder.SetActive(true);
        var source = sourceObject.GetComponent<Animator>();
        source.cullingMode = AnimatorCullingMode.AlwaysAnimate; source.applyRootMotion = false;
        source.Rebind(); source.Play(229373857, 0, .35f); source.Update(0); source.enabled = false;
        string path = Environment.GetEnvironmentVariable("VRM_AA_TEST_MODEL") ?? Path.Combine(Paths.GameRootPath, "ValheimVRM", "Shinano_LightAdjustment.vrm");
        GameObject model = null;
        yield return ValheimVRM.VRM.ImportVisualAsync(File.ReadAllBytes(path), path, 1, root => model = root);
        if (model == null) throw new Exception("Import failed");
        model.transform.SetParent(holder.transform, false); model.SetActive(true);
        foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        var sync = model.AddComponent<VRMAnimationSync>();
        sync.Setup(source, new ValheimVRM.Settings.VrmSettingsContainer { ModelScale = 1, ModelOffsetY = 0, PlayerHeight = 1.85f }); sync.enabled = false;
        AccessTools.Method(typeof(VRMAnimationSync), "LateUpdate").Invoke(sync, null);
        model.GetComponent<Animator>().enabled = false;
        foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        string shaderOverride=Environment.GetEnvironmentVariable("VRM_AA_SHADER");
        var converted=new HashSet<Material>();
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.updateWhenOffscreen = true;
            report.Add("MESH " + renderer.name + " quality=" + renderer.quality);
            foreach (var m in renderer.sharedMaterials)
            {
                if(!string.IsNullOrEmpty(shaderOverride) && converted.Add(m))
                {
                    int mode=(int)m.GetFloat(m.HasProperty("_BlendMode")?"_BlendMode":"_AlphaMode");
                    // Legacy MToon mode 3 is TransparentWithZWrite. The common
                    // fixture uses regular Blend; never leave a transparent tag
                    // on a keyword-opaque Standard/UniUnlit material.
                    mode=Math.Min(mode,2);
                    var replacement=Shader.Find(shaderOverride);
                    if(replacement==null)throw new Exception("Fixture shader unavailable: "+shaderOverride);
                    m.shader=replacement;
                    m.shaderKeywords=new string[0];
                    m.SetFloat(shaderOverride=="Standard"?"_Mode":"_BlendMode",mode);
                    m.SetFloat("_SrcBlend",mode==2?(float)BlendMode.SrcAlpha:1);m.SetFloat("_DstBlend",mode==2?(float)BlendMode.OneMinusSrcAlpha:0);m.SetFloat("_ZWrite",mode==2?0:1);
                    m.SetOverrideTag("RenderType",mode==0?"Opaque":mode==1?"TransparentCutout":"Transparent");
                    m.DisableKeyword("_ALPHATEST_ON");m.DisableKeyword("_ALPHABLEND_ON");
                    if(mode==1)m.EnableKeyword("_ALPHATEST_ON");if(mode==2)m.EnableKeyword("_ALPHABLEND_ON");
                    if(shaderOverride=="UniGLTF/UniUnlit")m.EnableKeyword("_VERTEXCOL_MUL");
                    var color=m.GetColor("_Color");color.r*=3;color.g*=3;color.b*=3;m.SetColor("_Color",color);
                    m.renderQueue=mode==0?2000:mode==1?2450:3000;
                }
                bool legacy = m.shader.name == "VRM/MToon" || m.shader.name == "ValheimVRM/MToonOptions";
                bool common = m.shader.name == "UniGLTF/UniUnlit" || m.shader.name == "Standard";
                if (Environment.GetEnvironmentVariable("VRM_AA_BLEND_ALL") == "1" &&
                    (legacy || common || m.shader.name == "VRM10/MToon10" || m.shader.name == "ValheimVRM/MToon10Options"))
                {
                    m.SetFloat(m.shader.name=="Standard"?"_Mode":legacy || common ? "_BlendMode" : "_AlphaMode", 2);
                    m.SetFloat(legacy || common ? "_ZWrite" : "_M_ZWrite", 0);
                    m.SetFloat(legacy || common ? "_SrcBlend" : "_M_SrcBlend", (float)BlendMode.SrcAlpha);
                    m.SetFloat(legacy || common ? "_DstBlend" : "_M_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON");
                    m.renderQueue = 2450;
                    // A fixture-only override, never written to the VRM. Include
                    // both actually solid and wholly semitransparent avatars.
                    if (Environment.GetEnvironmentVariable("VRM_AA_BLEND_PARTIAL") == "1")
                    { var color=m.GetColor("_Color"); color.a=.65f; m.SetColor("_Color",color); }
                }
                report.Add("MAT " + m.name + " shader="+m.shader.name+" queue=" + m.renderQueue+" keywords="+string.Join(",",m.shaderKeywords));
            }
        }
        File.WriteAllLines(Path.Combine(output, "materials.txt"), report);
        foreach (var env in Object.FindObjectsByType<EnvMan>(FindObjectsSortMode.None)) env.enabled = false;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray * .2f;
        RenderSettings.fog = false; RenderSettings.reflectionIntensity = 0;
        var light = new GameObject("LayerProbeLight").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1; light.cullingMask = 1 << 30;
        light.transform.rotation = Quaternion.Euler(40, 150, 0); light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel; RenderSettings.sun = light;
        var camera = new GameObject("LayerProbeCamera").AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.1f, .15f, .2f); camera.nearClipPlane = .1f; camera.farClipPlane = 5000;
        camera.fieldOfView = 40; camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading;
        var texture = new RenderTexture(512, 768, 24, RenderTextureFormat.ARGBHalf); texture.Create(); camera.targetTexture = texture;
        var raw = new RenderTexture(512, 768, 0, RenderTextureFormat.ARGBHalf); raw.Create();
        var capture = new CommandBuffer { name = "Layer probe raw color" }; capture.Blit(BuiltinRenderTextureType.CameraTarget, raw); camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, capture);
        var post = camera.gameObject.AddComponent<PostProcessingBehaviour>(); post.profile = ScriptableObject.CreateInstance<PostProcessingProfile>();
        var aa = post.profile.antialiasing.settings; aa.method = AntialiasingModel.Method.Taa; post.profile.antialiasing.settings = aa;
        var surface = model.AddComponent<AvatarRenderingTarget>(); model.AddComponent<AvatarBloomTarget>();
        if (Environment.GetEnvironmentVariable("VRM_AA_BLEND_ALL") == "1")
        {
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Check(material.renderQueue > 2500, "Blended avatar still uses an opaque queue");
            Log("Fixture: every material uses Blend, authored queue 2450; normalized before draw; " +
                (Environment.GetEnvironmentVariable("VRM_AA_BLEND_PARTIAL") == "1" ? "all color alpha=0.65" : "authored alpha preserved"));
        }
        CheckOpaqueAlpha(camera, post, texture, model);
        if (Environment.GetEnvironmentVariable("VRM_AA_OPAQUE_ONLY") == "1") yield break;
        if (Environment.GetEnvironmentVariable("VRM_AA_TEMPORAL_ONLY") == "1")
        {
            yield return TemporalBloom(camera, post, texture, raw, model, holder, light);
            yield break;
        }
        var taa = (TaaComponent)AccessTools.Field(typeof(PostProcessingBehaviour), "m_Taa").GetValue(post);
        int samples = 0, reproduced = 0;
        foreach (int origin in new[] { 0, 3000 })
            foreach (int angle in new[] { 0, 180 })
                foreach (bool bloom in new[] { false, true })
                    foreach (float intensity in new[] { 1f, 5f })
                        foreach (int sample in new[] { 1, 3, 5, 7 })
                        {
                            holder.transform.position = new Vector3(origin, 50, origin); var center = holder.transform.position + new Vector3(0, AvatarScale.MinimumHeight * .6f, 0);
                            camera.transform.position = center + Quaternion.Euler(0, angle, 0) * new Vector3(0, 0, AvatarScale.MinimumHeight * 2.2f); camera.transform.LookAt(center);
                            light.intensity = intensity;
                            post.profile.bloom.enabled = true;
                            AvatarBloomController.Enabled = !bloom;
                            camera.ResetProjectionMatrix();
                            var unjittered = camera.projectionMatrix;
                            // Use the actual game's Halton sample and projection construction.
                            taa.Init(new PostProcessingContext { camera = camera, materialFactory = new MaterialFactory(), renderTextureFactory = new RenderTextureFactory() }, post.profile.antialiasing);
                            var offset = new Vector2((float)AccessTools.Method(typeof(TaaComponent), "GetHaltonValue").Invoke(taa, new object[] { sample, 2 }),
                                (float)AccessTools.Method(typeof(TaaComponent), "GetHaltonValue").Invoke(taa, new object[] { sample, 3 })) * aa.taaSettings.jitterSpread;
                            var jittered = (Matrix4x4)AccessTools.Method(typeof(TaaComponent), "GetPerspectiveProjectionMatrix").Invoke(taa, new object[] { offset });
                            taa.context.materialFactory.Dispose(); taa.context.renderTextureFactory.Dispose();
                            post.jitteredMatrixFunc = _ => jittered;
                            yield return null;
                            Color[] reference = null, legacy = null, actual = null;
                            // Reference: render both layers using the same projection without
                            // TAA's transparent override. Compare scene HDR before post effects.
                            foreach (int mode in new[] { 0, 1, 2 })
                            {
                                post.profile.antialiasing.enabled = mode != 0; forceLegacy = mode == 1;
                                camera.projectionMatrix = mode == 0 ? jittered : unjittered;
                                camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
                                camera.useJitteredProjectionMatrixForTransparentRendering = false;
                                AccessTools.Field(typeof(TaaComponent), "m_SampleIndex").SetValue(taa, sample); post.ResetTemporalEffects();
                                projectionUsed = null;
                                camera.Render();
                                if (mode != 0) Check(!camera.useJitteredProjectionMatrixForTransparentRendering, "Projection state leaked after camera render");
                                if (mode == 2) Check(projectionUsed == true, "Production TAA patch did not match the visible avatar projection");
                                var pixels = Read(raw);
                                if (mode == 0) reference = pixels; else if (mode == 1) legacy = pixels; else actual = pixels;
                                if (origin == 0 && angle == 180 && intensity == 1 && sample == 3)
                                { Save(raw, Path.Combine(output, "bloom" + bloom + "-mode" + mode + "-raw.png")); Save(texture, Path.Combine(output, "bloom" + bloom + "-mode" + mode + "-final.png")); }
                            }
                            int bad = 0, fixedPixels = 0; float maximum = 0;
                            for (int i = 0; i < reference.Length; i++)
                            {
                                if (Delta(reference[i], legacy[i]) > .01f) bad++;
                                float d = Delta(reference[i], actual[i]); maximum = Mathf.Max(maximum, d); if (d > .001f) fixedPixels++;
                            }
                            if (bad > 100) reproduced++;
                            Log("origin=" + origin + " angle=" + angle + " bloom=" + bloom + " light=" + intensity + " sample=" + sample + " legacy changed=" + bad + " fixed changed=" + fixedPixels + " max=" + maximum);
                            Check(maximum <= .001f, "Corrected raw scene differs from matched-projection reference");
                            samples++;
                        }
        Check(reproduced > 0, "Legacy control did not reproduce layer corruption");
        Log(samples + " projection/lighting/bloom cases passed; legacy corruption reproduced in " + reproduced);
        forceLegacy = false; post.profile.antialiasing.enabled = true; post.jitteredMatrixFunc = null;
        foreach (int control in new[] { 0, 1, 2 })
        {
            surface.enabled = control != 0;
            camera.cullingMask = control == 1 ? 0 : 1 << 30;
            if (control == 2) camera.transform.rotation = Quaternion.LookRotation(-camera.transform.forward);
            projectionUsed = null; camera.Render();
            Check(projectionUsed == false, "TAA setting changed for a camera without a visible avatar");
        }
        Log("Disabled avatar target, excluded layers and avatar outside frustum retain native transparent projection");
        capture.Release(); camera.RemoveAllCommandBuffers(); Object.Destroy(model); Object.Destroy(holder); texture.Release(); raw.Release(); Object.Destroy(texture); Object.Destroy(raw); Object.Destroy(camera.gameObject); Object.Destroy(light.gameObject);
        yield return null;
    }
    void CheckOpaqueAlpha(Camera camera, PostProcessingBehaviour post, RenderTexture target, GameObject model)
    {
        var materials=model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().Where(m=>
            (m.shader.name=="VRM/MToon" && m.GetFloat("_BlendMode")==0) ||
            (m.shader.name=="VRM10/MToon10" && m.GetFloat("_AlphaMode")==0) ||
            ((m.shader.name=="UniGLTF/UniUnlit" || m.shader.name=="Standard") && !m.IsKeywordEnabled("_ALPHABLEND_ON") && !m.IsKeywordEnabled("_ALPHATEST_ON") && !m.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"))).ToArray();
        if(materials.Length==0)return;
        var colors=materials.Select(m=>m.GetColor("_Color")).ToArray();
        bool oldAa=post.profile.antialiasing.enabled,oldBloom=post.profile.bloom.enabled,oldExclude=AvatarBloomController.Enabled;
        camera.transform.position=new Vector3(0,AvatarScale.MinimumHeight*.6f,AvatarScale.MinimumHeight*2.2f);
        camera.transform.LookAt(new Vector3(0,AvatarScale.MinimumHeight*.6f,0));
        try
        {
            post.profile.antialiasing.enabled=false; post.profile.bloom.enabled=true;
            foreach(bool exclude in new[]{false,true})
            {
                AvatarBloomController.Enabled=exclude;
                Color[] reference=null;
                foreach(float alpha in new[]{1f,.5f})
                {
                    for(int i=0;i<materials.Length;i++){var color=colors[i];color.a=alpha;materials[i].SetColor("_Color",color);}
                    camera.Render(); var actual=Read(target);
                    if(reference==null){reference=actual;continue;}
                    float maximum=0;for(int i=0;i<actual.Length;i++)maximum=Mathf.Max(maximum,Delta(actual[i],reference[i]));
                    Check(maximum<.001f,"Opaque material alpha changed scene/bloom coverage: "+maximum);
                    Log("Opaque alpha 1/0.5: "+materials.Length+" materials; excludeBloom="+exclude+" finalRgbDelta="+maximum);
                }
            }
        }
        finally
        {
            for(int i=0;i<materials.Length;i++)materials[i].SetColor("_Color",colors[i]);
            post.profile.antialiasing.enabled=oldAa;post.profile.bloom.enabled=oldBloom;AvatarBloomController.Enabled=oldExclude;
            post.ResetTemporalEffects();
        }
    }
    static float Delta(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
    IEnumerator TemporalBloom(Camera camera, PostProcessingBehaviour post, RenderTexture final, RenderTexture raw, GameObject model, GameObject holder, Light light)
    {
        // Freeze actual imported/posed geometry so frame differences come from
        // rendering, not animation or asynchronous spring simulation.
        foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh(); skin.BakeMesh(mesh, false);
            var frozen = new GameObject("Frozen " + skin.name); frozen.layer = 30;
            frozen.transform.SetParent(skin.transform, false);
            frozen.AddComponent<MeshFilter>().sharedMesh = mesh;
            frozen.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
            skin.enabled = false;
            frozen.transform.SetParent(holder.transform, true);
        }
        model.SetActive(false);
        holder.AddComponent<AvatarRenderingTarget>(); holder.AddComponent<AvatarBloomTarget>();
        if (Environment.GetEnvironmentVariable("VRM_AA_DEPTH_DISABLED") == "1")
            foreach (var controller in Object.FindObjectsByType<AvatarDepthController>(FindObjectsSortMode.None)) controller.enabled = false;
        holder.transform.position = new Vector3(100, 50, 100);
        var center = holder.transform.position + new Vector3(0, AvatarScale.MinimumHeight * .6f, 0);
        camera.transform.position = center + new Vector3(0, 0, AvatarScale.MinimumHeight * 2.2f); camera.transform.LookAt(center);
        light.intensity = 5;
        post.profile.antialiasing.enabled = true; post.profile.bloom.enabled = true;
        post.jitteredMatrixFunc = null; forceLegacy = false; AvatarBloomController.Enabled = true;
        var bloom = post.profile.bloom.settings; bloom.bloom.intensity = 1; bloom.bloom.threshold = .8f; post.profile.bloom.settings = bloom;
        post.ResetTemporalEffects();
        var means = new List<float>(); var coverages = new List<float>();
        Color[] low = null, high = null;
        for (int frame = 0; frame < 96; frame++)
        {
            yield return null; camera.ResetProjectionMatrix();
            filteredCapture = frame < 32 ? null : Path.Combine(output, "continuous-" + (frame - 32).ToString("D3") + "-filtered.png");
            camera.Render(); filteredCapture = null;
            if (frame < 32) continue;
            var limiter = camera.GetComponent<AvatarBloomCamera>();
            var mask = (RenderTexture)AccessTools.Property(typeof(AvatarBloomCamera), "BloomMask").GetValue(limiter);
            var pixels = Read(final); var coverage = Read(mask);
            if (low == null) { low = (Color[])pixels.Clone(); high = (Color[])pixels.Clone(); }
            float mean = 0, sum = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                mean += (pixels[i].r + pixels[i].g + pixels[i].b) / 3; sum += coverage[i].r;
                low[i] = new Color(Mathf.Min(low[i].r, pixels[i].r), Mathf.Min(low[i].g, pixels[i].g), Mathf.Min(low[i].b, pixels[i].b));
                high[i] = new Color(Mathf.Max(high[i].r, pixels[i].r), Mathf.Max(high[i].g, pixels[i].g), Mathf.Max(high[i].b, pixels[i].b));
            }
            means.Add(mean / pixels.Length); coverages.Add(sum / pixels.Length);
            string stem = Path.Combine(output, "continuous-" + (frame - 32).ToString("D3"));
            Save(final, stem + "-final.png"); Save(mask, stem + "-mask.png"); Save(raw, stem + "-raw.png");
            if (frame == 32)
            {
                Save(raw, Path.Combine(output, "continuous-raw.png"));
                float hdr = Read(raw).Max(c => Mathf.Max(c.r, c.g, c.b)); Log("Strong-light raw HDR peak=" + hdr);
                Check(hdr > 1, "Strong-light fixture does not cross the bloom threshold");
            }
            Log("frame=" + (frame - 32) + " mean=" + means.Last() + " mask=" + coverages.Last());
        }
        float range = 0; foreach (int i in Enumerable.Range(0, low.Length)) range += Delta(low[i], high[i]); range /= low.Length;
        Log("64 consecutive frames after 32 warmup: mean range=" + (means.Max() - means.Min()) + " mask coverage range=" + (coverages.Max() - coverages.Min()) + " mean pixel temporal range=" + range);
        File.WriteAllText(Path.Combine(output, "temporal-metrics.txt"), (means.Max() - means.Min()) + "," + (coverages.Max() - coverages.Min()) + "," + range);
        // The old shader loses most of the opaque body on alternating samples.
        // Allow ordinary subpixel silhouette changes, but never coverage dropout.
        if (Environment.GetEnvironmentVariable("VRM_AA_EXPECT_LEGACY") != "1")
            Check(coverages.Average() > .01f && (coverages.Max() - coverages.Min()) / coverages.Average() < .01f,
                "Bloom exclusion coverage drops out during consecutive TAA frames");
    }
    static Color[] Read(RenderTexture texture)
    {
        var previous = RenderTexture.active; RenderTexture.active = texture; var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
        read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); read.Apply(); var colors = read.GetPixels(); Object.Destroy(read); RenderTexture.active = previous; return colors;
    }
    static void Save(RenderTexture texture, string path)
    {
        var png = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
        png.SetPixels(Read(texture).Select(c => c.gamma).ToArray()); png.Apply(); File.WriteAllBytes(path, png.EncodeToPNG()); Object.Destroy(png);
    }
}
