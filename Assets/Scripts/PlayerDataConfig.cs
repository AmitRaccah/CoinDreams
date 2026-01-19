using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Scriptable Objects/PlayerData")]
public class PlayerDataConfig : ScriptableObject
{
    public int startingCurrency;
    public int startingEnergy;
    public int maxEnergy;
    public int rollCost;
}
