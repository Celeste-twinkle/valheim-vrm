using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using ValheimVRM;
using Object = UnityEngine.Object;

[BepInPlugin("valheimvrm.tests.attachments", "Player attachment engine regression", "1.0.0")]
[BepInDependency(MainPlugin.PluginGuid)]
public sealed class AvatarAttachmentEngineTests : BaseUnityPlugin
{
    const string FixturePrefix = "AttachmentFixture";
    const long Session = 71001;
    string output;
    readonly List<string> report = new List<string>();
    readonly List<GameObject> fixtures = new List<GameObject>();
    readonly Dictionary<ZDOID, ZDO> objects = new Dictionary<ZDOID, ZDO>();
    static readonly MethodInfo OwnerSync = AccessTools.Method(typeof(ZSyncTransform), "OwnerSync");
    static readonly MethodInfo SyncPosition = AccessTools.Method(typeof(ZSyncTransform), "SyncPosition");
    ZNetScene scene;
    uint nextId = 1;

    void Awake()
    {
        output = Environment.GetEnvironmentVariable("VRM_ATTACHMENT_TEST_OUTPUT");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        Directory.CreateDirectory(output);
        var harmony = new Harmony("valheimvrm.tests.attachments.isolation");
        harmony.Patch(AccessTools.PropertyGetter(typeof(FileHelpers), "CloudStorageSupported"),
            prefix: new HarmonyMethod(typeof(AvatarAttachmentEngineTests), nameof(NoCloud)));
        // Only fixture lifecycle is suppressed. The player-relative-position,
        // owner sync, ZDO serialization and remote position code remain native.
        foreach (var type in new[] { typeof(Player), typeof(Character), typeof(ZNetView),
            typeof(ZSyncTransform), typeof(Sadle), typeof(ZNet), typeof(ZNetScene), typeof(Game) })
            foreach (string name in new[] { "Awake", "OnDestroy" })
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null) harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(AvatarAttachmentEngineTests), nameof(FixtureLifecycle)));
            }
        harmony.Patch(AccessTools.Method(typeof(Sadle), "FixedUpdate"),
            prefix: new HarmonyMethod(typeof(AvatarAttachmentEngineTests), nameof(FixtureLifecycle)));
        var playerAwake = typeof(MainPlugin).Assembly.GetType("ValheimVRM.Patch_Player_Awake");
        harmony.Patch(AccessTools.Method(playerAwake, "Postfix"),
            prefix: new HarmonyMethod(typeof(AvatarAttachmentEngineTests), nameof(FixturePlayerInitialization)));
        harmony.CreateReversePatcher(OwnerSync,
            new HarmonyMethod(typeof(AvatarAttachmentEngineTests), nameof(NativeOwnerSync))).Patch();
        harmony.CreateReversePatcher(SyncPosition,
            new HarmonyMethod(typeof(AvatarAttachmentEngineTests), nameof(NativeSyncPosition))).Patch();
    }
    static bool NoCloud(ref bool __result) { __result = false; return false; }
    static bool FixtureLifecycle(Component __instance) => !__instance.name.StartsWith(FixturePrefix, StringComparison.Ordinal);
    static bool FixturePlayerInitialization(Player __0) => !__0.name.StartsWith(FixturePrefix, StringComparison.Ordinal);
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void NativeOwnerSync(ZSyncTransform instance) => throw new InvalidOperationException("Reverse patch missing");
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void NativeSyncPosition(ZSyncTransform instance, ZDO zdo, float dt, out bool usedLocalRotation)
    { usedLocalRotation = false; throw new InvalidOperationException("Reverse patch missing"); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    void Log(string value) { report.Add(value); File.WriteAllLines(Path.Combine(output, "results.txt"), report); }

    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(output)) yield break;
        float deadline = Time.realtimeSinceStartup + 90;
        FejdStartup menu;
        while ((menu = Object.FindFirstObjectByType<FejdStartup>()) == null)
        {
            if (Time.realtimeSinceStartup > deadline) { Fail(new Exception("Menu timeout")); yield break; }
            yield return null;
        }
        var stack = new Stack<IEnumerator>(); stack.Push(Run(menu));
        while (stack.Count != 0)
        {
            bool more; object next = null;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception ex)
            {
                while (stack.Count != 0) (stack.Pop() as IDisposable)?.Dispose();
                Fail(ex); yield break;
            }
            if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        Log("AVATAR_ATTACHMENT_ENGINE_TESTS_PASSED"); Application.Quit(0);
    }
    void Fail(Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); Logger.LogError(ex); Application.Quit(1); }

    GameObject Fixture(string name, bool active = true)
    {
        var root = new GameObject(FixturePrefix + name); root.SetActive(active); fixtures.Add(root); return root;
    }
    ZDO View(GameObject root, long user, bool owner)
    {
        var view = root.AddComponent<ZNetView>(); view.enabled = false;
        var zdo = new ZDO { m_uid = new ZDOID(user, nextId++) };
        AccessTools.Field(typeof(ZDO), "m_prefab").SetValue(zdo, 1);
        zdo.SetOwnerInternal(owner ? Session : Session + 1);
        AccessTools.Field(typeof(ZNetView), "m_zdo").SetValue(view, zdo);
        objects.Add(zdo.m_uid, zdo); scene.AddInstance(zdo, view);
        return zdo;
    }
    Player PlayerFixture(GameObject template, long user, bool owner, out ZDO zdo, out ZSyncTransform sync)
    {
        var root = Fixture(owner ? "Owner" : "Observer");
        var body = root.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
        var collider = root.AddComponent<CapsuleCollider>();
        zdo = View(root, user, owner);
        var player = root.AddComponent<Player>(); player.enabled = false;
        AccessTools.Field(typeof(Character), "m_body").SetValue(player, body);
        AccessTools.Field(typeof(Character), "m_collider").SetValue(player, collider);
        AccessTools.Field(typeof(Character), "m_nview").SetValue(player, root.GetComponent<ZNetView>());
        var inactive = Fixture("VisualImport", false);
        var visual = Object.Instantiate(template, inactive.transform);
        foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        visual.transform.SetParent(root.transform, false);
        var animator = visual.GetComponent<Animator>();
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.Update(0);
        AccessTools.Field(typeof(Character), "m_animator").SetValue(player, animator);
        AccessTools.Field(typeof(Character), "m_visual").SetValue(player, visual);
        sync = root.AddComponent<ZSyncTransform>(); sync.enabled = false; sync.m_characterParentSync = true;
        AccessTools.Field(typeof(ZSyncTransform), "m_nview").SetValue(sync, root.GetComponent<ZNetView>());
        AccessTools.Field(typeof(ZSyncTransform), "m_character").SetValue(sync, player);
        // The position code is tested without physics interpolation, as for a
        // normal remotely controlled player. Character retains its real body.
        AccessTools.Field(typeof(ZSyncTransform), "m_wasOwner").SetValue(sync, owner);
        return player;
    }
    Sadle MountFixture(long user, Vector3 position, string animation, out ZDO zdo)
    {
        var root = Fixture("Mount"); root.transform.position = position;
        var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
        zdo = View(root, user, true);
        var character = root.AddComponent<Character>(); character.enabled = false;
        var saddleRoot = new GameObject("Saddle"); saddleRoot.transform.SetParent(root.transform, false);
        var attach = new GameObject("rider_attach").transform; attach.SetParent(saddleRoot.transform, false);
        attach.localPosition = new Vector3(.2f, 3.1f, -.4f);
        // Keep the fixture prefix on the component's object for lifecycle guards.
        saddleRoot.name = FixturePrefix + "Saddle";
        var saddle = saddleRoot.AddComponent<Sadle>(); saddle.m_attachPoint = attach; saddle.m_attachAnimation = animation;
        return saddle;
    }
    static Animator Animator(Player player) => (Animator)AccessTools.Field(typeof(Character), "m_animator").GetValue(player);
    static void Attach(Player player, Sadle saddle, bool riding)
    {
        AccessTools.Field(typeof(Player), "m_attached").SetValue(player, riding);
        AccessTools.Field(typeof(Player), "m_attachPoint").SetValue(player, riding ? saddle.m_attachPoint : null);
        AccessTools.Field(typeof(Character), "m_lastGroundBody").SetValue(player, saddle.GetComponentInParent<Rigidbody>());
        AccessTools.Field(typeof(Character), "m_lastGroundTouch").SetValue(player, 0f);
        Animator(player).SetBool(saddle.m_attachAnimation, riding);
        player.transform.position = riding ? saddle.m_attachPoint.position : saddle.transform.parent.TransformPoint(new Vector3(1, 1.3f, 0));
        player.transform.rotation = saddle.transform.parent.rotation;
    }
    static byte[] Data(ZDO zdo) { var packet = new ZPackage(); zdo.Serialize(packet); return packet.GetArray(); }
    static void Replicate(ZDO owner, ZDO replica)
    {
        replica.Deserialize(new ZPackage(Data(owner)));
        AccessTools.Field(typeof(ZDO), "m_position").SetValue(replica, owner.GetPosition());
        AccessTools.Field(typeof(ZDO), "m_rotation").SetValue(replica, owner.GetRotation().eulerAngles);
        replica.DataRevision = owner.DataRevision;
    }
    static void Observe(ZSyncTransform sync, ZDO zdo) => SyncPosition.Invoke(sync, new object[] { zdo, .02f, false });

    IEnumerator Run(FejdStartup menu)
    {
        var managerField = AccessTools.Field(typeof(ZDOMan), "s_instance");
        var sceneField = AccessTools.Field(typeof(ZNetScene), "s_instance");
        var netField = AccessTools.Field(typeof(ZNet), "m_instance");
        var serverField = AccessTools.Field(typeof(ZNet), "m_isServer");
        var gameField = AccessTools.Field(typeof(Game), "<instance>k__BackingField");
        object oldManager = managerField.GetValue(null), oldScene = sceneField.GetValue(null),
            oldNet = netField.GetValue(null), oldServer = serverField.GetValue(null), oldGame = gameField.GetValue(null);
        try
        {
            var manager = (ZDOMan)FormatterServices.GetUninitializedObject(typeof(ZDOMan));
            AccessTools.Field(typeof(ZDOMan), "m_objectsByID").SetValue(manager, objects);
            AccessTools.Field(typeof(ZDOMan), "m_sessionID").SetValue(manager, Session);
            AccessTools.Field(typeof(ZDOMan), "m_objectsBySector").SetValue(manager, new List<ZDO>[512 * 512]);
            var peers = AccessTools.Field(typeof(ZDOMan), "m_peers");
            peers.SetValue(manager, Activator.CreateInstance(peers.FieldType));
            managerField.SetValue(null, manager);
            var net = Fixture("Network", false).AddComponent<ZNet>();
            net.m_passwordDialog = Fixture("PasswordDialog", false).AddComponent<RectTransform>();
            netField.SetValue(null, net); serverField.SetValue(null, true);
            gameField.SetValue(null, Fixture("Game", false).AddComponent<Game>());
            scene = Fixture("Scene", false).AddComponent<ZNetScene>(); sceneField.SetValue(null, scene);
            var prefab = (GameObject)AccessTools.Field(typeof(FejdStartup), "m_playerPrefab").GetValue(menu);
            var template = prefab.GetComponentInChildren<Animator>(true).gameObject;
            var owner = PlayerFixture(template, 72001, true, out var ownerZdo, out var ownerSync);
            var observer = PlayerFixture(template, 72001, false, out var remoteZdo, out var remoteSync);
            var mount = MountFixture(73001, new Vector3(12, 20, -8), "attach_moose", out var mountZdo);
            // Discover the actual riding parameter, avoiding assumptions about
            // the controller shipped by the installed game version.
            string parameter = Animator(owner).parameters.Where(p => p.type == AnimatorControllerParameterType.Bool)
                .Select(p => p.name).First(p => p.IndexOf("moose", StringComparison.OrdinalIgnoreCase) >= 0);
            mount.m_attachAnimation = parameter;
            mountZdo.Set(ZDOVars.s_user, ownerZdo.m_uid.UserID);

            Attach(owner, mount, false); NativeOwnerSync(ownerSync);
            Check(ownerZdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.SyncTransform) == mountZdo.m_uid, "Standing parent not replicated");
            Attach(owner, mount, true); NativeOwnerSync(ownerSync); Replicate(ownerZdo, remoteZdo);
            Check(ownerZdo.GetString(ZDOVars.s_attachJointHash) == "", "Native missing-joint regression no longer reproduces");
            NativeSyncPosition(remoteSync, remoteZdo, .02f, out _);
            Check(Vector3.Distance(observer.transform.position, mount.transform.parent.position) < .0001f, "Native replica did not fall to mount origin");
            Log("Native bug reproduced: stand on mount -> ride same mount; transmitted joint is empty, remote player is " +
                Vector3.Distance(observer.transform.position, mount.m_attachPoint.position).ToString("F3") + " m below/away from saddle");

            Animator(observer).SetBool(parameter, true);
            byte[] received = Data(remoteZdo); uint revision = remoteZdo.DataRevision;
            Observe(remoteSync, remoteZdo);
            Check(Vector3.Distance(observer.transform.position, mount.m_attachPoint.position) < .0001f, "Old-sender observer recovery failed");
            Check(Data(remoteZdo).SequenceEqual(received) && remoteZdo.DataRevision == revision, "Observer modified received ZDO");
            OwnerSync.Invoke(ownerSync, null);
            Check(ownerZdo.GetString(ZDOVars.s_attachJointHash) == mount.m_attachPoint.name, "Same-parent mounting did not send joint");
            Replicate(ownerZdo, remoteZdo); NativeSyncPosition(remoteSync, remoteZdo, .02f, out _);
            Check(Vector3.Distance(observer.transform.position, mount.m_attachPoint.position) < .0001f, "Fixed sender failed with an unpatched receiver");
            Log("Fixed sender works with native receiver; fixed observer recovers old sender without changing received ZDO");

            uint stableRevision = ownerZdo.DataRevision;
            for (int i = 0; i < 100; i++) OwnerSync.Invoke(ownerSync, null);
            Check(ownerZdo.DataRevision == stableRevision, "Unchanged attachment rewrites network data every frame");
            for (int i = 0; i < 60; i++)
            {
                mount.transform.parent.position = new Vector3(12 + i * .1f, i % 2 == 0 ? 1200 : 20, -8);
                mount.transform.parent.rotation = Quaternion.Euler(0, i * 17, 0);
                Attach(owner, mount, false); OwnerSync.Invoke(ownerSync, null);
                Check(ownerZdo.GetString(ZDOVars.s_attachJointHash) == "", "Dismount kept saddle joint on same parent");
                Attach(owner, mount, true); OwnerSync.Invoke(ownerSync, null); Replicate(ownerZdo, remoteZdo);
                Observe(remoteSync, remoteZdo);
                Check(Vector3.Distance(observer.transform.position, mount.m_attachPoint.position) < .0001f, "Repeated moving mount drift");
            }
            Log("60 same-parent mount/dismount cycles at changing headings and 20/1200 m altitude; no position drift; 100 unchanged sends do not increase DataRevision");

            yield return GuardTests(owner, ownerSync, ownerZdo, observer, remoteSync, remoteZdo, mount, mountZdo);
            var paths = Environment.GetEnvironmentVariable("VRM_ATTACHMENT_TEST_MODELS")?.Split('|');
            if (paths != null) foreach (string path in paths)
                yield return AvatarTests(path, observer, remoteSync, remoteZdo, mount, mountZdo);
        }
        finally
        {
            managerField.SetValue(null, oldManager); sceneField.SetValue(null, oldScene);
            netField.SetValue(null, oldNet); serverField.SetValue(null, oldServer);
            gameField.SetValue(null, oldGame);
            foreach (var fixture in fixtures) if (fixture != null) Object.Destroy(fixture);
        }
    }

    IEnumerator GuardTests(Player owner, ZSyncTransform ownerSync, ZDO ownerZdo, Player observer,
        ZSyncTransform remoteSync, ZDO remoteZdo, Sadle mount, ZDO mountZdo)
    {
        remoteZdo.Set(ZDOVars.s_attachJointHash, ""); remoteZdo.Set(ZDOVars.s_relPosHash, Vector3.zero);
        Animator(observer).SetBool(mount.m_attachAnimation, false); Observe(remoteSync, remoteZdo);
        Check(Vector3.Distance(observer.transform.position, mount.transform.parent.position) < .0001f, "Standing was mistaken for riding");
        Animator(observer).SetBool(mount.m_attachAnimation, true);
        mountZdo.Set(ZDOVars.s_user, ownerZdo.m_uid.UserID + 99); Observe(remoteSync, remoteZdo);
        Check(Vector3.Distance(observer.transform.position, mount.transform.parent.position) < .0001f, "Another rider's saddle was used");
        mountZdo.Set(ZDOVars.s_user, ownerZdo.m_uid.UserID);
        mount.gameObject.SetActive(false); Observe(remoteSync, remoteZdo);
        Check(Vector3.Distance(observer.transform.position, mount.transform.parent.position) < .0001f, "Inactive saddle was used");
        mount.gameObject.SetActive(true);
        var instances = (Dictionary<ZDO, ZNetView>)AccessTools.Field(typeof(ZNetScene), "m_instances").GetValue(scene);
        var mountView = instances[mountZdo]; instances.Remove(mountZdo);
        observer.transform.position = remoteZdo.GetPosition();
        Vector3 unloaded = observer.transform.position; Observe(remoteSync, remoteZdo);
        Check(Vector3.Distance(observer.transform.position, unloaded) < .0001f, "Unloaded mount changed a stationary replica");
        instances.Add(mountZdo, mountView); Observe(remoteSync, remoteZdo);
        Check(Vector3.Distance(observer.transform.position, mount.m_attachPoint.position) < .0001f, "Late-loaded mount did not recover");
        byte[] data = Data(remoteZdo); uint revision = remoteZdo.DataRevision;
        OwnerSync.Invoke(remoteSync, null);
        Check(Data(remoteZdo).SequenceEqual(data) && remoteZdo.DataRevision == revision, "Non-owner wrote attachment state");
        // Losing all support must clear the parent and joint, too.
        AccessTools.Field(typeof(Player), "m_attached").SetValue(owner, false);
        AccessTools.Field(typeof(Player), "m_attachPoint").SetValue(owner, null);
        AccessTools.Field(typeof(Character), "m_lastGroundBody").SetValue(owner, null);
        OwnerSync.Invoke(ownerSync, null);
        Check(ownerZdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.SyncTransform).IsNone() &&
            ownerZdo.GetString(ZDOVars.s_attachJointHash) == "", "Detach did not clear attachment");
        Log("Guards: standing, other rider, inactive/unloaded saddle, late mount load, non-owner writes and complete detach passed");
        yield break;
    }

    IEnumerator AvatarTests(string path, Player observer, ZSyncTransform sync, ZDO zdo, Sadle mount, ZDO mountZdo)
    {
        GameObject imported = null;
        yield return ValheimVRM.VRM.ImportVisualAsync(File.ReadAllBytes(path), path, 1, root => imported = root);
        Check(imported != null, "Avatar import failed: " + path); imported.SetActive(false);
        var source = Animator(observer);
        source.Rebind(); source.Play("Base Layer.RideMoose", 0, .5f); source.Update(0);
        source.SetBool(mount.m_attachAnimation, true);
        zdo.SetConnection(ZDOExtraData.ConnectionType.SyncTransform, mountZdo.m_uid);
        zdo.Set(ZDOVars.s_attachJointHash, ""); zdo.Set(ZDOVars.s_relPosHash, Vector3.zero);
        foreach (float height in new[] { 1.4f, 2f, 2.2f })
        {
            var model = Object.Instantiate(imported, observer.transform); AvatarScale.ApplyHeight(model, height);
            AccessTools.Method(typeof(ValheimVRM.VRM), "PrepareVrm10Clone").Invoke(null, new object[] { imported, model });
            model.SetActive(true); foreach (var behaviour in model.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            var animation = model.AddComponent<VRMAnimationSync>();
            animation.Setup(source, new ValheimVRM.Settings.VrmSettingsContainer { Name = "AttachmentProbe", ModelOffsetY = .07f, SittingHeightOffset = .08f });
            animation.enabled = false;
            var tick = AccessTools.Method(typeof(VRMAnimationSync), "LateUpdate");
            var target = model.GetComponent<Animator>();
            Vector3? baseline = null;
            float maximumSeatError = 0;
            using (var nativeSurface = new GroundingProbe.Surface(source))
            using (var avatarSurface = new GroundingProbe.Surface(target))
            for (int i = 0; i < 90; i++)
            {
                mount.transform.parent.position = new Vector3(i * .3f, 30 + i * .02f, -i * .2f);
                mount.transform.parent.rotation = Quaternion.Euler(0, i * 11, 0);
                source.Play("Base Layer.RideMoose", 0, .5f); source.Update(0);
                Observe(sync, zdo); tick.Invoke(animation, null);
                Check(Vector3.Distance(observer.transform.position, mount.m_attachPoint.position) < .0001f, "Avatar root escaped saddle");
                Vector3 offset = Quaternion.Inverse(observer.transform.rotation) *
                    (target.GetBoneTransform(HumanBodyBones.Hips).position - source.GetBoneTransform(HumanBodyBones.Hips).position);
                if (baseline.HasValue) Check(Vector3.Distance(offset, baseline.Value) < .002f, "Mounted avatar calibration drifted");
                else baseline = offset;
                // Independently bake mesh support, rather than checking only a
                // stable hip delta which could consistently be at the wrong height.
                nativeSurface.Sample(out _, out float nativeSeat, out _);
                avatarSurface.Sample(out _, out float avatarSeat, out _);
                float seatError = Mathf.Abs(avatarSeat - nativeSeat - .15f);
                maximumSeatError = Mathf.Max(maximumSeatError, seatError);
                Check(seatError < .025f, "Mounted avatar seat/model offsets were lost: " + seatError);
            }
            Log(Path.GetFileName(path) + " height=" + height + " maximum independently baked seat error=" + maximumSeatError.ToString("F6") + " m");
            Object.Destroy(model); yield return null;
        }
        Object.Destroy(imported);
        Log("Mounted avatar " + Path.GetFileName(path) + ": 1.4/2/2.2 m, 270 moving/turning samples, sitting/model offsets retained without drift");
    }
}
