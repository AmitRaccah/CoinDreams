using System;
using UnityEngine;

namespace Game.Runtime.Village
{
    internal sealed class BuildingLevelVisualApplier
    {
        private readonly GameObject[] levelRoots;
        private readonly GeneratedMeshSet generatedMeshes;

        public BuildingLevelVisualApplier(
            GameObject[] levelRoots,
            bool combineMeshes)
        {
            this.levelRoots = levelRoots ?? Array.Empty<GameObject>();

            IsValid = HasAnyLevelRoot(this.levelRoots);
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

                return levelRoots.Length;
            }
        }

        public void ApplyLevel(int level)
        {
            if (!IsValid)
            {
                return;
            }

            int activeIndex = GetActiveRootIndex(level);

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

        private int GetActiveRootIndex(int level)
        {
            if (level <= 0)
            {
                return -1;
            }

            if (level > levelRoots.Length)
            {
                return levelRoots.Length - 1;
            }

            return level - 1;
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
