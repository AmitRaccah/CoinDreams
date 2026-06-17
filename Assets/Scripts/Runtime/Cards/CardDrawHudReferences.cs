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

        [Header("Visibility holders (GameObjects toggled by DrawHudPresenter)")]
        [Tooltip("Shown only while baseEnergy < maxEnergy (still regenerating).")]
        [SerializeField] private GameObject? energyTimerHolder;
        [Tooltip("Shown only while baseEnergy >= maxEnergy AND extraEnergy > 0.")]
        [SerializeField] private GameObject? extraEnergyHolder;

        public Slider? EnergySlider => energySlider;
        public TMP_Text? EnergyText => energyText;
        public TMP_Text? EnergyTimerText => energyTimerText;
        public TMP_Text? ExtraEnergyText => extraEnergyText;
        public TMP_Text? CoinsText => coinsText;
        public TMP_Text? ResultText => resultText;
        public GameObject? EnergyTimerHolder => energyTimerHolder;
        public GameObject? ExtraEnergyHolder => extraEnergyHolder;
    }
}
