#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDrawHudReferences : MonoBehaviour
    {
        [SerializeField] private Slider? energySlider;
        [SerializeField] private TMP_Text? energyText;
        [SerializeField] private TMP_Text? energyTimerText;
        [SerializeField] private TMP_Text? extraEnergyText;
        [SerializeField] private TMP_Text? coinsText;
        [SerializeField] private TMP_Text? resultText;

        public Slider? EnergySlider => energySlider;
        public TMP_Text? EnergyText => energyText;
        public TMP_Text? EnergyTimerText => energyTimerText;
        public TMP_Text? ExtraEnergyText => extraEnergyText;
        public TMP_Text? CoinsText => coinsText;
        public TMP_Text? ResultText => resultText;
    }
}
