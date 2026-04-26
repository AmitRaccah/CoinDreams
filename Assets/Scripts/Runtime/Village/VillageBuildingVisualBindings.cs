using System;
using System.Collections.Generic;
using Game.Domain.Village;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Runtime.Village
{
    internal sealed class VillageBuildingVisualBindings
    {
        private readonly MonoBehaviour logContext;
        private readonly BuildingVisualController[] configuredVisuals;
        private BuildingVisualController[] visualsByBuildingIndex = Array.Empty<BuildingVisualController>();

        public VillageBuildingVisualBindings(
            MonoBehaviour logContext,
            BuildingVisualController[] configuredVisuals)
        {
            this.logContext = logContext;
            this.configuredVisuals = configuredVisuals;
        }

        public void Rebuild(VillageUpgradeService upgradeService)
        {
            visualsByBuildingIndex = Array.Empty<BuildingVisualController>();

            if (upgradeService == null || !upgradeService.IsValid)
            {
                return;
            }

            int buildingCount = upgradeService.BuildingCount;
            BuildingVisualController[] visualArray = new BuildingVisualController[buildingCount];
            Dictionary<string, BuildingVisualController> visualsById = BuildVisualLookup();

            int buildingIndex;
            for (buildingIndex = 0; buildingIndex < buildingCount; buildingIndex++)
            {
                string buildingId = upgradeService.GetBuildingIdByIndex(buildingIndex);
                BuildingVisualController visual = null;
                if (!string.IsNullOrEmpty(buildingId))
                {
                    visualsById.TryGetValue(buildingId, out visual);
                }

                visualArray[buildingIndex] = visual;

                if (visual == null)
                {
                    Debug.LogWarning(
                        "[VillageUpgradeRuntime] Missing root binding for building ID "
                        + buildingId
                        + ".",
                        logContext);
                }
            }

            visualsByBuildingIndex = visualArray;
        }

        public void Clear()
        {
            visualsByBuildingIndex = Array.Empty<BuildingVisualController>();
        }

        public void ApplyAll(VillageUpgradeService upgradeService)
        {
            if (upgradeService == null || !upgradeService.IsValid)
            {
                return;
            }

            int buildingIndex;
            for (buildingIndex = 0; buildingIndex < upgradeService.BuildingCount; buildingIndex++)
            {
                int currentLevel = upgradeService.GetCurrentLevelByIndex(buildingIndex);
                ApplyForIndex(buildingIndex, currentLevel);
            }
        }

        public void ApplyForIndex(int buildingIndex, int level)
        {
            if (buildingIndex < 0 || buildingIndex >= visualsByBuildingIndex.Length)
            {
                return;
            }

            BuildingVisualController visual = visualsByBuildingIndex[buildingIndex];
            if (visual == null)
            {
                return;
            }

            if (!visual.ApplyLevel(level))
            {
                Debug.LogWarning(
                    "[VillageUpgradeRuntime] Failed to apply visual level "
                    + level
                    + " for building index "
                    + buildingIndex
                    + ".",
                    visual);
            }
        }

        private Dictionary<string, BuildingVisualController> BuildVisualLookup()
        {
            BuildingVisualController[] source = ResolveVisualSources();
            Dictionary<string, BuildingVisualController> lookup =
                new Dictionary<string, BuildingVisualController>(source.Length, StringComparer.Ordinal);

            int i;
            for (i = 0; i < source.Length; i++)
            {
                BuildingVisualController visual = source[i];
                if (visual == null || visual.Definition == null)
                {
                    continue;
                }

                string buildingId = visual.BuildingId;
                if (string.IsNullOrEmpty(buildingId))
                {
                    Debug.LogWarning("[VillageUpgradeRuntime] BuildingVisualController has empty BuildingID.", visual);
                    continue;
                }

                if (lookup.ContainsKey(buildingId))
                {
                    Debug.LogWarning("[VillageUpgradeRuntime] Duplicate visual for BuildingID " + buildingId + ".", visual);
                    continue;
                }

                lookup.Add(buildingId, visual);
            }

            return lookup;
        }

        private BuildingVisualController[] ResolveVisualSources()
        {
            BuildingVisualController[] configured = configuredVisuals;
            if (configured != null && configured.Length > 0)
            {
                int validConfiguredCount = CountNonNullVisuals(configured);
                if (validConfiguredCount > 0)
                {
                    if (validConfiguredCount != configured.Length)
                    {
                        Debug.LogWarning(
                            "[VillageUpgradeRuntime] buildingVisualControllers contains null entries. Ignoring null entries.",
                            logContext);
                    }

                    return CopyNonNullVisuals(configured, validConfiguredCount);
                }

                Debug.LogWarning(
                    "[VillageUpgradeRuntime] buildingVisualControllers is configured but all entries are null. Falling back to auto-discovery.",
                    logContext);
            }

            if (logContext == null)
            {
                return Array.Empty<BuildingVisualController>();
            }

            BuildingVisualController[] inChildren =
                logContext.GetComponentsInChildren<BuildingVisualController>(true);
            if (inChildren != null && inChildren.Length > 0)
            {
                return inChildren;
            }

            BuildingVisualController[] inScene = UnityEngine.Object.FindObjectsByType<BuildingVisualController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (inScene == null || inScene.Length == 0)
            {
                return Array.Empty<BuildingVisualController>();
            }

            Scene currentScene = logContext.gameObject.scene;
            int validSceneCount = 0;

            int i;
            for (i = 0; i < inScene.Length; i++)
            {
                BuildingVisualController visual = inScene[i];
                if (visual == null || visual.gameObject.scene != currentScene)
                {
                    continue;
                }

                validSceneCount++;
            }

            if (validSceneCount == 0)
            {
                return Array.Empty<BuildingVisualController>();
            }

            return CopyVisualsInScene(inScene, currentScene, validSceneCount);
        }

        private static int CountNonNullVisuals(BuildingVisualController[] visuals)
        {
            int validCount = 0;

            int i;
            for (i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null)
                {
                    validCount++;
                }
            }

            return validCount;
        }

        private static BuildingVisualController[] CopyNonNullVisuals(
            BuildingVisualController[] source,
            int count)
        {
            BuildingVisualController[] copy = new BuildingVisualController[count];
            int copyIndex = 0;

            int i;
            for (i = 0; i < source.Length; i++)
            {
                BuildingVisualController visual = source[i];
                if (visual == null)
                {
                    continue;
                }

                copy[copyIndex] = visual;
                copyIndex++;
            }

            return copy;
        }

        private static BuildingVisualController[] CopyVisualsInScene(
            BuildingVisualController[] source,
            Scene scene,
            int count)
        {
            BuildingVisualController[] copy = new BuildingVisualController[count];
            int copyIndex = 0;

            int i;
            for (i = 0; i < source.Length; i++)
            {
                BuildingVisualController visual = source[i];
                if (visual == null || visual.gameObject.scene != scene)
                {
                    continue;
                }

                copy[copyIndex] = visual;
                copyIndex++;
            }

            return copy;
        }
    }
}
