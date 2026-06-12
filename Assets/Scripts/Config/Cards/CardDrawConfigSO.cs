#nullable enable

using UnityEngine;

namespace Game.Config.Cards
{
    [CreateAssetMenu(menuName = "CoinDreams/Cards/Card Draw Config", fileName = "CardDrawConfig")]
    public sealed class CardDrawConfigSO : ScriptableObject
    {
        [Header("Cost")]
        [SerializeField] private int drawCost = 1;

        public int DrawCost => drawCost;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (drawCost < 0)
            {
                drawCost = 0;
            }
        }
#endif
    }
}
