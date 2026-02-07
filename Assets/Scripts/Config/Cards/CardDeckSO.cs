using System.Collections.Generic;
using UnityEngine;

namespace Game.Cards.Config
{
    [CreateAssetMenu(fileName = "CardDeck", menuName = "Cards/Card Deck")]
    public sealed class CardDeckSO : ScriptableObject
    {
        [SerializeField] private List<CardDefinitionSO> cards = new List<CardDefinitionSO>();

        public List<CardDefinitionSO> Cards
        {
            get { return cards; }
        }
    }
}
