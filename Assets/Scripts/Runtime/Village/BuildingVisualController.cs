using System;
using UnityEngine;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class BuildingVisualController : MonoBehaviour
    {
        [SerializeField] private BuildingDefinitionSO buildingDefinition;
        [SerializeField] private GameObject[] levelRoots = Array.Empty<GameObject>();
        [SerializeField] private bool combineLevelMeshesOnAwake;
        [SerializeField] private bool autoCollectChildrenWhenEmpty = true;
        [SerializeField] private bool applyLevelZeroOnAwake = true;

        private BuildingLevelVisualApplier visualApplier;
        private bool cacheInitialized;

        public BuildingDefinitionSO Definition
        {
            get { return buildingDefinition; }
        }

        public string BuildingId
        {
            get
            {
                if (buildingDefinition == null)
                {
                    return string.Empty;
                }

                return buildingDefinition.BuildingID;
            }
        }

        public int MaxLevel
        {
            get
            {
                EnsureCache();
                return visualApplier != null && visualApplier.IsValid
                    ? visualApplier.MaxLevel
                    : 0;
            }
        }

        private void Awake()
        {
            EnsureCache();

            if (applyLevelZeroOnAwake)
            {
                ApplyLevel(0);
            }
        }

        private void OnDestroy()
        {
            if (visualApplier != null)
            {
                visualApplier.Dispose();
                visualApplier = null;
            }
        }

        public bool ApplyLevel(int level)
        {
            EnsureCache();

            if (visualApplier == null || !visualApplier.IsValid)
            {
                return false;
            }

            visualApplier.ApplyLevel(level);
            return true;
        }

        private void EnsureCache()
        {
            if (cacheInitialized)
            {
                return;
            }

            cacheInitialized = true;
            visualApplier = CreateVisualApplier();
        }

        private BuildingLevelVisualApplier CreateVisualApplier()
        {
            if (buildingDefinition == null)
            {
                Debug.LogError("[BuildingVisualController] Missing BuildingDefinitionSO.", this);
                return null;
            }

            GameObject[] resolvedLevelRoots = ResolveLevelRoots();
            if (resolvedLevelRoots.Length == 0)
            {
                Debug.LogError("[BuildingVisualController] No level roots configured on root " + name + ".", this);
                return null;
            }

            BuildingLevelVisualApplier levelApplier = new BuildingLevelVisualApplier(
                resolvedLevelRoots,
                combineLevelMeshesOnAwake);

            if (levelApplier.IsValid)
            {
                return levelApplier;
            }

            levelApplier.Dispose();
            return null;
        }

        private GameObject[] ResolveLevelRoots()
        {
            GameObject[] configuredLevelRoots = levelRoots;
            if (configuredLevelRoots != null && configuredLevelRoots.Length > 0)
            {
                return configuredLevelRoots;
            }

            if (!autoCollectChildrenWhenEmpty)
            {
                return Array.Empty<GameObject>();
            }

            int childCount = transform.childCount;
            if (childCount == 0)
            {
                return Array.Empty<GameObject>();
            }

            GameObject[] collected = new GameObject[childCount];
            int i;
            for (i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                collected[i] = child != null ? child.gameObject : null;
            }

            return collected;
        }
    }
}
