using System;

namespace Game.Domain.Village
{
    public sealed class VillageProgressState
    {
        private int[] buildingLevels;
        public event Action Changed;

        public VillageProgressState(int buildingCount)
        {
            if (buildingCount < 0)
            {
                buildingCount = 0;
            }

            buildingLevels = new int[buildingCount];
        }

        public int BuildingCount
        {
            get { return buildingLevels.Length; }
        }

        public bool TryGetLevel(int index, out int level)
        {
            if (index < 0 || index >= buildingLevels.Length)
            {
                level = 0;
                return false;
            }

            level = buildingLevels[index];
            return true;
        }

        public int GetLevelOrDefault(int index)
        {
            int level;
            if (TryGetLevel(index, out level))
            {
                return level;
            }

            return 0;
        }

        public bool CanSetLevel(int index, int level)
        {
            if (index < 0 || index >= buildingLevels.Length)
            {
                return false;
            }

            if (level < 0)
            {
                return false;
            }

            return true;
        }

        public bool TrySetLevel(int index, int level)
        {
            if (!CanSetLevel(index, level))
            {
                return false;
            }

            if (buildingLevels[index] == level)
            {
                return true;
            }

            buildingLevels[index] = level;
            NotifyChanged();
            return true;
        }

        public void EnsureCapacity(int buildingCount)
        {
            if (buildingCount < 0)
            {
                buildingCount = 0;
            }

            if (buildingLevels.Length >= buildingCount)
            {
                return;
            }

            Array.Resize(ref buildingLevels, buildingCount);
            NotifyChanged();
        }

        public int[] GetLevelsSnapshot()
        {
            if (buildingLevels.Length == 0)
            {
                return Array.Empty<int>();
            }

            int[] snapshot = new int[buildingLevels.Length];
            Array.Copy(buildingLevels, snapshot, buildingLevels.Length);
            return snapshot;
        }

        public void SetLevels(int[] levels)
        {
            // Grow-only: never shrink the building array implicitly. Use ResetLevels for explicit shrink.
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            int targetLength = levels.Length > buildingLevels.Length
                ? levels.Length
                : buildingLevels.Length;

            int[] copy = new int[targetLength];
            int i;
            for (i = 0; i < buildingLevels.Length; i++)
            {
                copy[i] = buildingLevels[i];
            }

            int copyCount = levels.Length;
            for (i = 0; i < copyCount; i++)
            {
                int level = levels[i];
                if (level < 0)
                {
                    level = 0;
                }

                copy[i] = level;
            }

            if (AreEqual(buildingLevels, copy))
            {
                return;
            }

            buildingLevels = copy;
            NotifyChanged();
        }

        public void ResetLevels()
        {
            if (buildingLevels.Length == 0)
            {
                return;
            }

            buildingLevels = Array.Empty<int>();
            NotifyChanged();
        }

        public void ClampToCatalog(VillageUpgradeCatalog catalog)
        {
            if (catalog == null || buildingLevels.Length == 0)
            {
                return;
            }

            bool mutated = false;
            int i;
            for (i = 0; i < buildingLevels.Length; i++)
            {
                int maxLevel = catalog.GetMaxLevel(i);
                if (maxLevel < 0)
                {
                    maxLevel = 0;
                }

                int level = buildingLevels[i];
                if (level < 0)
                {
                    buildingLevels[i] = 0;
                    mutated = true;
                    continue;
                }

                if (level > maxLevel)
                {
                    buildingLevels[i] = maxLevel;
                    mutated = true;
                }
            }

            if (mutated)
            {
                NotifyChanged();
            }
        }

        private static bool AreEqual(int[] a, int[] b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            int i;
            for (i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void NotifyChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
