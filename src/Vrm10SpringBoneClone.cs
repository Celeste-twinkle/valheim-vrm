using System.Collections.Generic;
using UniVRM10;
using UnityEngine;
using UniGLTF;

namespace ValheimVRM
{
    internal static class Vrm10SpringBoneClone
    {
        // Unity's player-side Instantiate does not reliably copy UniVRM's nested
        // spring lists. Rebuild them before the clone creates its runtime. Map by
        // hierarchy/component index: names are not unique in imported avatars.
        internal static void Copy(Vrm10Instance source, Vrm10Instance target)
        {
            var map = new Dictionary<Object, Object>();
            MapHierarchy(source.transform, target.transform, map);
            var imported = source.GetComponent<RuntimeGltfInstance>();
            if (imported != null)
                foreach (var pose in imported.InitialTransformStates)
                {
                    var bone = Remap(pose.Key, map);
                    if (bone == null || bone == target.transform) continue;
                    bone.localPosition = pose.Value.LocalPosition;
                    bone.localRotation = pose.Value.LocalRotation;
                    bone.localScale = pose.Value.LocalScale;
                }
            var springs = new Vrm10InstanceSpringBone();
            foreach (var group in source.SpringBone.ColliderGroups)
            {
                var cloneGroup = Remap(group, map);
                if (cloneGroup == null) continue;
                cloneGroup.Colliders = new List<VRM10SpringBoneCollider>();
                foreach (var collider in group.Colliders)
                {
                    var cloneCollider = Remap(collider, map);
                    if (cloneCollider != null) cloneGroup.Colliders.Add(cloneCollider);
                }
                springs.ColliderGroups.Add(cloneGroup);
            }
            foreach (var spring in source.SpringBone.Springs)
            {
                var clone = new Vrm10InstanceSpringBone.Spring(spring.Name) { Center = Remap(spring.Center, map) };
                foreach (var joint in spring.Joints)
                {
                    var cloneJoint = Remap(joint, map);
                    if (cloneJoint != null) clone.Joints.Add(cloneJoint);
                }
                foreach (var group in spring.ColliderGroups)
                {
                    var cloneGroup = Remap(group, map);
                    if (cloneGroup != null) clone.ColliderGroups.Add(cloneGroup);
                }
                if (clone.Joints.Count > 0) springs.Springs.Add(clone);
            }
            target.SpringBone = springs;
        }

        static T Remap<T>(T source, Dictionary<Object, Object> map) where T : Object
        {
            return source != null && map.TryGetValue(source, out var target) ? target as T : null;
        }

        static void MapHierarchy(Transform source, Transform target, Dictionary<Object, Object> map)
        {
            var components = source.GetComponents<Component>();
            var clonedComponents = target.GetComponents<Component>();
            for (int i = 0; i < components.Length && i < clonedComponents.Length; i++)
                if (components[i] != null && clonedComponents[i] != null && components[i].GetType() == clonedComponents[i].GetType())
                    map[components[i]] = clonedComponents[i];
            for (int i = 0; i < source.childCount && i < target.childCount; i++)
                MapHierarchy(source.GetChild(i), target.GetChild(i), map);
        }
    }
}
