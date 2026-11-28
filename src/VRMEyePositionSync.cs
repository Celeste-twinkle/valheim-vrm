using UnityEngine;

namespace ValheimVRM
{
    public class VRMEyePositionSync : MonoBehaviour
    {
        private Transform orgEye;
        private Vector3 originalLocalPosition;

        // Call while the new avatar is still in its rest pose. An animated head
        // is not a stable camera pivot: GameCamera uses m_eye directly for its
        // close-range collision casts, bypassing its smoothed base offset.
        public void Setup(Transform vrmEye, float modelOffsetY = 0f)
        {
            ResetEyePosition();
            var player = GetComponent<Player>();
            if (player == null || player.m_eye == null || vrmEye == null) return;

            orgEye = player.m_eye;
            originalLocalPosition = orgEye.localPosition;
            var position = orgEye.position;
            position.y = vrmEye.position.y + modelOffsetY;
            orgEye.position = position;
            enabled = true;
        }

        public void ResetEyePosition()
        {
            if (orgEye != null) orgEye.localPosition = originalLocalPosition;
            orgEye = null;
        }

        void OnDisable()
        {
            ResetEyePosition();
        }

        void OnDestroy()
        {
            ResetEyePosition();
        }
    }
}
