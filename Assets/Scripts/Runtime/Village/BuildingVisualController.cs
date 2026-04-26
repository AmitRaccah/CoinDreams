using System;
using UnityEngine;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class BuildingVisualController : MonoBehaviour
    {
        [SerializeField] private BuildingDefinitionSO buildingDefinition;
        [SerializeField] private GameObject[] partObjects = new GameObject[0];
        [SerializeField] private BuildingLevelVisual[] levelVisuals = Array.Empty<BuildingLevelVisual>();
        [SerializeField] private bool usePartObjectsAsLevelVisuals;
        [SerializeField] private bool combineLevelMeshesOnAwake;
        [SerializeField] private bool autoCollectChildrenWhenEmpty = true;
        [SerializeField] private bool applyLevelZeroOnAwake = true;

        private IBuildingVisualApplier visualApplier;
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

        private IBuildingVisualApplier CreateVisualApplier()
        {
            if (buildingDefinition == null)
            {
                Debug.LogError("[BuildingVisualController] Missing BuildingDefinitionSO.", this);
                return null;
            }

            GameObject[] resolvedPartObjects = ResolvePartObjects();
            bool hasConfiguredLevels = BuildingLevelVisualApplier.HasConfiguredLevelVisuals(levelVisuals);

            if (hasConfiguredLevels || usePartObjectsAsLevelVisuals)
            {
                BuildingLevelVisualApplier levelApplier = new BuildingLevelVisualApplier(
                    levelVisuals,
                    resolvedPartObjects,
                    usePartObjectsAsLevelVisuals,
                    combineLevelMeshesOnAwake);

                if (levelApplier.IsValid)
                {
                    return levelApplier;
                }

                levelApplier.Dispose();
            }

            if (resolvedPartObjects.Length == 0)
            {
                Debug.LogError("[BuildingVisualController] No part objects configured on root " + name + ".", this);
                return null;
            }

            return new BuildingPartStepVisualApplier(
                buildingDefinition,
                resolvedPartObjects,
                this);
        }

        private GameObject[] ResolvePartObjects()
        {
            GameObject[] configuredParts = partObjects;
            if (configuredParts != null && configuredParts.Length > 0)
            {
                return configuredParts;
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
