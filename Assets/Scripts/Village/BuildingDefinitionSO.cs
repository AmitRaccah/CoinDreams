using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingDefinitionSO", menuName = "Buildings/BuildingDefinitionSO")]
public sealed class BuildingDefinitionSO : ScriptableObject
{
    public string DisplayName;
    public string BuildingID;

    // upgradeSteps[i] is the upgrade from level i to level i + 1.
    public List<BuildingUpgradeStepConfig> upgradeSteps = new List<BuildingUpgradeStepConfig>();
}

[Serializable]
public sealed class BuildingUpgradeStepConfig
{
    public int upgradeCost;
}
