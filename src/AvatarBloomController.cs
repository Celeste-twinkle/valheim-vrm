using System;
using System.IO;
using UnityEngine;

namespace ValheimVRM
{
    public sealed class AvatarBloomController : MonoBehaviour
    {
        public static bool Enabled { get; set; } = true;
        internal static Shader BloomShader { get; private set; }
        AssetBundle bundle;

        void Awake()
        {
            try
            {
                using (var stream = typeof(AvatarBloomController).Assembly.GetManifestResourceStream("ValheimVRM.avatar_bloom"))
                {
                    if (stream == null) throw new FileNotFoundException("Embedded bloom shader bundle is missing");
                    using (var bytes = new MemoryStream())
                    {
                        stream.CopyTo(bytes);
                        bundle = AssetBundle.LoadFromMemory(bytes.ToArray());
                    }
                }
                if (bundle != null) BloomShader = bundle.LoadAsset<Shader>("Assets/AlbedoLit/AvatarBloom.shader");
                if (BloomShader == null || !BloomShader.isSupported)
                    throw new NotSupportedException("Avatar bloom shader is unavailable on this graphics device");
                Camera.onPreCull += PrepareCamera;
                Debug.Log("[ValheimVRM] Avatar bloom control initialized. Scene HDR color is unchanged.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ValheimVRM] Could not enable avatar bloom exclusion: " + ex.Message);
            }
        }

        void PrepareCamera(Camera camera)
        {
            // Reflection/shadow cameras must retain their ordinary lighting data.
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            var limiter = camera.GetComponent<AvatarBloomCamera>();
            if (limiter == null && Enabled && AvatarBloomTarget.Active.Count != 0)
                limiter = camera.gameObject.AddComponent<AvatarBloomCamera>();
            if (limiter != null) limiter.Prepare(camera);
        }

        void OnDestroy()
        {
            Camera.onPreCull -= PrepareCamera;
            foreach (var camera in Resources.FindObjectsOfTypeAll<AvatarBloomCamera>()) Destroy(camera);
            BloomShader = null;
            if (bundle != null) bundle.Unload(false);
        }
    }
}
