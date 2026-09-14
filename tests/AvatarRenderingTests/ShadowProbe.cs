using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

static class ShadowProbe
{
    static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name; go.layer = 30; go.transform.position = position; go.transform.localScale = scale;
        return go;
    }
    public static IEnumerator Run(Material original, string output)
    {
        File.WriteAllLines(Path.Combine(output, "cameras.txt"), Array.ConvertAll(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None), c => c.name + " path=" + c.renderingPath + " far=" + c.farClipPlane + " depth=" + c.depthTextureMode + " AO=" + c.GetComponent<AmplifyOcclusionEffect>()?.PerPixelNormals));
        foreach (var env in Object.FindObjectsByType<EnvMan>(FindObjectsSortMode.None)) env.enabled = false;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.gray * .05f;
        RenderSettings.fog = false;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 50;
        QualitySettings.shadowCascades = 4;
        QualitySettings.pixelLightCount = 8;
        var light = new GameObject("ShadowProbeLight").AddComponent<Light>();
        light.type = LightType.Directional; light.color = Color.white; light.intensity = .4f;
        light.transform.rotation = Quaternion.Euler(15, 180, 0); light.shadows = LightShadows.Hard;
        light.shadowBias = .01f; light.shadowNormalBias = .01f; light.cullingMask = 1 << 30;
        light.shadowCustomResolution = 2048;
        light.renderMode = LightRenderMode.ForcePixel;
        RenderSettings.reflectionIntensity = 0;
        RenderSettings.sun = light;
        var camera = new GameObject("ShadowProbeCamera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 1, 4);
        camera.transform.LookAt(new Vector3(0, 1, 0));
        camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.gray; camera.nearClipPlane = .1f; camera.farClipPlane = 30;
        camera.fieldOfView = 40; camera.allowHDR = true; camera.depthTextureMode = DepthTextureMode.Depth;
        var texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGBFloat);
        camera.targetTexture = texture;
        var sphere = Primitive(PrimitiveType.Sphere, "Foreground", new Vector3(0, 1, 0), Vector3.one * 1.6f);
        var material = new Material(original);
        bool legacy=original.shader.name=="VRM/MToon";
        Func<string,string> Property = name => {
            if(!legacy)return name;
            switch(name) {
                case "_ShadeTex": return "_ShadeTexture";
                case "_ShadingShiftFactor": return "_ShadeShift";
                case "_ShadingToonyFactor": return "_ShadeToony";
                case "_UvAnimScrollXSpeed": return "_UvAnimScrollX";
                case "_UvAnimScrollYSpeed": return "_UvAnimScrollY";
                case "_UvAnimRotationSpeed": return "_UvAnimRotation";
                case "_AlphaMode": return "_BlendMode";
                case "_M_ZWrite": return "_ZWrite";
                case "_M_SrcBlend": return "_SrcBlend";
                case "_M_DstBlend": return "_DstBlend";
                default: return name;
            }
        };
        material.SetTexture("_MainTex", Texture2D.whiteTexture);
        material.SetTexture(Property("_ShadeTex"), Texture2D.whiteTexture);
        material.SetColor("_Color", Color.white);
        material.SetColor("_ShadeColor", Color.gray * .1f);
        material.SetFloat(Property("_ShadingShiftFactor"), 0);
        material.SetFloat(Property("_ShadingToonyFactor"), .6f);
        material.DisableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale", 0);
        sphere.GetComponent<Renderer>().sharedMaterial = material;
        var background = Primitive(PrimitiveType.Cube, "Background", new Vector3(0, 1, -4), new Vector3(12, 12, .1f));
        background.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard"));
        var blocker = Primitive(PrimitiveType.Cube, "BehindModelShadowCaster", new Vector3(0, 1, -2), new Vector3(.5f, 4, .1f));
        blocker.GetComponent<Renderer>().sharedMaterial = background.GetComponent<Renderer>().sharedMaterial;
        blocker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        var lines = new List<string> { "Shader "+original.shader.name };
        foreach (var path in new[] { RenderingPath.Forward, RenderingPath.DeferredShading })
        foreach (var cast in new[] { ShadowCastingMode.On, ShadowCastingMode.Off })
        {
            camera.renderingPath = path;
            sphere.GetComponent<Renderer>().shadowCastingMode = cast;
            string label = path + "-" + cast;
            blocker.SetActive(false);
            yield return null;
            var clear = Capture(camera, texture, Path.Combine(output, label + "-clear.png"));
            blocker.SetActive(true);
            yield return null;
            var blocked = Capture(camera, texture, Path.Combine(output, label + "-background-shadow.png"));
            float difference = 0;
            for (int y = 215; y < 297; y++) for (int x = 230; x < 282; x++)
            {
                var a = clear[y * 512 + x]; var b = blocked[y * 512 + x];
                difference = Mathf.Max(difference, Mathf.Abs(a.r-b.r), Mathf.Abs(a.g-b.g), Mathf.Abs(a.b-b.b));
            }
            lines.Add(label + " backgroundShadowForegroundMaxDelta=" + difference.ToString("F6"));
            lines.Add(label + " clearCenter=" + clear[256*512+256] + " blockedCenter=" + blocked[256*512+256]);
            blocker.transform.position = new Vector3(0, 1, 1);
            yield return null;
            var realShadow = Capture(camera, texture, Path.Combine(output, label + "-front-shadow.png"));
            lines.Add(label + " frontShadowCenter=" + realShadow[256*512+256]);
            blocker.transform.position = new Vector3(0, 1, -2);
            sphere.SetActive(false);
            yield return null;
            Capture(camera, texture, Path.Combine(output, label + "-background-only.png"));
            sphere.SetActive(true);
        }
        var ao = camera.gameObject.AddComponent<AmplifyOcclusionEffect>();
        var avatarSurface = sphere.AddComponent<ValheimVRM.AvatarRenderingTarget>();
        ao.enabled = false;
        blocker.transform.position = new Vector3(0, 1, 1);
        sphere.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        foreach (bool receive in new[] { true, false, true })
        {
            ValheimVRM.AvatarRendering.Set(true, receive, false);
            blocker.SetActive(false); yield return null;
            var clear = Capture(camera, texture, null);
            blocker.SetActive(true); yield return null;
            var shadow = Capture(camera, texture, null);
            float difference = Mathf.Abs(clear[256*512+256].r-shadow[256*512+256].r);
            lines.Add("ReceiveShadows=" + receive + " shadowDelta=" + difference);
            if ((receive && difference < .01f) || (!receive && difference > .0001f))
                throw new Exception("Shadow toggle failed: " + difference);
        }
        avatarSurface.enabled = false;
        ao.enabled = true;
        ao.FilterEnabled = false;
        ao.PerPixelNormals = AmplifyOcclusionEffect.PerPixelNormalSource.GBuffer;
        blocker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        blocker.transform.position = new Vector3(0, 1, -3.7f);
        blocker.transform.localScale = new Vector3(2, 4, .2f);
        blocker.transform.rotation = Quaternion.Euler(35, 55, 20);
        light.shadows = LightShadows.None;
        foreach (bool fix in new[] { false, true })
        foreach (var method in new[] { AmplifyOcclusionEffect.ApplicationMethod.PostEffect, AmplifyOcclusionEffect.ApplicationMethod.Deferred })
        {
            avatarSurface.enabled = fix;
            string prefix = method + "-" + fix;
            ao.ApplyMethod = method;
            blocker.SetActive(false); yield return null;
            var clear = Capture(camera, texture, Path.Combine(output, prefix + "-AO-clear.png"));
            blocker.SetActive(true); yield return null;
            var blocked = Capture(camera, texture, Path.Combine(output, prefix + "-AO-blocked.png"));
            float difference = 0;
            for (int y = 215; y < 297; y++) for (int x = 230; x < 282; x++) difference = Mathf.Max(difference, Mathf.Abs(clear[y*512+x].r-blocked[y*512+x].r));
            lines.Add(prefix + " backgroundAOForegroundDelta=" + difference);
            if (method == AmplifyOcclusionEffect.ApplicationMethod.PostEffect && ((!fix && difference < .01f) || (fix && difference > .0001f)))
                throw new Exception("AO regression control failed: " + prefix + " delta=" + difference);
            sphere.SetActive(false); blocker.SetActive(false); yield return null;
            var bgClear = Capture(camera, texture, Path.Combine(output, method + "-AO-background-clear.png"));
            blocker.SetActive(true); yield return null;
            Capture(camera, texture, Path.Combine(output, method + "-AO-background-blocker.png"));
            sphere.SetActive(true);
        }
        ao.enabled = false;
        blocker.SetActive(false);
        // The surface pass must not change authored lighting, transparent holes,
        // or foreground occlusion. Compare complete linear images, not PNGs.
        var alpha = new Texture2D(2,2,TextureFormat.RGBA32,false);
        alpha.filterMode = FilterMode.Point;
        alpha.SetPixels(new[] { Color.clear, Color.white, Color.white, Color.clear }); alpha.Apply();
        var wall = Primitive(PrimitiveType.Cube, "ForegroundWall", new Vector3(.4f,1,1.2f), new Vector3(.6f,4,.1f));
        wall.GetComponent<Renderer>().sharedMaterial = background.GetComponent<Renderer>().sharedMaterial;
        foreach (bool hdr in new[] { false, true })
        foreach (bool animate in new[] { false, true })
        foreach (int mode in new[] { 0, 1, 2 })
        foreach (bool occlude in new[] { false, true })
        {
            camera.allowHDR = hdr;
            material.SetFloat(Property("_UvAnimScrollXSpeed"), animate ? .13f : 0);
            material.SetFloat(Property("_UvAnimScrollYSpeed"), animate ? -.17f : 0);
            material.SetFloat(Property("_UvAnimRotationSpeed"), animate ? .11f : 0);
            material.SetFloat(Property("_AlphaMode"), mode);
            material.SetTexture("_MainTex", mode == 0 ? Texture2D.whiteTexture : alpha);
            material.SetFloat("_Cutoff", .5f);
            material.SetFloat(Property("_M_ZWrite"), mode == 2 ? 0 : 1);
            material.SetFloat(Property("_M_SrcBlend"), mode == 2 ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            material.SetFloat(Property("_M_DstBlend"), mode == 2 ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            material.renderQueue = mode == 0 ? 2000 : mode == 1 ? 2450 : 3000;
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHABLEND_ON");
            if (mode == 1) material.EnableKeyword("_ALPHATEST_ON");
            if (mode == 2) material.EnableKeyword("_ALPHABLEND_ON");
            wall.SetActive(occlude);
            yield return null;
            avatarSurface.enabled = false;
            var before = Capture(camera, texture, null);
            avatarSurface.enabled = true;
            var after = Capture(camera, texture, null);
            float maxDelta = 0;
            for (int i = 0; i < before.Length; i++) maxDelta = Mathf.Max(maxDelta, Mathf.Abs(before[i].r-after[i].r), Mathf.Abs(before[i].g-after[i].g), Mathf.Abs(before[i].b-after[i].b));
            lines.Add("color hdr=" + hdr + " animated=" + animate + " alpha=" + mode + " wall=" + occlude + " maxDelta=" + maxDelta.ToString("F8"));
            if (maxDelta > .001f) throw new Exception("Surface pass changed scene color: " + lines[lines.Count-1]);
        }
        File.WriteAllLines(Path.Combine(output, "shadow-results.txt"), lines);
    }
    static Color[] Capture(Camera camera, RenderTexture target, string path)
    {
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var read = new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
        read.ReadPixels(new Rect(0,0,target.width,target.height),0,0); read.Apply();
        var colors = read.GetPixels();
        if (path != null)
        {
            var png = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            png.SetPixels(colors); png.Apply(); File.WriteAllBytes(path, png.EncodeToPNG()); Object.Destroy(png);
        }
        Object.Destroy(read); RenderTexture.active = previous;
        return colors;
    }
}
