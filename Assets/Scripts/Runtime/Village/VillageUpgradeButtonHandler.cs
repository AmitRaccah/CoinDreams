#nullable enable

using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

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
        [SerializeField] private Button? buttonComponent;

        [Header("Target")]
        [SerializeField] private TargetMode targetMode = TargetMode.ByBuildingId;
        [SerializeField] private string buildingId = string.Empty;
        [SerializeField] private int buildingIndex;

        [Inject] private IPublisher<VillageUpgradeRequestedSignal>? upgradePublisher;

        private void Awake()
        {
            if (buttonComponent == null)
            {
                buttonComponent = GetComponent<Button>();
            }
        }

        public void OnUpgradeClicked()
        {
            upgradePublisher?.Publish(
                new VillageUpgradeRequestedSignal(
                    buildingId,
                    buildingIndex,
                    targetMode == TargetMode.ByBuildingIndex));
        }
    }
}
