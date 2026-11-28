using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ValheimVRM
{
    public sealed class AvatarPartVisibility : MonoBehaviour
    {
        public sealed class Part
        {
            internal Renderer Renderer;
            internal bool AuthoredEnabled;
            internal Transform[] Ancestors;
            public string Id { get; internal set; }
            public string Name { get; internal set; }
            public string Path { get; internal set; }
            public bool AuthoredVisible { get; internal set; }
            public bool Hidden { get; internal set; }
            public bool Shown { get; internal set; }
            public bool Visible => Renderer != null && Renderer.enabled && Renderer.gameObject.activeInHierarchy;
            public bool Enabled => Renderer != null && Renderer.enabled;
        }

        readonly List<Part> parts = new List<Part>();
        readonly HashSet<string> hidden = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> shown = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<Transform, bool> authoredActive = new Dictionary<Transform, bool>();
        public IReadOnlyList<Part> Parts => parts;
        public IReadOnlyCollection<string> Hidden => hidden;
        public IReadOnlyCollection<string> Shown => shown;

        public void Initialize(IEnumerable<string> hiddenValues, IEnumerable<string> shownValues = null)
        {
            parts.Clear(); hidden.Clear(); shown.Clear(); authoredActive.Clear();
            Add(hidden, hiddenValues); Add(shown, shownValues); shown.ExceptWith(hidden);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                // A fur overlay follows its authored renderer automatically and
                // must not appear as a duplicate user-facing part.
                if (renderer.GetComponent<AvatarFurSurface>() != null) continue;
                string path = DisplayPath(renderer.transform);
                string identity = Identity(renderer, path);
                var ancestors = Ancestors(renderer.transform).ToArray();
                foreach (var ancestor in ancestors)
                    if (!authoredActive.ContainsKey(ancestor)) authoredActive[ancestor] = ancestor.gameObject.activeSelf;
                parts.Add(new Part {
                    Renderer = renderer, AuthoredEnabled = renderer.enabled, Ancestors = ancestors,
                    AuthoredVisible = renderer.enabled && ancestors.All(v => v.gameObject.activeSelf),
                    Id = Hash(identity), Name = renderer.gameObject.name, Path = path
                });
            }
            parts.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path));
            Apply();
        }

        public void SetVisible(string id, bool value)
        {
            var part = parts.FirstOrDefault(p => p.Id == id);
            if (part == null) return;
            hidden.Remove(id); shown.Remove(id);
            if (value != part.AuthoredVisible) (value ? shown : hidden).Add(id);
            Apply();
        }
        public void SetOverrides(IEnumerable<string> hiddenValues, IEnumerable<string> shownValues = null)
        {
            hidden.Clear(); shown.Clear(); Add(hidden, hiddenValues); Add(shown, shownValues); shown.ExceptWith(hidden); Apply();
        }
        public bool IsHidden(string id) => hidden.Contains(id);
        public bool IsShown(string id) => shown.Contains(id);
        public bool DesiredVisible(Part part) => shown.Contains(part.Id) || !hidden.Contains(part.Id) && part.AuthoredVisible;

        void Apply()
        {
            // Start from the imported hierarchy each time. Overrides are absolute,
            // so repeated application and respawn never accumulate state.
            foreach (var pair in authoredActive.OrderBy(p => Depth(p.Key)))
                if (pair.Key != null) pair.Key.gameObject.SetActive(pair.Value);
            foreach (var part in parts)
            {
                part.Hidden = hidden.Contains(part.Id); part.Shown = shown.Contains(part.Id);
                if (part.Shown) foreach (var ancestor in part.Ancestors.Reverse())
                    if (ancestor != null && !ancestor.gameObject.activeSelf) ancestor.gameObject.SetActive(true);
            }
            foreach (var part in parts)
            {
                if (part.Renderer == null) continue;
                if (part.Hidden) part.Renderer.enabled = false;
                else if (part.Shown) part.Renderer.enabled = true;
                else
                {
                    bool active = part.Ancestors.All(v => v != null && v.gameObject.activeSelf);
                    part.Renderer.enabled = active && !part.AuthoredVisible ? false : part.AuthoredEnabled;
                }
            }
        }

        void LateUpdate()
        {
            // Enforce explicit choices only. Parts left at model defaults remain
            // available to authored animation and visibility controllers.
            foreach (var part in parts)
            {
                if (part.Renderer == null) continue;
                if (part.Hidden) { if (part.Renderer.enabled) part.Renderer.enabled = false; continue; }
                if (!part.Shown) continue;
                foreach (var ancestor in part.Ancestors.Reverse())
                    if (ancestor != null && !ancestor.gameObject.activeSelf) ancestor.gameObject.SetActive(true);
                if (!part.Renderer.enabled) part.Renderer.enabled = true;
            }
        }

        static void Add(HashSet<string> target, IEnumerable<string> values)
        { foreach (var value in values ?? Enumerable.Empty<string>()) if (ValheimVRM.Sync.AvatarPartSync.ValidId(value)) target.Add(value); }
        IEnumerable<Transform> Ancestors(Transform value)
        { while (value != null && value != transform) { yield return value; value = value.parent; } }
        static int Depth(Transform value) { int depth = 0; while (value != null) { depth++; value = value.parent; } return depth; }
        string DisplayPath(Transform value)
        {
            var names = new List<string>();
            while (value != null && value != transform) { names.Add(value.name); value = value.parent; }
            names.Reverse(); return names.Count == 0 ? name : string.Join(" / ", names);
        }
        string Identity(Renderer renderer, string display)
        {
            var segments = new List<string>(); var value = renderer.transform;
            while (value != null && value != transform)
            { segments.Add(value.name + "[" + value.GetSiblingIndex() + "]"); value = value.parent; }
            segments.Reverse();
            int component = Array.IndexOf(renderer.GetComponents<Renderer>(), renderer);
            return string.Join("/", segments) + "|" + renderer.GetType().Name + "[" + component + "]|" + display;
        }
        static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2")));
        }
    }
}
