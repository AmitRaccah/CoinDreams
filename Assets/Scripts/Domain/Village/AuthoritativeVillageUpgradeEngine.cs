using System;
using Game.Domain.Player;
using Game.Domain.Time;

namespace Game.Domain.Village
{
    public static class AuthoritativeVillageUpgradeEngine
    {
        public static AuthoritativeVillageUpgradeResult TryExecute(
            PlayerProfileSnapshot snapshot,
            AuthoritativeVillageUpgradeRequest request)
        {
            if (snapshot == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Player snapshot is null.");
            }

            if (request == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Upgrade request is null.");
            }

            if (request.Catalog == null
                || request.Catalog.BuildingIds == null
                || request.Catalog.UpgradeCostsByBuilding == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Upgrade catalog is missing.");
            }

            VillageUpgradeCatalog catalog;
            try
            {
                catalog = new VillageUpgradeCatalog(
                    request.Catalog.BuildingIds,
                    request.Catalog.UpgradeCostsByBuilding);
            }
            catch (Exception exception)
            {
                return AuthoritativeVillageUpgradeResult.Invalid(
                    "Invalid village upgrade catalog: " + exception.Message);
            }

            PlayerProfile profile;
            try
            {
                profile = PlayerProfile.FromSnapshot(snapshot, new TimeProvider());
            }
            catch (Exception exception)
            {
                return AuthoritativeVillageUpgradeResult.Error(
                    "Failed to create profile from snapshot: " + exception.Message);
            }

            profile.EnsureVillageCapacity(catalog.BuildingCount);

            VillageUpgradeService upgradeService = new VillageUpgradeService(
                catalog,
                profile.Village,
                profile.Currency);

            if (!upgradeService.IsValid)
            {
                return AuthoritativeVillageUpgradeResult.Invalid(
                    "Upgrade service is invalid: " + upgradeService.ValidationMessage);
            }

            BuildingUpgradeResult upgradeResult = request.UseBuildingIndex
                ? upgradeService.TryUpgradeByIndex(request.BuildingIndex)
                : upgradeService.TryUpgrade(request.BuildingId);

            PlayerProfileSnapshot updatedSnapshot = profile.CreateSnapshot();
            return AuthoritativeVillageUpgradeResult.FromUpgrade(upgradeResult, updatedSnapshot);
        }
    }
}
