using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

public sealed partial class AvatarLifecycleEngineTests
{
    IEnumerator HeightTests()
    {
        foreach (float height in new[] { 1.2f, 1.6f, 1.8f })
        {
            var root = new GameObject("Known-height fixture");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(.4f, height, .3f);
            body.transform.localPosition = Vector3.up * (height / 2 + 10);
            var hidden = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hidden.transform.SetParent(root.transform, false);
            hidden.transform.localScale = Vector3.one * 100;
            hidden.GetComponent<Renderer>().enabled = false;
            root.transform.localScale = Vector3.one * 2;
            float scale = AvatarScale.Apply(root, 1);
            Check(Mathf.Abs(scale - Mathf.Max(1, 1.6f / height)) < .0001f, "Known-height scaling failed");
            Check(Mathf.Abs(body.GetComponent<Renderer>().bounds.size.y - Mathf.Max(1.6f, height)) < .0001f,
                "Actual rendered fixture height differs from expected height");
            for (int i = 0; i < 5; i++) Check(Mathf.Abs(AvatarScale.Apply(root, 1) - scale) < .0001f, "Repeated scaling accumulated");
            Check(Mathf.Abs(AvatarScale.Apply(root, 2) - 2) < .0001f, "Explicit enlargement was lost");
            var clone = Object.Instantiate(root);
            clone.transform.GetChild(0).localScale *= .2f;
            Check(Mathf.Abs(AvatarScale.Apply(clone, .5f) - 1.6f / height) < .0001f,
                "Clone remeasured an animated/compressed pose or bypassed minimum height");
            Check(!float.IsNaN(AvatarScale.Apply(clone, float.NaN)), "Invalid custom scale poisoned the transform");
            Object.Destroy(clone); Object.Destroy(root);
            yield return null;
        }
        Log("Known 1.2/1.6/1.8 m meshes: minimum height, taller unchanged, explicit enlargement, disabled geometry, root scale, repeated application and clone calibration passed");

        foreach (var path in Directory.GetFiles(ValheimVRM.Settings.ValheimVRMDir, "*.vrm").Where(p => !Path.GetFileName(p).StartsWith("___")).OrderBy(p => p))
        {
            GameObject root = null;
            yield return ValheimVRM.VRM.ImportVisualAsync(File.ReadAllBytes(path), path, 1, r => root = r);
            Check(root != null, "Height fixture import failed: " + path);
            var sizing = root.GetComponent<AvatarScale>();
            Check(sizing != null && sizing.UnscaledHeight > .01f, "Standing height was not captured: " + path);
            float final = sizing.UnscaledHeight * root.transform.localScale.y;
            Check(final >= 1.5999f, "Imported model below 1.6 m: " + path);
            if (sizing.UnscaledHeight >= 1.6f) Check(root.transform.localScale == Vector3.one, "Tall model was changed");
            Log(Path.GetFileName(path) + ": standing mesh " + sizing.UnscaledHeight.ToString("F3") + " m, scale " + root.transform.localScale.y.ToString("F3") + ", final " + final.ToString("F3") + " m");
            Object.Destroy(root);
            yield return null; yield return null;
        }
    }
}
