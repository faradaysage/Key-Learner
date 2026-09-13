using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeyLearner.Unity
{
    [Serializable]
    public sealed class ContentItem
    {
        public string Category;
        public string Id;
        public GameObject Prefab;
        public float Height = 1;
        // Source-space dimensions before the game requests a uniform scale.
        public Vector3 Size = Vector3.one;
        public float Yaw;
    }

    public sealed class ContentLibrary : ScriptableObject
    {
        // Builder-owned source adaptations. SetItems deliberately leaves authored entries intact.
        public ContentItem[] Items = Array.Empty<ContentItem>();
        [Tooltip("Optional artist-owned replacements by stable Id, or additional content. Keep referenced prefabs/materials outside generated folders.")]
        public ContentItem[] AuthoredItems = Array.Empty<ContentItem>();
        readonly Dictionary<string, ContentItem[]> groups = new Dictionary<string, ContentItem[]>();
        ContentItem[] resolvedItems;

        public ContentItem[] Category(string category)
        {
            if (!groups.TryGetValue(category, out var result))
            {
                result = Array.FindAll(ResolvedItems(), i => i.Category == category);
                groups[category] = result;
            }
            return result;
        }

        public void SetItems(ContentItem[] items)
        {
            Items = items ?? Array.Empty<ContentItem>();
            Invalidate();
        }
        void OnEnable()
        {
            Invalidate();
        }
        void OnValidate()
        {
            Invalidate();
        }
        void Invalidate()
        {
            groups.Clear();
            resolvedItems = null;
        }

        ContentItem[] ResolvedItems()
        {
            if (resolvedItems != null)
                return resolvedItems;
            var merged = new List<ContentItem>();
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in Items ?? Array.Empty<ContentItem>())
            {
                if (item == null)
                    continue;
                if (!string.IsNullOrEmpty(item.Id))
                    positions[item.Id] = merged.Count;
                merged.Add(item);
            }
            foreach (var authored in AuthoredItems ?? Array.Empty<ContentItem>())
            {
                // An unfinished inspector row must not hide its working generated model.
                if (authored == null || !authored.Prefab || string.IsNullOrWhiteSpace(authored.Id) || string.IsNullOrWhiteSpace(authored.Category))
                    continue;
                if (positions.TryGetValue(authored.Id, out int position))
                    merged[position] = authored;
                else
                {
                    positions[authored.Id] = merged.Count;
                    merged.Add(authored);
                }
            }
            resolvedItems = merged.ToArray();
            return resolvedItems;
        }

        public GameObject Spawn(string category, int index, Transform parent, Vector3 position, float height, float yaw = 0)
        {
            var item = Pick(category, index);
            return item == null ? null : SpawnItem(item, parent, position, height / Mathf.Max(.001f, item.Height), yaw);
        }

        // Paths, driveways and broad ground details are sized by their authored
        // X width, preserving their thinness and every original proportion.
        public GameObject SpawnWidth(string category, int index, Transform parent, Vector3 position, float width, float yaw = 0)
        {
            var item = Pick(category, index);
            return item == null ? null : SpawnItem(item, parent, position, width / Mathf.Max(.001f, item.Size.x), yaw);
        }

        ContentItem Pick(string category, int index)
        {
            var choices = Category(category);
            return choices.Length == 0 ? null : choices[(int)(Math.Abs((long)index) % choices.Length)];
        }

        static GameObject SpawnItem(ContentItem item, Transform parent, Vector3 position, float scale, float yaw)
        {
            var go = UnityEngine.Object.Instantiate(item.Prefab, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw + item.Yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }
    }
}
