using UnityEngine;

namespace Game.Config.Player
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "Player/PlayerData")]
    public class PlayerDataConfig : ScriptableObject
    {
        public int startingCurrency;
        public int startingEnergy;
        public int maxEnergy;
        public int rollCost;
    }
}
