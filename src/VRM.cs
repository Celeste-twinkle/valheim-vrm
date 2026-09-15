using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Diagnostics;
using HarmonyLib;
using UniGLTF;
using UnityEngine;
using VRM;
using UniVRM10;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


namespace ValheimVRM
{
	public class VRM : IDisposable
	{
		public enum SourceType
		{
			Local,  // my VRM from my computer
			Shared // VRM, downloaded from other player
		}

		public GameObject VisualModel { get; private set; }
		public byte[] Src;
		public byte[] SrcHash;
		public byte[] SettingsHash;
		public string Name { get; private set; }
		public SourceType Source = SourceType.Local;

		public VRM(GameObject visualModel, string name)
		{
			VisualModel = visualModel;
			if (visualModel.GetComponent<AvatarRenderingTarget>() == null) visualModel.AddComponent<AvatarRenderingTarget>();
			if (visualModel.GetComponent<AvatarBloomTarget>() == null) visualModel.AddComponent<AvatarBloomTarget>();
			Name = name;
		}

		public void Dispose()
		{
			// Unity objects must be released on the main thread, never a finalizer.
			if (VisualModel != null) Object.Destroy(VisualModel);
			VisualModel = null;
			Src = null;
		}

		public void RecalculateSrcBytesHash()
		{
			var bytes = Src;
			if (!Settings.globalSettings.EnableLegacyVrmSharing) Src = null;
			if (bytes == null) return;
			Task.Run(() =>
			{
				using (var md5 = System.Security.Cryptography.MD5.Create())
				{
					var hash = md5.ComputeHash(bytes);
					lock (this)
					{
						SrcHash = hash;
					}
				}
			});
		}



		public void RecalculateSettingsHash()
		{
			Task.Run(() =>
			{
				using (var md5 = System.Security.Cryptography.MD5.Create())
				{
					byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(Settings.GetSettings(Name).ToStringDiffOnly());

					lock (this)
					{
						SettingsHash = md5.ComputeHash(inputBytes); ;
					}
				}
			});
		}
		public class Timer : IDisposable
		{
			private Stopwatch stopwatch;
			private string name;

			public Timer(string name)
			{
				this.name = name;
				this.stopwatch = Stopwatch.StartNew();
			}

			public void Dispose()
			{
				stopwatch.Stop();
				Debug.Log($"{name} took {stopwatch.ElapsedMilliseconds} ms");
			}
		}

		public static GameObject ImportVisual(byte[] buf, string path, float scale)
		{
			try { return LoadVisual(buf, path, scale, new ImmediateCaller(), false).GetAwaiter().GetResult(); }
			catch (Exception ex) { Debug.LogError("[ValheimVRM] Import failed: " + ex); return null; }
		}

		public static IEnumerator ImportVisualAsync(byte[] buf, string path, float scale, Action<GameObject> onCompleted)
		{
			var task = ImportVisualAsync(buf, path, scale);
			while (!task.IsCompleted) yield return null;
			if (task.IsFaulted)
			{
				Debug.LogError("[ValheimVRM] Import failed: " + task.Exception.Flatten());
				onCompleted(null);
			}
			else onCompleted(task.Result);
		}

		public static Task<GameObject> ImportVisualAsync(byte[] buf, string path, float scale)
		{
			return LoadVisual(buf, path, scale, new RuntimeOnlyAwaitCaller(0.001f), true);
		}

		private static async Task<GameObject> LoadVisual(byte[] buf, string path, float scale, IAwaitCaller awaitCaller, bool backgroundParse)
		{
			Debug.Log("[ValheimVRM] Loading VRM: " + buf.Length + " bytes");
			// GltfData owns native import buffers. Dispose it even on malformed VRM
			// input; dropping the managed reference does not free these buffers.
			using (var data = backgroundParse
				? await Task.Run(() => new GlbBinaryParser(buf, path).Parse())
				: new GlbBinaryParser(buf, path).Parse())
			{
				ImporterContext context;
				try
				{
					var legacy = new VRMData(data);
					context = new VRMImporterContext(legacy, null, new TextureDeserializer(),
						new AvatarBrightness(new BuiltInVrmMaterialDescriptorGenerator(legacy.VrmExtension)));
				}
				catch (NotVrm0Exception) { context = new Vrm10Importer(Vrm10Data.Parse(data), null, null, new AvatarBrightness()); }
				using (context)
				{
					try
					{
						var loaded = await context.LoadAsync(awaitCaller);
						loaded.ShowMeshes();
						float effectiveScale = AvatarScale.Apply(loaded.Root, scale);
						var sizing = loaded.Root.GetComponent<AvatarScale>();
						Debug.Log($"[ValheimVRM] Avatar standing height {sizing.UnscaledHeight:F3} m, scale {effectiveScale:F3}, final {sizing.UnscaledHeight * effectiveScale:F3} m");
						Debug.Log("[ValheimVRM] VRM read successful");
						// LoadAsync transfers resource ownership to RuntimeGltfInstance.
						return loaded.Root;
					}
					catch
					{
						var failedRoot = AccessTools.Field(typeof(ImporterContext), "Root")?.GetValue(context) as GameObject;
						if (failedRoot != null) Object.Destroy(failedRoot);
						throw;
					}
				}
			}
		}

