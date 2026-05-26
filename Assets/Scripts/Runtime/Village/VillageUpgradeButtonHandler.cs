using Game.Domain.Village;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class VillageUpgradeButtonHandler : MonoBehaviour
    {
        private enum TargetMode
        {
            ByBuildingId = 0,
            ByBuildingIndex = 1
        }

        [Header("References")]
        [SerializeField] private VillageUpgradeRuntime villageUpgradeRuntime;
        [SerializeField] private Button buttonComponent;

        [Header("Target")]
        [SerializeField] private TargetMode targetMode = TargetMode.ByBuildingId;
        [SerializeField] private string buildingId;
        [SerializeField] private int buildingIndex;

        [Header("Debug")]
        [SerializeField] private bool logResult = true;

        private void Awake()
        {
            if (buttonComponent == null)
            {
                buttonComponent = GetComponent<Button>();
            }
        }

        public async void OnUpgradeClicked()
        {
            if (buttonComponent != null)
            {
                buttonComponent.interactable = false;
            }

            try
            {
                if (villageUpgradeRuntime == null)
                {
                    Debug.LogError("[VillageUpgradeButtonHandler] Missing VillageUpgradeRuntime.", this);
                    return;
                }

                BuildingUpgradeResult result = targetMode == TargetMode.ByBuildingIndex
                    ? await villageUpgradeRuntime.TryUpgradeByIndex(buildingIndex)
                    : await villageUpgradeRuntime.TryUpgrade(buildingId);

                if (logResult)
                {
                    Debug.Log(
                        "[VillageUpgradeButtonHandler] Upgrade result: "
                        + result.Status
                        + " | BuildingIndex="
                        + result.BuildingIndex
                        + " | PrevLevel="
                        + result.PreviousLevel
                        + " | NewLevel="
                        + result.NewLevel
                        + " | Cost="
                        + result.Cost,
                        this);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Intentionally swallowed: user navigated away or scope was disposed.
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                if (buttonComponent != null)
                {
                    buttonComponent.interactable = true;
                }
            }
        }
    }
}
