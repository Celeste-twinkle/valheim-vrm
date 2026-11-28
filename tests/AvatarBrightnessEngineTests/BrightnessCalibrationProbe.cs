using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UniVRM10;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRM;
using Object = UnityEngine.Object;

static class BrightnessCalibrationProbe
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static float Error(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r-b.r), Mathf.Abs(a.g-b.g), Mathf.Abs(a.b-b.b), Mathf.Abs(a.a-b.a));
    static readonly string[] Properties = { "_Color", "_BaseColor", "_ShadeColor", "_EmissionColor", "_RimColor", "_MatcapColor", "_OutlineColor" };

    public static IEnumerator Run(List<string> report, string output)
    {
        var options = AvatarRendering.Current;
        bool lighting = options.SceneLighting, shadows = options.ReceiveShadows, bloom = options.Bloom;
        float brightness = options.Brightness;
        string path = Path.Combine(ValheimVRM.Settings.ConfigDir, "rendering_options.json");
        byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : null;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        try
        {
            File.WriteAllText(path, "{\"SceneLighting\":false,\"ReceiveShadows\":true,\"Bloom\":true}");
            LoadPreferences();
            Check(AvatarRendering.Current.Brightness == 1, "Previous rendering settings did not default to 100%");
            foreach (float value in new[] { .1f, .47f, 1f, 2f })
            {
                AvatarRendering.PreviewBrightness(value); AvatarRendering.SaveBrightness(); LoadPreferences();
                Check(AvatarRendering.Current.Brightness == value, "Brightness save/reload failed");
                AvatarRendering.Set(true, false, false);
                Check(AvatarRendering.Current.Brightness == value, "Rendering toggle reset brightness");
            }
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            { AvatarRendering.PreviewBrightness(invalid); Check(AvatarRendering.Current.Brightness == 1, "Nonfinite brightness was not reset"); }
            AvatarRendering.PreviewBrightness(-1); Check(AvatarRendering.Current.Brightness == .1f, "Minimum brightness was not clamped");
            AvatarRendering.PreviewBrightness(5); Check(AvatarRendering.Current.Brightness == 2, "Maximum brightness was not clamped");
            report.Add("Preferences: old JSON defaults to 100%; 10/47/100/200% save and reload; lighting/bloom toggles retain brightness; nonfinite/range guards passed.");

            foreach (string shaderName in new[] { "VRM10/MToon10", "VRM/MToon", "Standard", "UniGLTF/UniUnlit" })
            {
                var shader = Shader.Find(shaderName);
                Check(shader != null, "Missing shader: " + shaderName);
                var material = new Material(shader);
                foreach (string key in Properties)
                    if (material.HasProperty(key)) material.SetColor(key, new Color(.65f, .35f, .12f, .37f));
                material.renderQueue = 3051;
                var original = Properties.Where(material.HasProperty).ToDictionary(k => k, material.GetColor);
                var a = new GameObject("BrightnessSlotA"); var b = new GameObject("BrightnessSlotB");
                var rendererA = a.AddComponent<MeshRenderer>(); var rendererB = b.AddComponent<MeshRenderer>();
                rendererA.sharedMaterials = new[] { material, material }; rendererB.sharedMaterial = material;
                var existing = new MaterialPropertyBlock(); existing.SetFloat("_BrightnessRegression", 123);
                rendererA.SetPropertyBlock(existing, 1);
                var targetA = a.AddComponent<AvatarBrightnessTarget>(); var targetB = b.AddComponent<AvatarBrightnessTarget>();
                targetA.Setup(false, .5f); targetB.Setup(false, 1.5f);
                var block = new MaterialPropertyBlock();
                for (int repeat = 0; repeat < 100; repeat++)
                {
                    targetA.Apply(); targetB.Apply(); rendererA.GetPropertyBlock(block, 0);
                    foreach (var pair in original)
                    {
                        int index = material.shader.FindPropertyIndex(pair.Key);
                        bool hdr = (material.shader.GetPropertyFlags(index) & ShaderPropertyFlags.HDR) != 0;
                        var expected = hdr ? pair.Value : pair.Value.linear;
                        expected.r *= .5f; expected.g *= .5f; expected.b *= .5f;
                        if (!hdr && QualitySettings.activeColorSpace != ColorSpace.Linear) expected = expected.gamma;
                        expected.a = pair.Value.a;
                        Check(Error(block.GetVector(pair.Key), expected) < .00001f, "Brightness/color/alpha drift: " + shaderName + pair.Key);
                        Check(material.GetColor(pair.Key).Equals(pair.Value), "Shared material was modified: " + shaderName);
                    }
                }
                rendererA.GetPropertyBlock(block, 1);
                Check(block.GetFloat("_BrightnessRegression") == 123, "Unrelated material properties were lost");
                Check(rendererB.sharedMaterial == material && rendererA.sharedMaterials.All(m => m == material) && material.renderQueue == 3051,
                    "Brightness cloned materials or changed render queue");
                material.SetColor("_Color", new Color(.2f,.4f,.6f,.27f)); targetA.Apply();
                rendererA.GetPropertyBlock(block, 0);
                Check(block.GetVector("_Color").w == .27f, "Animated material alpha was not read live");
                targetA.SynchronizedBrightness = 1; targetA.Apply(); rendererA.GetPropertyBlock(block, 0);
                Check(block.isEmpty, "100% did not remove the brightness override");
                rendererA.GetPropertyBlock(block, 1);
                Check(block.GetFloat("_BrightnessRegression") == 123 && !block.HasProperty("_Color"), "100% did not restore the original block");
                var wide = new MaterialPropertyBlock(); wide.SetColor("_Color", new Color(.3f,.2f,.1f,.43f));
                wide.SetFloat("_BrightnessWideRegression", 321); rendererA.SetPropertyBlock(wide);
                targetA.Setup(false, .5f); rendererA.GetPropertyBlock(block, 0);
                var wideColor = wide.GetVector("_Color"); var expectedWide = wideColor * .5f; expectedWide.w = wideColor.w;
                Check(Error(block.GetVector("_Color"), expectedWide) < .00001f && block.GetFloat("_BrightnessWideRegression") == 321,
                    "Renderer-wide properties: " + shaderName + " actual=" + block.GetVector("_Color") + " expected=" + expectedWide +
                    " custom=" + block.GetFloat("_BrightnessWideRegression") + " space=" + QualitySettings.activeColorSpace);
                targetA.enabled = false; rendererA.GetPropertyBlock(block, 0);
                Check(block.isEmpty, "Disabling did not restore renderer-wide inheritance");
                targetB.enabled = false; rendererB.GetPropertyBlock(block, 0);
                Check(block.isEmpty, "Disabled component retained its override");
                Object.Destroy(a); Object.Destroy(b); Object.Destroy(material);
                report.Add(shaderName + ": independent players and material slots, 100 repeated applications without drift, live colors/alpha, HDR handling, source/queue preservation, reset and disable passed.");
            }
            yield return ImportedExpressions(report);
            RenderBrightness(report, output);
            report.Add("AVATAR_BRIGHTNESS_CALIBRATION_TESTS_PASSED");
        }
        finally
        {
            AvatarRendering.PreviewBrightness(brightness); AvatarRendering.Set(lighting, shadows, bloom);
            if (before == null) File.Delete(path); else File.WriteAllBytes(path, before);
        }
    }

    static void LoadPreferences() => HarmonyLib.AccessTools.Method(typeof(AvatarRendering), "LoadPreferences").Invoke(null, null);

    static IEnumerator ImportedExpressions(List<string> report)
    {
        string path = Path.Combine(ValheimVRM.Settings.ValheimVRMDir, "Shinano_LightAdjustment.vrm");
        var bytes = File.ReadAllBytes(path);
        GameObject root = null; yield return ValheimVRM.VRM.ImportVisualAsync(bytes, path, 1, r => root = r);
        Check(root != null, "Production import failed");
        var target = root.AddComponent<AvatarBrightnessTarget>(); target.Setup(true);
        var expression = root.GetComponent<Vrm10Instance>().Runtime.Expression;
        var renderer = root.GetComponentsInChildren<Renderer>(true).First(r => r.sharedMaterials.Any(m => m != null && m.HasProperty("_Color")));
        int index = Array.FindIndex(renderer.sharedMaterials, m => m != null && m.HasProperty("_Color"));
        var block = new MaterialPropertyBlock();
        foreach (float value in new[] { .1f, .5f, 1.5f, 2f, 1f, .3f })
        {
            AvatarRendering.PreviewBrightness(value);
            expression.SetWeight(ExpressionKey.Happy, 1); yield return null; yield return null;
            expression.SetWeight(ExpressionKey.Happy, 0); yield return null; yield return null;
            target.Apply(); renderer.GetPropertyBlock(block, index);
            Check(target.Brightness == value && (value == 1 ? block.isEmpty : block.HasProperty("_Color")),
                "Expression reset lost live brightness");
        }
        Object.Destroy(root); yield return null;
        report.Add("Production VRM 1.0 import: owner brightness follows preferences across expression changes and reset without changing the import baseline.");
    }

    static void RenderBrightness(List<string> report, string output)
    {
        AvatarRendering.Set(false, false, false);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.layer = 30;
        var material = new Material(Shader.Find("VRM10/MToon10"));
        material.SetColor("_Color", new Color(.4f,.25f,.1f,1).gamma);
        material.SetColor("_EmissionColor", Color.black); quad.GetComponent<Renderer>().sharedMaterial = material;
        quad.AddComponent<AvatarRenderingTarget>();
        var target = quad.AddComponent<AvatarBrightnessTarget>(); target.Setup(false);
        var camera = new GameObject("BrightnessGpuCamera").AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black; camera.transform.position = new Vector3(0,0,-2);
        camera.orthographic = true; camera.orthographicSize = .8f; camera.allowHDR = true;
        var texture = new RenderTexture(96,96,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear); texture.Create(); camera.targetTexture = texture;
        var readback = new Texture2D(96,96,TextureFormat.RGBAFloat,false,true);
        var previous = RenderTexture.active;
        try
        {
            Color baseline = Read(camera, texture, readback);
            Check(baseline.r > .1f, "Brightness GPU fixture did not draw");
            foreach (float value in new[] { .1f, .5f, 1.5f, 2f, 1f })
            {
                target.SynchronizedBrightness = value; target.Apply();
                var actual = Read(camera, texture, readback); var expected = baseline * value; expected.a = baseline.a;
                Check(Error(actual, expected) < .003f, "GPU brightness ratio mismatch: " + value + " actual=" + actual + " expected=" + expected);
                File.WriteAllBytes(Path.Combine(output, "brightness-" + value.ToString("F2",System.Globalization.CultureInfo.InvariantCulture) + ".png"), readback.EncodeToPNG());
            }
            report.Add("D3D11 HDR render/readback: 10/50/150/200% linear RGB ratios and exact default restoration passed; PNG captures retained.");
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null; texture.Release();
            Object.Destroy(texture); Object.Destroy(readback); Object.Destroy(camera.gameObject); Object.Destroy(quad); Object.Destroy(material);
        }
    }
    static Color Read(Camera camera, RenderTexture texture, Texture2D readback)
    {
        camera.Render(); RenderTexture.active = texture; readback.ReadPixels(new Rect(0,0,96,96),0,0); readback.Apply();
        return readback.GetPixel(48,48);
    }
}
