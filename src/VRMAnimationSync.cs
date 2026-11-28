using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ValheimVRM
{
	// Copy game animation before UniVRM 1.0 updates constraints and spring bones (11000).
	[DefaultExecutionOrder(10000)]
	public class VRMAnimationSync : MonoBehaviour
	{
		private Animator orgAnim, vrmAnim;
		private HumanPoseHandler orgPose, vrmPose;
		private HumanPose hp = new HumanPose();
		private bool ragdoll;
		private Settings.VrmSettingsContainer settings;
		private readonly Quaternion[] boneRotationOffsets = new Quaternion[(int)HumanBodyBones.LastBone];
		private readonly bool[] hasBoneRotationOffset = new bool[(int)HumanBodyBones.LastBone];
		private HumanBodyBones[] ragdollBones;
		private AvatarPoseCalibration calibration;
		private AvatarCalibrationOptions.Profile profile;
		private readonly List<AnimatorClipInfo> clipInfo = new List<AnimatorClipInfo>();
		public AvatarAnimationCatalog AnimationCatalog { get; private set; }
		public int CalibratedStateCount => calibration?.Count ?? 0;

		public void Setup(Animator orgAnim, Settings.VrmSettingsContainer settings, bool isRagdoll = false)
		{
			this.ragdoll = isRagdoll;
			this.settings = settings;
			this.orgAnim = orgAnim;
			this.vrmAnim = GetComponent<Animator>();
			this.vrmAnim.applyRootMotion = true;
			this.vrmAnim.updateMode = orgAnim.updateMode;
			this.vrmAnim.feetPivotActive = orgAnim.feetPivotActive;
			this.vrmAnim.layersAffectMassCenter = orgAnim.layersAffectMassCenter;
			this.vrmAnim.stabilizeFeet = orgAnim.stabilizeFeet;

			PoseHandlerCreate(orgAnim, vrmAnim);
			if (isRagdoll)
			{
				PrepareRagdollBones();
			}
			else
			{
				AnimationCatalog = new AvatarAnimationCatalog(orgAnim);
				profile = GetComponent<AvatarCalibrationBinding>()?.Profile ?? AvatarCalibrationOptions.Current.Get(settings.Name);
				calibration = new AvatarPoseCalibration(orgAnim, vrmAnim, AnimationCatalog, vrmPose);
			}
		}

		private void PrepareRagdollBones()
		{
			ragdollBones = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
				.Select(index => (HumanBodyBones)index)
				.Where(bone => orgAnim.GetBoneTransform(bone) != null && vrmAnim.GetBoneTransform(bone) != null)
				.OrderBy(bone => GetBoneDepth(vrmAnim.GetBoneTransform(bone)))
				.ToArray();

			foreach (var bone in ragdollBones)
			{
				int index = (int)bone;
				if (hasBoneRotationOffset[index]) continue;

				var source = orgAnim.GetBoneTransform(bone);
				var target = vrmAnim.GetBoneTransform(bone);
				boneRotationOffsets[index] = Quaternion.Inverse(source.rotation) * target.rotation;
			}
		}

		private static int GetBoneDepth(Transform bone)
		{
			int depth = 0;
			while (bone.parent != null)
			{
				depth++;
				bone = bone.parent;
			}
			return depth;
		}

		private void CacheBoneRotationOffsets()
		{
			for (int index = 0; index < (int)HumanBodyBones.LastBone; index++)
			{
				var bone = (HumanBodyBones)index;
				var source = orgAnim.GetBoneTransform(bone);
				var target = vrmAnim.GetBoneTransform(bone);
				if (source == null || target == null) continue;

				boneRotationOffsets[index] = Quaternion.Inverse(source.rotation) * target.rotation;
				hasBoneRotationOffset[index] = true;
			}
		}

		void PoseHandlerCreate(Animator org, Animator vrm)
		{
			OnDestroy();
			orgPose = new HumanPoseHandler(org.avatar, org.transform);
			vrmPose = new HumanPoseHandler(vrm.avatar, vrm.transform);
		}

		void OnDestroy()
		{
			if (orgPose != null)
				orgPose.Dispose();
			if (vrmPose != null)
				vrmPose.Dispose();
			orgPose = vrmPose = null;
		}

		void LateUpdate()
		{
			if (ragdoll)
			{
				vrmAnim.transform.localPosition = Vector3.zero;
				var verticalOffset = Vector3.up * settings.ModelOffsetY;

				// Physics does not update HumanPoseHandler. Apply rotations parent first,
				// using the live avatar's bone axes and retaining its own bone lengths.
				foreach (var bone in ragdollBones)
				{
					var source = orgAnim.GetBoneTransform(bone);
					var target = vrmAnim.GetBoneTransform(bone);
					target.rotation = source.rotation * boneRotationOffsets[(int)bone];
				}
				vrmAnim.GetBoneTransform(HumanBodyBones.Hips).position =
					orgAnim.GetBoneTransform(HumanBodyBones.Hips).position + verticalOffset;
				return;
			}

			vrmAnim.transform.localPosition = Vector3.zero;

			// One-way transfer: the game skeleton is an input, never a destination.
			// Start from this frame's animation before applying an absolute contact delta.
			// Writing corrected VRM bones back would feed last frame's lift into the next.
			orgPose.GetHumanPose(ref hp);
			vrmPose.SetHumanPose(ref hp);

            var hips = vrmAnim.GetBoneTransform(HumanBodyBones.Hips);
            Vector3 offset = LayerOffset(0);
            for (int layer = 1; layer < orgAnim.layerCount; layer++)
                offset += LayerOffset(layer) * orgAnim.GetLayerWeight(layer);
            hips.position = orgAnim.GetBoneTransform(HumanBodyBones.Hips).position + orgAnim.transform.rotation * offset;
            vrmAnim.transform.localPosition = Vector3.up * settings.ModelOffsetY;
            CacheBoneRotationOffsets();
        }

        public bool IsActive(AvatarAnimationEntry entry)
        {
            if (orgAnim == null || entry == null || entry.LayerIndex >= orgAnim.layerCount) return false;
            if (entry.LayerIndex > 0 && orgAnim.GetLayerWeight(entry.LayerIndex) <= 0) return false;
            return orgAnim.GetCurrentAnimatorStateInfo(entry.LayerIndex).fullPathHash == entry.Hash ||
                (orgAnim.IsInTransition(entry.LayerIndex) && orgAnim.GetNextAnimatorStateInfo(entry.LayerIndex).fullPathHash == entry.Hash);
        }

        Vector3 LayerOffset(int layer)
        {
            var state = orgAnim.GetCurrentAnimatorStateInfo(layer);
            var entry = AnimationCatalog.Resolve(orgAnim, layer, state, false);
            var result = StateOffset(entry, layer, false);
            if (orgAnim.IsInTransition(layer))
            {
                var next = AnimationCatalog.Resolve(orgAnim, layer, orgAnim.GetNextAnimatorStateInfo(layer), true);
                if (next != null) result = Vector3.Lerp(result, StateOffset(next, layer, true),
                    Mathf.Clamp01(orgAnim.GetAnimatorTransitionInfo(layer).normalizedTime));
            }
            return result;
        }

        Vector3 StateOffset(AvatarAnimationEntry entry, int layer, bool next)
        {
            var result = layer == 0 ? calibration.Offset(entry, orgAnim, vrmAnim) : Vector3.zero;
            if (entry == null) return result;
            result += profile.Get(entry.Path);
            if (layer == 0)
            {
                bool sitting = entry.Contact == AvatarContactKind.Ground || entry.Contact == AvatarContactKind.Seat;
                if (sitting || entry.Contact == AvatarContactKind.Feet)
                    result.y += AvatarHeightOffsets.Clamp(sitting ? settings.SittingHeightOffset : settings.StandingHeightOffset);
                // Preserve existing per-model position settings exactly once.
                result += LegacyOffset(entry.Name);
            }
            if (entry.Clips.Length > 1)
            {
                clipInfo.Clear();
                if (next) orgAnim.GetNextAnimatorClipInfo(layer, clipInfo); else orgAnim.GetCurrentAnimatorClipInfo(layer, clipInfo);
                float weight = 0; Vector3 clipOffset = Vector3.zero;
                foreach (var clip in clipInfo)
                {
                    if (clip.clip == null || clip.weight <= 0) continue;
                    weight += clip.weight; clipOffset += profile.Get(entry.ClipKey(clip.clip.name)) * clip.weight;
                }
                if (weight > .00001f) result += clipOffset / weight;
            }
            return result;
        }

        Vector3 LegacyOffset(string name)
        {
            switch (name)
            {
                case "sit down": case "Emote_sit": return settings.SittingIdleOffset;
                case "SitChair": return settings.SittingOnChairOffset;
                case "SitThrone": return settings.SittingOnThroneOffset;
                case "SitShip": return settings.SittingOnShipOffset;
                case "HoldMast": return settings.HoldingMastOffset;
                case "HoldDragon": return settings.HoldingDragonOffset;
                case "Sleeping": return settings.SleepingOffset;
                default: return Vector3.zero;
            }
        }
    }
}
