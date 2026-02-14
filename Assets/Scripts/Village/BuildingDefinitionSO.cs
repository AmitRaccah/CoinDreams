using System.Collections.Generic;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingDefinitionSO", menuName = "Buildings/BuildingDefinitionSO")]
public class BuildingDefinitionSO : ScriptableObject
{
    public string DisplayName;
    public string BuildingID;
    //TODO: UPGRADE BY LIST
    public List<BuildingLevelConfig> levels = new();

}


[Serializable]
public class BuildingLevelConfig
{
    public int upgradeCost;
}
