using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;

[BepInPlugin("valheimvrm.tests.rendering", "Avatar rendering regression tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed class AvatarRenderingTests : BaseUnityPlugin
{
    string output;
    void Awake()
    {
        output = Environment.GetEnvironmentVariable("VRM_RENDER_TEST_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        Directory.CreateDirectory(output);
        var getter = AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupported");
        if (getter != null) new Harmony("valheimvrm.tests.rendering.isolation").Patch(getter,
            prefix: new HarmonyMethod(typeof(AvatarRenderingTests), nameof(NoCloud)));
    }
    static bool NoCloud(ref bool __result) { __result = false; return false; }

    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        float deadline = Time.realtimeSinceStartup + 90;
        while (!VRMShaders.Shaders.ContainsKey("VRM10/MToon10"))
        {
            if (Time.realtimeSinceStartup > deadline) { File.WriteAllText(Path.Combine(output, "error.txt"), "Shaders did not load"); Application.Quit(1); yield break; }
            yield return null;
        }
        yield return null;
        var material = new Material(VRMShaders.Shaders["VRM10/MToon10"]);
        var stack = new Stack<IEnumerator>(); stack.Push(ShadowProbe.Run(material, output));
        while (stack.Count > 0)
        {
            bool more; object value = null;
            try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Logger.LogError(ex); Application.Quit(1); yield break; }
            if (!more) { stack.Pop(); continue; }
            if (value is IEnumerator nested) stack.Push(nested); else yield return value;
        }
        Logger.LogInfo("AVATAR_RENDER_TESTS_PASSED"); Application.Quit(0);
    }
}
