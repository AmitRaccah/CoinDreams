using System;
using System.Threading.Tasks;
using Game.Domain.Village;
using Game.Runtime;
using UnityEngine;

namespace Game.Runtime.Village
{
    internal sealed class AuthoritativeVillageUpgradeExecutor
    {
        private readonly MonoBehaviour logContext;
        private MonoBehaviour serviceSource;
        private IAuthoritativeVillageUpgradeService upgradeService;
        private bool isUpgradeInFlight;

        public AuthoritativeVillageUpgradeExecutor(
            MonoBehaviour logContext,
            MonoBehaviour serviceSource)
        {
            this.logContext = logContext;
            this.serviceSource = serviceSource;
        }

        public MonoBehaviour ServiceSource
        {
            get { return serviceSource; }
        }

        public void SetServiceSource(MonoBehaviour source)
        {
            if (serviceSource == source)
            {
                return;
            }

            serviceSource = source;
            upgradeService = null;
        }

        public void ResetInFlight()
        {
            isUpgradeInFlight = false;
        }

        public bool ResolveService()
        {
            if (RuntimeServiceResolver.TryResolveAuthoritativeVillageUpgradeService(
                    serviceSource,
                    out upgradeService,
                    out MonoBehaviour resolvedSource))
            {
                serviceSource = resolvedSource;
                return true;
            }

            Debug.LogWarning(
                "[VillageUpgradeRuntime] No IAuthoritativeVillageUpgradeService implementation found in scene.",
                logContext);
            return false;
        }

        public async Task<BuildingUpgradeResult> TryUpgradeAsync(
            int buildingIndex,
            AuthoritativeVillageUpgradeCatalogData catalogData)
        {
            if (isUpgradeInFlight)
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            if (catalogData == null)
            {
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            if (upgradeService == null)
            {
                ResolveService();
            }

            if (upgradeService == null || !upgradeService.IsReady)
            {
                Debug.LogWarning(
                    "[VillageUpgradeRuntime] Authoritative village upgrade service is not ready.",
                    logContext);
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }

            isUpgradeInFlight = true;
            try
            {
                AuthoritativeVillageUpgradeRequest request =
                    AuthoritativeVillageUpgradeRequest.ForBuildingIndex(
                        catalogData,
                        buildingIndex);

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
                return BuildingUpgradeResult.InvalidConfiguration(buildingIndex);
            }
            finally
            {
                isUpgradeInFlight = false;
            }
        }
    }
}
