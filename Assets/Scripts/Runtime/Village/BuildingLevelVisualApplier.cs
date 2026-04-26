using System;
using UnityEngine;

namespace Game.Runtime.Village
{
    internal sealed class BuildingLevelVisualApplier : IBuildingVisualApplier
    {
        private readonly GameObject[] levelRoots;
        private readonly GeneratedMeshSet generatedMeshes;

        public BuildingLevelVisualApplier(
            BuildingLevelVisual[] configuredLevelVisuals,
            GameObject[] partObjects,
            bool usePartObjectsAsLevelVisuals,
            bool combineMeshes)
        {
            levelRoots = BuildLevelRoots(
                configuredLevelVisuals,
                partObjects,
                usePartObjectsAsLevelVisuals);

            IsValid = HasAnyLevelRoot(levelRoots);
            if (!IsValid || !combineMeshes)
            {
                return;
            }

            generatedMeshes = new GeneratedMeshSet();
            BuildingLevelMeshCombiner combiner = new BuildingLevelMeshCombiner();
            combiner.Combine(levelRoots, generatedMeshes);
        }

        public bool IsValid { get; private set; }

        public int MaxLevel
        {
            get
            {
                if (!IsValid || levelRoots.Length == 0)
                {
                    return 0;
                }

                return levelRoots.Length - 1;
            }
        }

        public void ApplyLevel(int level)
        {
            if (!IsValid)
            {
                return;
            }

            int activeIndex = ClampLevel(level);

            int i;
            for (i = 0; i < levelRoots.Length; i++)
            {
                GameObject visualRoot = levelRoots[i];
                if (visualRoot == null)
                {
                    continue;
                }

                bool shouldBeActive = i == activeIndex;
                if (visualRoot.activeSelf != shouldBeActive)
                {
                    visualRoot.SetActive(shouldBeActive);
                }
            }
        }

        public void Dispose()
        {
            if (generatedMeshes != null)
            {
                generatedMeshes.DestroyAll();
            }
        }

        public static bool HasConfiguredLevelVisuals(BuildingLevelVisual[] configuredLevelVisuals)
        {
            return configuredLevelVisuals != null && configuredLevelVisuals.Length > 0;
        }

        private int ClampLevel(int level)
        {
            if (level < 0)
            {
                return 0;
            }

            if (level >= levelRoots.Length)
            {
                return levelRoots.Length - 1;
            }

            return level;
        }

        private static GameObject[] BuildLevelRoots(
            BuildingLevelVisual[] configuredLevelVisuals,
            GameObject[] partObjects,
            bool usePartObjectsAsLevelVisuals)
        {
            int configuredCount = configuredLevelVisuals != null ? configuredLevelVisuals.Length : 0;
            if (configuredCount > 0)
            {
                GameObject[] roots = new GameObject[configuredCount];

                int i;
                for (i = 0; i < configuredCount; i++)
                {
                    BuildingLevelVisual visual = configuredLevelVisuals[i];
                    roots[i] = visual != null ? visual.root : null;
                }

                return roots;
            }

            if (!usePartObjectsAsLevelVisuals || partObjects == null || partObjects.Length == 0)
            {
                return Array.Empty<GameObject>();
            }

            GameObject[] partLevelRoots = new GameObject[partObjects.Length + 1];

            int partIndex;
            for (partIndex = 0; partIndex < partObjects.Length; partIndex++)
            {
                partLevelRoots[partIndex + 1] = partObjects[partIndex];
            }

            return partLevelRoots;
        }

        private static bool HasAnyLevelRoot(GameObject[] roots)
        {
            if (roots == null)
            {
                return false;
            }

            int i;
            for (i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
