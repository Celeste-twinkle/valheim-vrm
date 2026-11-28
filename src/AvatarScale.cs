using System.Collections.Generic;
using UnityEngine;
using ValheimVRM.Sync;

namespace ValheimVRM
{
    // Capture the imported standing mesh once, before game animation or springs.
    // Serialized scalar data survives Instantiate; clones never measure a seated pose.
    public sealed class AvatarScale : MonoBehaviour
    {
        public const float MinimumHeight = AvatarHeightRules.Minimum;
        public const float DefaultHeight = AvatarHeightRules.Default;
        public const float MaximumHeight = AvatarHeightRules.Maximum;
        [SerializeField] float unscaledHeight;
        [SerializeField] float targetHeight = DefaultHeight;
        public float UnscaledHeight => unscaledHeight;
        public float TargetHeight => targetHeight;

        public static float Apply(GameObject root, float requestedScale)
        {
            return ApplyHeight(root, DefaultHeight, requestedScale);
        }

        // A target height is absolute, never a multiplier of the previous scale.
        // Only player clones receive personal height; the shared import stays at 2 m.
        public static float ApplyHeight(GameObject root, float height, float fallbackScale = 1f)
        {
            var sizing = root.GetComponent<AvatarScale>() ?? root.AddComponent<AvatarScale>();
            if (sizing.unscaledHeight <= 0) sizing.unscaledHeight = Measure(root);
            sizing.targetHeight = AvatarHeightRules.Clamp(height);
            if (!Finite(fallbackScale) || fallbackScale <= 0) fallbackScale = 1;
            float scale = sizing.unscaledHeight > 0
                ? sizing.targetHeight / sizing.unscaledHeight : fallbackScale;
            root.transform.localScale = Vector3.one * scale;
            return scale;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static float Measure(GameObject root)
        {
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            var vertices = new List<Vector3>();
            var baked = new Mesh();
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    Mesh mesh;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        if (skin.sharedMesh == null) continue;
                        // Ignore loose renderer bounds; baking also includes authored blend shapes.
                        skin.BakeMesh(baked, true);
                        mesh = baked;
                    }
                    else if (renderer is MeshRenderer)
                        mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    else continue;
                    if (mesh == null || !mesh.isReadable) continue;
                    mesh.GetVertices(vertices);
                    var toRoot = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                    foreach (var vertex in vertices)
                    {
                        float y = toRoot.MultiplyPoint3x4(vertex).y;
                        if (!Finite(y)) continue;
                        bottom = Mathf.Min(bottom, y);
                        top = Mathf.Max(top, y);
                    }
                }
            }
            finally { Object.Destroy(baked); }
            float height = top - bottom;
            return Finite(height) && height > .0001f ? height : 0;
        }
    }
}
