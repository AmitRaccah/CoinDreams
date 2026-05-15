using System.Collections.Generic;
using UnityEngine;

namespace Game.Config.Village
{
    [CreateAssetMenu(fileName = "VillageDefinitionSO", menuName = "Buildings/VillageDefinitionSO")]
    public sealed class VillageDefinitionSO : ScriptableObject
    {
        public List<BuildingDefinitionSO> buildings = new List<BuildingDefinitionSO>();
    }
}
