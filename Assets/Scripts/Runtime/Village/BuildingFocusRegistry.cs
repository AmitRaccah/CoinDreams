#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Village
{
    /// <summary>
    /// Camera-side discovery for the per-building focus points. At first use
    /// (or scene Start) it scans the loaded scene once for every
    /// <see cref="BuildingUpgradeFocusPoint"/> and maps them by building id,
    /// matching how <c>VillageBuildingVisualBindings</c> maps visuals by id.
    ///
    /// Buildings are static scene objects, so a single cold scan is enough —
    /// no per-frame work and no allocations during play. The
    /// <see cref="VillageCameraDirector"/> consumes this as a sibling component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingFocusRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, BuildingUpgradeFocusPoint> byBuildingId =
            new Dictionary<string, BuildingUpgradeFocusPoint>(StringComparer.Ordinal);

        private bool built;

        public bool TryGet(string buildingId, out BuildingUpgradeFocusPoint focusPoint)
        {
            EnsureBuilt();

            if (string.IsNullOrEmpty(buildingId))
            {
                focusPoint = null!;
                return false;
            }

            return byBuildingId.TryGetValue(buildingId, out focusPoint);
        }

        // Start (not Awake) so every building's own Awake has resolved its id
        // before we read it. TryGet also guards via EnsureBuilt for any earlier
        // caller.
        private void Start() => EnsureBuilt();

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;
            byBuildingId.Clear();

            BuildingUpgradeFocusPoint[] points = UnityEngine.Object.FindObjectsByType<BuildingUpgradeFocusPoint>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int i;
            for (i = 0; i < points.Length; i++)
            {
                BuildingUpgradeFocusPoint point = points[i];
                if (point == null)
                {
                    continue;
                }

                string id = point.BuildingId;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning("[BuildingFocusRegistry] Focus point with empty building id — skipping.", point);
                    continue;
                }

                if (byBuildingId.ContainsKey(id))
                {
                    Debug.LogWarning(
                        "[BuildingFocusRegistry] Duplicate focus point for building id " + id + " — keeping the first.",
                        point);
                    continue;
                }

                byBuildingId.Add(id, point);
            }
        }
    }
}
