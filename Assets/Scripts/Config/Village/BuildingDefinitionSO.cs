using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Config.Village
{
    [CreateAssetMenu(fileName = "BuildingDefinitionSO", menuName = "Buildings/BuildingDefinitionSO")]
    public sealed class BuildingDefinitionSO : ScriptableObject
    {
        public string DisplayName;
        public string BuildingID;

        // Icon shown in the Buildings panel's UpgradeObject row. Keeps the
        // UI layer ignorant of the 3D building visuals — the panel only
        // cares about this 2D sprite.
        public Sprite uiIcon;

        // upgradeSteps[i] is the upgrade from level i to level i + 1.
        public List<BuildingUpgradeStepConfig> upgradeSteps = new List<BuildingUpgradeStepConfig>();
    }

    [Serializable]
    public sealed class BuildingUpgradeStepConfig
    {
        public int upgradeCost;

        // Visual state changes that should be applied when this upgrade succeeds.
        public List<BuildingPartVisualStateConfig> nextLevelPartStates = new List<BuildingPartVisualStateConfig>();
    }

    [Serializable]
    public sealed class BuildingPartVisualStateConfig
    {
        public int partIndex;
        public bool isActive;

        // Optional: if null, texture will not change for this part in this step.
        public Texture texture;
    }
}
