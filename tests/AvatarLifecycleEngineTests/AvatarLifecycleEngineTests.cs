using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using ValheimVRM;
using Object = UnityEngine.Object;

[BepInPlugin("valheimvrm.tests.lifecycle", "Avatar lifecycle engine tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed partial class AvatarLifecycleEngineTests : BaseUnityPlugin
{
    const string Owner = "com.yoship1639.plugins.valheimvrm.patch";
    string output;
    bool diagnose;
    readonly List<string> report = new List<string>();
    static bool recording;
    static readonly HashSet<int> fixturePlayers = new HashSet<int>();
    static readonly List<RenderTexture> acquired = new List<RenderTexture>();
    static readonly List<RenderTexture> released = new List<RenderTexture>();
    void Awake()
    {
        output = Environment.GetEnvironmentVariable("VRM_LIFECYCLE_TEST_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        diagnose = Environment.GetEnvironmentVariable("VRM_LIFECYCLE_DIAGNOSE") == "1";
        Directory.CreateDirectory(output);
        var harmony = new Harmony("valheimvrm.tests.lifecycle.cloud");
        harmony.Patch(AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupported"),
            prefix: new HarmonyMethod(typeof(AvatarLifecycleEngineTests), nameof(NoCloud)));
        harmony.Patch(AccessTools.Method(typeof(Player), "IsDead"),
            prefix: new HarmonyMethod(typeof(AvatarLifecycleEngineTests), nameof(FixtureIsAlive)));
    }
    static bool NoCloud(ref bool __result) { __result = false; return false; }
    static bool FixtureIsAlive(Player __instance, ref bool __result)
    {
        if (__instance != null && fixturePlayers.Contains(__instance.GetInstanceID()))
        { __result = false; return false; }
        return true;
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    void Log(string value) { report.Add(value); File.WriteAllLines(Path.Combine(output, "results.txt"), report); }
    static void Acquired(RenderTexture __2) { if (recording && __2 != null) acquired.Add(__2); }
    static void Released(RenderTexture __1) { if (recording && __1 != null) released.Add(__1); }
    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count != 0)
        {
            bool more; object next = null;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Application.Quit(1); yield break; }
            if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Log("LIFECYCLE_TESTS_PASSED"); Application.Quit(0);
    }
    IEnumerator Run()
    {
        float timeout = Time.realtimeSinceStartup + 90;
        FejdStartup menu;
        while ((menu = Object.FindFirstObjectByType<FejdStartup>()) == null || !VRMShaders.Shaders.ContainsKey("VRM10/MToon10"))
        { Check(Time.realtimeSinceStartup < timeout, "Startup timeout"); yield return null; }
        if (Environment.GetEnvironmentVariable("VRM_LIFECYCLE_RESIDENCY_ONLY") == "1")
        {
            yield return CancellationTests((GameObject)AccessTools.Field(typeof(FejdStartup), "m_playerPrefab").GetValue(menu));
            yield break;
        }
        var startup = typeof(MainPlugin).Assembly.GetType("ValheimVRM.PatchFejdStartup");
        var bloom = AccessTools.DeclaredMethod(typeof(BloomComponent), "Prepare", new[] { typeof(RenderTexture), typeof(Material), typeof(Texture) });
        int before = Harmony.GetPatchInfo(bloom).Prefixes.Count(p => p.owner == Owner);
        for (int i = 0; i < 3; i++) AccessTools.Method(startup, "Postfix").Invoke(null, null);
        int after = Harmony.GetPatchInfo(bloom).Prefixes.Count(p => p.owner == Owner);
        Log("Bloom prefix registrations before/after three menu startup callbacks: " + before + "/" + after);
        if (!diagnose)
        {
            Check(before == 1 && after == 1, "Menu reentry duplicated bloom patches");
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                var info = Harmony.GetPatchInfo(method);
                var duplicates = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
                    .Where(p => p.owner == Owner).GroupBy(p => p.PatchMethod).Where(g => g.Count() > 1).ToArray();
                Check(duplicates.Length == 0, "Duplicate production patch: " + method);
            }
        }
        yield return BloomStress(diagnose ? 256 : 3840, diagnose ? 256 : 2160, diagnose ? 3 : 360);
        if (!diagnose)
        {
            yield return HeightTests();
            yield return CancellationTests((GameObject)AccessTools.Field(typeof(FejdStartup), "m_playerPrefab").GetValue(menu));
            string scene = menu.gameObject.scene.name;
            for (int i = 0; i < 3; i++)
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                Check(Harmony.GetPatchInfo(bloom).Prefixes.Count(p => p.owner == Owner) == 1,
                    "Actual menu scene reload duplicated patches");
            }
            Log("Three actual main-menu scene reloads retained exactly one bloom prefix");
        }
    }
    IEnumerator BloomStress(int width, int height, int frames)
    {
        AvatarBloomController.Enabled = true;
        var root = new GameObject("Lifecycle bloom probe");
        var camera = root.AddComponent<Camera>(); camera.enabled = false;
        var source = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf); source.Create();
        camera.targetTexture = source;
        var subject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var surface = new Material(VRMShaders.Shaders["VRM10/MToon10"]);
        subject.GetComponent<Renderer>().sharedMaterial = surface;
        subject.AddComponent<AvatarBloomTarget>();
        var limiter = root.AddComponent<AvatarBloomCamera>();
        AccessTools.Method(typeof(AvatarBloomCamera), "Prepare").Invoke(limiter, new object[] { camera });
        var context = new PostProcessingContext { camera = camera, materialFactory = new MaterialFactory(), renderTextureFactory = new RenderTextureFactory() };
        var component = new BloomComponent(); component.Init(context, new BloomModel());
        var uber = new Material(Shader.Find("Hidden/Post FX/Uber Shader"));
        var probe = new Harmony("valheimvrm.tests.lifecycle.allocations");
        probe.Patch(AccessTools.Method(typeof(PatchAvatarBloom), "Prefix"), postfix: new HarmonyMethod(typeof(AvatarLifecycleEngineTests), nameof(Acquired)));
        probe.Patch(AccessTools.Method(typeof(PatchAvatarBloom), "Finalizer"), postfix: new HarmonyMethod(typeof(AvatarLifecycleEngineTests), nameof(Released)));
        long minBytes = long.MaxValue, maxBytes = 0; int minCount = int.MaxValue, maxCount = 0;
        int leaked = 0;
        try
        {
            for (int frame = 0; frame < frames; frame++)
            {
                acquired.Clear(); released.Clear(); recording = true;
                component.Prepare(source, uber, Texture2D.whiteTexture);
                recording = false;
                context.renderTextureFactory.ReleaseAll();
                var unreleased = acquired.Where(rt => !released.Contains(rt)).ToArray();
                leaked += unreleased.Length;
                // Bound the old-build diagnostic; never exhaust the machine's memory.
                foreach (var rt in unreleased) RenderTexture.ReleaseTemporary(rt);
                if (frame == 0) Log("One bloom call acquired/release calls/unreleased: " + acquired.Count + "/" + released.Count + "/" + unreleased.Length);
                if (!diagnose) Check(acquired.Count == 1 && released.Count == 1 && unreleased.Length == 0, "Temporary bloom texture leaked or double-released");
                yield return null;
                if (frame >= 60 && frame % 30 == 0)
                {
                    var textures = Resources.FindObjectsOfTypeAll<RenderTexture>();
                    long bytes = textures.Sum(rt => Profiler.GetRuntimeMemorySizeLong(rt));
                    minBytes = Math.Min(minBytes, bytes); maxBytes = Math.Max(maxBytes, bytes);
                    minCount = Math.Min(minCount, textures.Length); maxCount = Math.Max(maxCount, textures.Length);
                    Log("Frame " + frame + " render textures=" + textures.Length + ", native bytes=" + bytes);
                }
            }
            Log("Bloom frames=" + frames + ", resolution=" + width + "x" + height + ", unbalanced allocations=" + leaked);
            if (!diagnose)
            {
                Check(maxCount - minCount <= 6, "Render texture object count grows after warmup");
                Check(maxBytes - minBytes <= 64L * 1024 * 1024, "Render texture memory grows after warmup");
                Log("4K sustained bloom allocation and release remained balanced with bounded texture count/memory");
            }
        }
        finally
        {
            recording = false; probe.UnpatchSelf(); context.renderTextureFactory.Dispose(); context.materialFactory.Dispose();
            camera.targetTexture = null; source.Release(); Object.Destroy(source); Object.Destroy(uber);
            Object.Destroy(root); Object.Destroy(subject); Object.Destroy(surface);
        }
        yield return null;
    }

    IEnumerator CancellationTests(GameObject prefab)
    {
        const string name = "Shinano_LightAdjustment";
        ValheimVRM.Settings.AddSettingsFromFile(name, false);
        GameObject imported = null;
        yield return ValheimVRM.VRM.ImportVisualAsync(File.ReadAllBytes(Path.Combine(ValheimVRM.Settings.ValheimVRMDir, name + ".vrm")),
            name + ".vrm", 1, root => imported = root);
        Check(imported != null, "Cancellation fixture import failed");
        var model = new ValheimVRM.VRM(imported, name);
        imported.SetActive(false);
        int cases = 0;
        foreach (bool destroyPlayer in new[] { true, false })
        {
            for (int cancelAt = 0; cancelAt < 8; cancelAt++)
            {
                var holder = new GameObject("Inactive cancellation fixture"); holder.SetActive(false);
                var player = Object.Instantiate(prefab, holder.transform).GetComponent<Player>();
                fixturePlayers.Add(player.GetInstanceID());
                var animator = player.GetComponentInChildren<Animator>(true);
                AccessTools.Field(typeof(Character), "m_animator").SetValue(player, animator);
                AccessTools.Field(typeof(Character), "m_visual").SetValue(player, animator.gameObject);
                player.gameObject.AddComponent<VrmController>();
                var routine = model.SetToPlayer(player);
                GameObject clone = null;
                int steps = 0; bool cancelled = false;
                try
                {
                    while (routine.MoveNext())
                    {
                        VrmManager.PlayerToVrmInstance.TryGetValue(player, out clone);
                        if (steps++ == cancelAt && clone != null)
                        {
                            if (destroyPlayer) Object.Destroy(player.gameObject); else Object.Destroy(clone);
                            cancelled = true;
                        }
                        yield return null;
                    }
                }
                finally { (routine as IDisposable)?.Dispose(); }
                if (cancelled) cases++;
                var entries = (IDictionary)AccessTools.Field(typeof(AvatarResidency), "entries").GetValue(null);
                var entry = entries[model];
                Check((int)AccessTools.Field(entry.GetType(), "PendingAttachments").GetValue(entry) == 0,
                    "Cancelled attachment permanently pinned imported resources");
                VrmManager.PlayerToVrmInstance.Remove(player);
                if (clone != null) Object.Destroy(clone);
                Object.Destroy(holder);
                yield return null;
            }
        }
        Check(cases >= 10, "Not enough asynchronous cancellation boundaries exercised");
        model.Dispose(); yield return null;
        Log("Player/visual destroyed across " + cases + " attachment yield boundaries: no exception, pending resource lease returned to zero");
    }
}
