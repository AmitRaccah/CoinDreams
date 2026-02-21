using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VillageDefinitionSO", menuName = "Buildings/VillageDefinitionSO")]
public sealed class VillageDefinitionSO : ScriptableObject
{
    public List<BuildingDefinitionSO> buildings = new List<BuildingDefinitionSO>();
}