		public IEnumerator SetToPlayer(Player player, float? height = null, string calibration = null)
		{
			using (AvatarResidency.Acquire(this))
			{
				// Drive the child here so cancellation/errors unwind the attachment
				// lease in this iterator instead of escaping a nested Unity coroutine.
				var attachment = AttachToPlayer(player, height, calibration);
				try { while (attachment.MoveNext()) yield return attachment.Current; }
				finally { (attachment as IDisposable)?.Dispose(); }
			}
		}

		private IEnumerator AttachToPlayer(Player player, float? height, string calibration)
		{
			if (player == null) yield break;
			var animator = player.GetField<Player, Animator>("m_animator") ?? player.GetComponentInChildren<Animator>();
			while (animator == null)
			{
				yield return null;
				if (player == null) yield break;
				animator = player.GetComponentInChildren<Animator>();
			}

			var vrmController = player.GetComponent<VrmController>();
			if (vrmController == null) yield break;

			var settings = Settings.GetSettings(Name);
			if (settings == null) yield break;
			// Capture native state before the first local or remote attachment.
			(player.GetComponent<RemoteAvatarBaseline>() ?? player.gameObject.AddComponent<RemoteAvatarBaseline>()).Capture();
			var networkView = player.GetComponent<ZNetView>();
			bool localPhysics = networkView == null || networkView.GetZDO() == null || networkView.IsOwner();
			if (localPhysics) player.m_maxInteractDistance *= settings.InteractionDistanceScale;

			var parent = animator.transform != null ? animator.transform.parent : null;
			if (parent == null || VisualModel == null) yield break;
			var vrmModel = Object.Instantiate(VisualModel);
			if (vrmModel == null) yield break;
			AvatarResidency.Bind(this, vrmModel);
			VrmManager.PlayerToVrmInstance[player] = vrmModel;
			vrmModel.name = "VRM_Visual";
			vrmController.visual = vrmModel;
			Func<bool> stillAttached = () => player != null && animator != null &&
				vrmModel != null && vrmController != null && vrmController.visual == vrmModel;

			var oldModel = parent.Find("VRM_Visual");
			if (oldModel != null)
			{
				Object.Destroy(oldModel.gameObject);
			}

			vrmModel.transform.SetParent(parent, false);
			vrmModel.transform.localPosition = animator.transform.localPosition;
			calibration = calibration ?? AvatarSyncClient.Instance?.RemoteCalibration(player);
			AvatarCalibrationBinding calibrationBinding = null;
			if (calibration != null)
			{
				calibrationBinding = vrmModel.AddComponent<AvatarCalibrationBinding>();
				if (!calibrationBinding.Initialize(settings, calibration)) throw new InvalidOperationException("Invalid synchronized calibration.");
				settings = calibrationBinding.Settings;
			}
			// Scale the clone before springs, camera, and both posture calibrations.
			// The imported template and any other player's clone remain unchanged.
			AvatarScale.ApplyHeight(vrmModel, height ?? (player == Player.m_localPlayer
				? OutfitSwitcher.Instance?.Heights?.Get(player.GetPlayerName()) ?? AvatarScale.DefaultHeight
				: AvatarSyncClient.Instance?.RemoteHeight(player) ?? AvatarScale.DefaultHeight));
			// Initialize springs at the player's location, not at the import origin.
			PrepareVrm10Clone(VisualModel, vrmModel);
			var physicsWeight = vrmModel.GetComponent<AvatarPhysicsWeight>() ?? vrmModel.AddComponent<AvatarPhysicsWeight>();
			physicsWeight.Setup();
			if (calibrationBinding != null) physicsWeight.SynchronizedWeight = calibrationBinding.PhysicsWeight;
			vrmModel.SetActive(true);
			var equipmentSync = player.GetComponent<VRMEquipmentSync>() ?? player.gameObject.AddComponent<VRMEquipmentSync>();
			equipmentSync.Setup(animator, vrmModel.GetComponent<Animator>(), player.GetComponentInChildren<VisEquipment>(), settings);

			// Detach the previous camera binding even when the next avatar opts out.
			// Calibrate from this clone before any yield lets animation retargeting
			// change its rest-pose bones. Never select the player's vanilla animator.
			var eyeSync = player.GetComponent<VRMEyePositionSync>();
			if (eyeSync != null) eyeSync.ResetEyePosition();
			if (localPhysics && settings.FixCameraHeight)
			{
				var vrmAnimator = vrmModel.GetComponent<Animator>();
				if (vrmAnimator != null)
				{
					var vrmEye = vrmAnimator.GetBoneTransform(HumanBodyBones.LeftEye) ??
						vrmAnimator.GetBoneTransform(HumanBodyBones.Head) ??
						vrmAnimator.GetBoneTransform(HumanBodyBones.Neck);
					if (vrmEye != null)
					{
						if (eyeSync == null) eyeSync = player.gameObject.AddComponent<VRMEyePositionSync>();
						eyeSync.Setup(vrmEye, settings.ModelOffsetY);
					}
				}
			}

			float newHeight = settings.PlayerHeight;
			float newRadius = settings.PlayerRadius;

			var rigidBody = player.GetComponent<Rigidbody>();
			var collider = player.GetComponent<CapsuleCollider>();
			if (localPhysics && collider != null)
			{
				collider.height = newHeight;
				collider.radius = newRadius;
				collider.center = new Vector3(0, newHeight / 2, 0);
			}
			if (localPhysics && rigidBody != null && collider != null)
			{
				rigidBody.centerOfMass = collider.center;
			}

			yield return null;
			if (!stillAttached()) yield break;

			var originalVisual = player.GetVisual();
			if (originalVisual != null)
			{
				foreach (var smr in originalVisual.GetComponentsInChildren<SkinnedMeshRenderer>())
				{
					if (smr == null) continue;
					smr.forceRenderingOff = true;
					smr.updateWhenOffscreen = true;
					yield return null;
					if (!stillAttached()) yield break;
				}
			}

			var orgAnim = AccessTools.FieldRefAccess<Player, Animator>(player, "m_animator");
			if (orgAnim != null)
			{
				orgAnim.keepAnimatorStateOnDisable = true;
				orgAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
				vrmModel.transform.localPosition = orgAnim.transform.localPosition;
			}
			yield return null;
			if (!stillAttached()) yield break;

			var animationSync = vrmModel.GetComponent<VRMAnimationSync>();
			if (animationSync == null)
			{
				animationSync = vrmModel.AddComponent<VRMAnimationSync>();
			}
			if (orgAnim != null)
			{
				animationSync.Setup(orgAnim, settings, false);
			}
			yield return null;

			if (!stillAttached()) yield break;

			if (settings.UseMToonShader)
			{
				var mToonColorSync = vrmModel.GetComponent<MToonColorSync>() ?? vrmModel.AddComponent<MToonColorSync>();
				mToonColorSync.Setup(vrmModel);
			}
			yield return null;
			if (!stillAttached()) yield break;

			foreach (var springBone in vrmModel.GetComponentsInChildren<VRMSpringBone>())
			{
				if (springBone == null) continue;
				springBone.m_stiffnessForce *= settings.SpringBoneStiffness;
				springBone.m_gravityPower *= settings.SpringBoneGravityPower;
				// Legacy springs also run after the retargeted humanoid pose. Keep
				// the avatar's authored simulation center and collision settings.
				springBone.m_updateType = VRMSpringBone.SpringBoneUpdateType.LateUpdate;
				yield return null;
				if (!stillAttached()) yield break;
			}

			if (player == null) yield break;
			var controller = player.GetComponent<VrmController>();
			if (controller != null)
			{
				controller.ReloadSpringBones();
			}
		}

		private static void PrepareVrm10Clone(GameObject source, GameObject model)
		{
			var vrm10 = model.GetComponent<Vrm10Instance>();
			if (vrm10 == null) return;
			var sourceVrm10 = source.GetComponent<Vrm10Instance>();
			if (sourceVrm10 != null) Vrm10SpringBoneClone.Copy(sourceVrm10, vrm10);

			var gltf = model.GetComponent<RuntimeGltfInstance>();
			if (gltf != null && gltf.InitialTransformStates.Count == 0 && gltf.RuntimeResources.Count == 0)
			{
				// Instantiate does not copy the importer's pose dictionary or resource ownership.
				// Remove the empty component so UniVRM captures the clone's own bind pose.
				Object.DestroyImmediate(gltf);
			}

			// Constraints must capture this pose before animation changes the transforms.
			_ = vrm10.Runtime;
		}
	}
}
