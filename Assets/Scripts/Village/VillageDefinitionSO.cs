using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VillageDefinitionSO", menuName = "Buildings/VillageDefinitionSO")]
public class VillageDefinitionSO : ScriptableObject
{
    public List<BuildingDefinitionSO> buildings = new();
}
