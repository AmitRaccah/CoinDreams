#nullable enable

using System;
using System.Threading.Tasks;
using Game.Domain.Village;
using UnityEngine;

namespace Game.Runtime.Village
{
    internal sealed class AuthoritativeVillageUpgradeExecutor
    {
        private readonly MonoBehaviour logContext;
        private readonly IAuthoritativeVillageUpgradeService? upgradeService;
        private bool isUpgradeInFlight;

        public AuthoritativeVillageUpgradeExecutor(
            MonoBehaviour logContext,
            IAuthoritativeVillageUpgradeService? upgradeService)
        {
            this.logContext = logContext;
            this.upgradeService = upgradeService;
        }

        public void ResetInFlight()
        {
            isUpgradeInFlight = false;
        }

        public async Task<BuildingUpgradeResult> TryUpgradeAsync(
            int buildingIndex,
            AuthoritativeVillageUpgradeCatalogData catalogData)
        {
            if (isUpgradeInFlight)
            {
                return BuildingUpgradeResult.AlreadyInProgress(buildingIndex);
            }

            if (catalogData == null)
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            if (upgradeService == null || !upgradeService.IsReady)
            {
                Debug.LogWarning(
                    "[VillageUpgradeRuntime] Authoritative village upgrade service is not ready.",
                    logContext);
                return AuthoritativeVillageUpgradeResult
                    .Unavailable("Authoritative village upgrade service is not ready.")
                    .UpgradeResult;
            }

            isUpgradeInFlight = true;
            try
            {
                string upgradeRequestId = System.Guid.NewGuid().ToString("N");

                AuthoritativeVillageUpgradeRequest request =
                    AuthoritativeVillageUpgradeRequest.ForBuildingIndex(
                        catalogData,
                        buildingIndex,
                        upgradeRequestId);

                AuthoritativeVillageUpgradeResult authoritativeResult =
                    await upgradeService.TryUpgradeAsync(request);

                if (authoritativeResult == null)
                {
                    return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
                }

                if (!string.IsNullOrEmpty(authoritativeResult.Message))
                {
                    Debug.LogWarning(
                        "[VillageUpgradeRuntime] Authoritative upgrade message: "
                        + authoritativeResult.Message,
                        logContext);
                }

                return authoritativeResult.UpgradeResult;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[VillageUpgradeRuntime] Authoritative village upgrade failed: "
                    + exception.Message,
                    logContext);
                return AuthoritativeVillageUpgradeResult
                    .Error(exception.Message)
                    .UpgradeResult;
            }
            finally
            {
                isUpgradeInFlight = false;
            }
        }
    }
}
