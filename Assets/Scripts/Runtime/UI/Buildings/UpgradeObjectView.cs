#nullable enable

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Buildings
{
    /// <summary>
    /// Pure view of a single UpgradeObject row in the Buildings panel.
    /// Has SerializeFields for every child UI element and exposes plain
    /// setters: <see cref="SetBuilding"/>, <see cref="SetLevel"/>,
    /// <see cref="SetCost"/>. Fires <see cref="UpgradeClicked"/> when the
    /// upgrade button is pressed AND the building isn't at MAX (max-level
    /// click is swallowed as a no-op per design).
    ///
    /// SRP — zero game logic, zero DI dependencies. The presenter holds
    /// all the wiring; this class just translates data into pixels and
    /// click into event. Trivially testable with a stub prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeObjectView : MonoBehaviour
    {
        [Header("Building")]
        [SerializeField] private Image? buildingImage;

        [Header("Level Indicators")]
        [Tooltip("Parent transform that hosts the dynamically-spawned indicator Images. " +
            "Use a HorizontalLayoutGroup on this transform so spawned indicators auto-layout.")]
        [SerializeField] private Transform? indicatorContainer;
        [Tooltip("Prefab instantiated once per upgrade step. A simple Image with the empty " +
            "indicator sprite is enough — the view swaps its sprite based on current level.")]
        [SerializeField] private Image? indicatorPrefab;
        [SerializeField] private Sprite? filledIndicatorSprite;
        [SerializeField] private Sprite? emptyIndicatorSprite;

        [Header("Upgrade Button")]
        [SerializeField] private Button? upgradeButton;
        [SerializeField] private TMP_Text? costText;
        [Tooltip("Group holding the coin icon next to the cost text. Hidden when the " +
            "building reaches MAX level so the COMPLETE label stands alone.")]
        [SerializeField] private GameObject? coinIconGroup;
        [Tooltip("Label shown on the upgrade button once the building is fully maxed.")]
        [SerializeField] private string maxLevelLabel = "COMPLETE";

        /// <summary>Fired when the upgrade button is clicked AND the row isn't at max.</summary>
        public event Action? UpgradeClicked;

        private readonly List<Image> indicators = new List<Image>();
        private bool isMaxLevel;

        private void Awake()
        {
            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(HandleUpgradeClicked);
            }

            // Collect any indicator Images already living in the container
            // (hardcoded in the prefab). SetLevel then GROWS the list via
            // indicatorPrefab when more are needed, SHRINKS by destroying
            // when fewer are. This lets the artist ship a prefab with N
            // pre-arranged indicators that "just work" up to N levels, and
            // only fall back to dynamic instantiation past that.
            if (indicatorContainer != null)
            {
                for (int i = 0; i < indicatorContainer.childCount; i++)
                {
                    Image existing = indicatorContainer.GetChild(i).GetComponent<Image>();
                    if (existing != null) indicators.Add(existing);
                }
            }
        }

        private void OnDestroy()
        {
            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
            }
            UpgradeClicked = null;
        }

        public void SetBuilding(Sprite? icon)
        {
            if (buildingImage == null) return;
            buildingImage.sprite = icon;
            buildingImage.enabled = icon != null;
        }

        /// <summary>
        /// Grows / shrinks the indicator pool to match <paramref name="maxLevel"/>,
        /// then paints the first <paramref name="currentLevel"/> as filled and the
        /// rest as empty. Cheap to call every frame — sprite assignment is
        /// idempotent at the UGUI level.
        /// </summary>
        public void SetLevel(int currentLevel, int maxLevel)
        {
            if (indicatorContainer == null) return;
            if (maxLevel < 0) maxLevel = 0;

            // Grow only if we have a prefab to clone from. Without one, log
            // once and stop — the existing prefab-baked indicators still
            // get painted below, just the overflow is skipped.
            while (indicators.Count < maxLevel)
            {
                if (indicatorPrefab == null)
                {
                    Debug.LogWarning("[UpgradeObjectView] Need " + maxLevel
                        + " indicators but only " + indicators.Count
                        + " exist and no indicatorPrefab is assigned to grow further.", this);
                    break;
                }
                Image clone = Instantiate(indicatorPrefab, indicatorContainer);
                clone.gameObject.SetActive(true);
                indicators.Add(clone);
            }
            while (indicators.Count > maxLevel)
            {
                int lastIdx = indicators.Count - 1;
                Image last = indicators[lastIdx];
                indicators.RemoveAt(lastIdx);
                if (last != null) Destroy(last.gameObject);
            }

            int clamped = Mathf.Clamp(currentLevel, 0, indicators.Count);
            for (int i = 0; i < indicators.Count; i++)
            {
                indicators[i].sprite = i < clamped ? filledIndicatorSprite : emptyIndicatorSprite;
            }
        }

        /// <summary>
        /// Paints the cost label + coin icon, gates interactability on
        /// affordability, and switches into MAX mode when requested.
        /// </summary>
        public void SetCost(int cost, bool affordable, bool isMaxLevel)
        {
            this.isMaxLevel = isMaxLevel;

            if (costText != null)
            {
                costText.text = isMaxLevel ? maxLevelLabel : cost.ToString("N0");
            }

            if (coinIconGroup != null)
            {
                bool shouldShowIcon = !isMaxLevel;
                if (coinIconGroup.activeSelf != shouldShowIcon)
                {
                    coinIconGroup.SetActive(shouldShowIcon);
                }
            }

            if (upgradeButton != null)
            {
                // At MAX the button stays clickable but swallows the event;
                // affordability only matters before MAX.
                upgradeButton.interactable = isMaxLevel || affordable;
            }
        }

        private void HandleUpgradeClicked()
        {
            if (isMaxLevel) return; // designed no-op
            UpgradeClicked?.Invoke();
        }
    }
}
