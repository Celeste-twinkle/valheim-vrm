using System;
using System.Reflection;
using BepInEx;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

// Run only in a disposable game copy. No characters or worlds are loaded.
[BepInPlugin("valheimvrm.tests.cameraheight", "Camera height regression tests", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed class CameraHeightTests : BaseUnityPlugin
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Type SyncType = typeof(VRMEyePositionSync);

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void Near(Vector3 actual, Vector3 expected, string message)
    {
        Require(Vector3.Distance(actual, expected) < .0001f, message + ": " + actual + " != " + expected);
    }

    static void Setup(VRMEyePositionSync sync, Transform head, float offset = 0f)
    {
        var method = SyncType.GetMethod("Setup");
        method.Invoke(sync, method.GetParameters().Length == 1
            ? new object[] { head } : new object[] { head, offset });
    }

    static void Tick(VRMEyePositionSync sync)
    {
        SyncType.GetMethod("LateUpdate", Instance)?.Invoke(sync, null);
    }

    void Start()
    {
        try
        {
            Run();
            Logger.LogInfo("CAMERA_TESTS_PASS");
            Application.Quit(0);
        }
        catch (Exception ex)
        {
            Logger.LogError("CAMERA_TESTS_FAIL: " + ex);
            Application.Quit(1);
        }
    }

    void Run()
    {
        bool legacy = Environment.GetEnvironmentVariable("VRM_CAMERA_EXPECT_LEGACY") == "1";
        // Keep Player/GameCamera inactive to avoid unrelated world/network Awake logic.
        var root = new GameObject("CameraRegressionPlayer");
        root.SetActive(false);
        root.transform.position = new Vector3(1000f, 100f, 1000f);
        var player = root.AddComponent<Player>();
        var eye = new GameObject("Eye").transform;
        eye.SetParent(root.transform, false);
        eye.localPosition = new Vector3(.03f, 1.8f, .08f);
        player.m_eye = eye;
        var original = eye.localPosition;
        var sync = root.AddComponent<VRMEyePositionSync>();
        var avatar = new GameObject("AvatarA").transform;
        avatar.SetParent(root.transform, false);
        var head = new GameObject("Head").transform;
        head.SetParent(avatar, false);
        head.localPosition = Vector3.up * 1.55f;
        Setup(sync, head);
        Tick(sync);
        var calibrated = eye.localPosition;
        Near(calibrated, new Vector3(original.x, 1.55f, original.z), "Calibrate to the chosen avatar height");

        var cameraObject = new GameObject("CameraRegressionCamera");
        cameraObject.SetActive(false);
        var camera = cameraObject.AddComponent<GameCamera>();
        typeof(GameCamera).GetField("m_blockCameraMask", Instance).SetValue(camera, (LayerMask)(1 << 0));
        var cast = typeof(GameCamera).GetMethod("RayTestPoint", Instance, null,
            new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(Vector3).MakeByRefType() }, null);
        Require(cast != null, "Find the installed game's close-range camera collision helper");
        var wall = new GameObject("CameraRegressionWall");
        wall.transform.position = root.transform.position + new Vector3(0, 1.5f, -.7f);
        var box = wall.AddComponent<BoxCollider>();
        box.size = new Vector3(4f, 4f, .2f);
        Physics.SyncTransforms();
        float maxDrift = 0f;
        Vector3 firstHit = Vector3.zero;
        for (int frame = 0; frame < 240; frame++)
        {
            head.localPosition = Vector3.up * (1.55f + .07f * Mathf.Sin(frame * .3f));
            Tick(sync);
            object[] arguments = { eye.position, Vector3.back, 4f, Vector3.zero };
            Require((bool)cast.Invoke(camera, arguments), "The close wall must still block the camera");
            var hit = (Vector3)arguments[3];
            if (frame == 0) firstHit = hit;
            maxDrift = Mathf.Max(maxDrift, Vector3.Distance(firstHit, hit));
        }
        Logger.LogInfo("CLOSE_WALL_MAX_DRIFT_METERS=" + maxDrift.ToString("F6"));
        if (legacy)
        {
            Require(maxDrift > .06f, "The previous implementation must reproduce animated camera drift");
            Logger.LogInfo("BASELINE_JITTER_REPRODUCED");
            return;
        }
        Require(maxDrift < .0001f, "Head animation must not move the camera collision result");

        wall.SetActive(false);
        Physics.SyncTransforms();
        object[] clearCast = { eye.position, Vector3.back, 4f, Vector3.zero };
        Require(!(bool)cast.Invoke(camera, clearCast), "Removing the wall must clear the camera cast");
        root.transform.position += new Vector3(3, 5, -2);
        root.transform.rotation = Quaternion.Euler(0, 73, 0);
        Near(eye.localPosition, calibrated, "Walking, climbing and turning preserve the calibrated local pivot");
        Object.DestroyImmediate(head.gameObject);
        Tick(sync);
        Near(eye.localPosition, calibrated, "Removing the old model must not leave a live bone reference");

        var nextHead = new GameObject("AvatarBHead").transform;
        nextHead.SetParent(root.transform, false);
        nextHead.localPosition = Vector3.up * 2.1f;
        for (int i = 0; i < 25; i++) Setup(sync, nextHead, .12f);
        Near(eye.localPosition, new Vector3(original.x, 2.22f, original.z), "Switches apply the next height and model offset without compounding");
        SyncType.GetMethod("ResetEyePosition").Invoke(sync, null);
        Near(eye.localPosition, original, "FixCameraHeight=false restores the original pivot");
        Setup(sync, nextHead);
        Setup(sync, null);
        Near(eye.localPosition, original, "A missing target clears the previous calibration");
        foreach (string callback in new[] { "OnDisable", "OnDestroy" })
        {
            Setup(sync, nextHead);
            SyncType.GetMethod(callback, Instance).Invoke(sync, null);
            Near(eye.localPosition, original, callback + " restores the original pivot");
        }
        Setup(sync, nextHead);
        Object.DestroyImmediate(eye.gameObject);
        SyncType.GetMethod("ResetEyePosition").Invoke(sync, null);
        Logger.LogInfo("PASS: calibration, 240 collision samples, wall removal, movement, missing/destroyed targets, 25 switches, offsets and cleanup.");
        // The process exits immediately; inactive game components deliberately do
        // not run their unrelated destruction callbacks without a world.
    }
}
