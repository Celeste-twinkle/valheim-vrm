using System.Collections.Generic;
using UnityEngine;

namespace ValheimVRM
{
    // This component follows instantiated visuals, including outfit changes and ragdolls.
    public sealed class AvatarBloomTarget : MonoBehaviour
    {
        internal static readonly HashSet<AvatarBloomTarget> Active = new HashSet<AvatarBloomTarget>();
        internal Renderer[] Renderers { get; private set; }

        void Awake()
        {
            Renderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        void OnDestroy() { Active.Remove(this); }
    }
}
