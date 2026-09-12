using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    // Capture the imported standing mesh once, before game animation or springs.
    // Serialized scalar data survives Instantiate; clones never measure a seated pose.
    public sealed class AvatarScale : MonoBehaviour
    {
        public const float MinimumHeight = 1.6f;
        [SerializeField] float unscaledHeight;
        public float UnscaledHeight => unscaledHeight;

        public static float Apply(GameObject root, float requestedScale)
        {
            var sizing = root.GetComponent<AvatarScale>() ?? root.AddComponent<AvatarScale>();
            if (sizing.unscaledHeight <= 0) sizing.unscaledHeight = Measure(root);
            if (!Finite(requestedScale) || requestedScale <= 0) requestedScale = 1;
            float scale = sizing.unscaledHeight > 0
                ? Mathf.Max(requestedScale, MinimumHeight / sizing.unscaledHeight) : requestedScale;
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
                        skin.BakeMesh(baked);
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
