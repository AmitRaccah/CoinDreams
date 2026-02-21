using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class BuildingPartsRegistry : MonoBehaviour
    {
        [SerializeField] private BuildingPartBinding[] parts = new BuildingPartBinding[0];

        private readonly Dictionary<int, int> indexByPartId = new Dictionary<int, int>();
        private GameObject[] partObjects = Array.Empty<GameObject>();
        private Renderer[] partRenderers = Array.Empty<Renderer>();
        private bool initialized;
        private bool valid;

        public bool IsValid
        {
            get
            {
                EnsureInitialized();
                return valid;
            }
        }

        public int PartCount
        {
            get
            {
                EnsureInitialized();
                return partObjects.Length;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public bool TryGetPartIndex(int partId, out int partIndex)
        {
            EnsureInitialized();

            if (!valid)
            {
                partIndex = -1;
                return false;
            }

            return indexByPartId.TryGetValue(partId, out partIndex);
        }

        public bool TryGetPartObjectByIndex(int partIndex, out GameObject partObject)
        {
            EnsureInitialized();

            if (!valid || partIndex < 0 || partIndex >= partObjects.Length)
            {
                partObject = null;
                return false;
            }

            partObject = partObjects[partIndex];
            return partObject != null;
        }

        public bool TryGetPartRendererByIndex(int partIndex, out Renderer partRenderer)
        {
            EnsureInitialized();

            if (!valid || partIndex < 0 || partIndex >= partRenderers.Length)
            {
                partRenderer = null;
                return false;
            }

            partRenderer = partRenderers[partIndex];
            return partRenderer != null;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            valid = BuildCache(out string error);

            if (!valid)
            {
                Debug.LogError("[BuildingPartsRegistry] " + error, this);
            }
        }

        private bool BuildCache(out string error)
        {
            error = string.Empty;
            indexByPartId.Clear();

            if (parts == null)
            {
                parts = Array.Empty<BuildingPartBinding>();
            }

            List<GameObject> objects = new List<GameObject>(parts.Length);
            List<Renderer> renderers = new List<Renderer>(parts.Length);

            int i;
            for (i = 0; i < parts.Length; i++)
            {
                BuildingPartBinding binding = parts[i];
                if (binding == null)
                {
                    continue;
                }

                if (indexByPartId.ContainsKey(binding.partId))
                {
                    error = "Duplicate part ID " + binding.partId + " on " + name + ".";
                    return false;
                }

                GameObject targetObject = binding.targetObject;
                if (targetObject == null)
                {
                    error = "Missing target object for part entry index " + i + " on " + name + ".";
                    return false;
                }

                int partIndex = objects.Count;
                indexByPartId.Add(binding.partId, partIndex);
                objects.Add(targetObject);
                renderers.Add(targetObject.GetComponent<Renderer>());
            }

            partObjects = objects.ToArray();
            partRenderers = renderers.ToArray();
            return true;
        }

        [Serializable]
        public sealed class BuildingPartBinding
        {
            public int partId;
            public GameObject targetObject;
        }
    }
}
