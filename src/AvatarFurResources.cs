using System;
using System.IO;
using UnityEngine;

namespace ValheimVRM
{
    // Only imported templates own this lease; clones use AvatarResidency to keep
    // their template alive. No update callback or resources for ordinary VRMs.
    public sealed class AvatarFurResources : MonoBehaviour
    {
        static AssetBundle bundle;
        static Shader shader;
        static int owners;
        [NonSerialized] bool owns;
        internal static Shader Acquire(GameObject root)
        {
            if (!SystemInfo.supportsGeometryShaders) return null;
            if (shader == null)
            {
                try
                {
                    using (var stream = typeof(AvatarFurResources).Assembly.GetManifestResourceStream("ValheimVRM.avatar_fur"))
                    using (var bytes = new MemoryStream())
                    {
                        if (stream == null) return null;
                        stream.CopyTo(bytes); bundle = AssetBundle.LoadFromMemory(bytes.ToArray());
                    }
                    shader = bundle.LoadAsset<Shader>("Assets/AvatarRendering/AvatarFur.shader");
                    if (shader == null || !shader.isSupported) { Unload(); return null; }
                }
                catch (Exception ex) { Unload(); Debug.LogWarning("[ValheimVRM] Cannot load optional fur shader: " + ex.Message); return null; }
            }
            var lease = root.GetComponent<AvatarFurResources>() ?? root.AddComponent<AvatarFurResources>();
            if (!lease.owns) { lease.owns = true; owners++; }
            return shader;
        }
        void OnDestroy()
        {
            if (!owns) return;
            owns = false;
            if (--owners == 0) Unload();
        }
        static void Unload()
        {
            shader = null;
            if (bundle != null) bundle.Unload(true);
            bundle = null;
        }
    }
}
