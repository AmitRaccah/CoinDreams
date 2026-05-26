using System;
using Game.Domain.Player;
using Game.Domain.Time;

namespace Game.Domain.Village
{
    public static class AuthoritativeVillageUpgradeEngine
    {
        public static AuthoritativeVillageUpgradeResult TryExecute(
            PlayerProfileSnapshot snapshot,
            AuthoritativeVillageUpgradeRequest request,
            ITimeProvider timeProvider)
        {
            if (snapshot == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Player snapshot is null.");
            }

            if (request == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Upgrade request is null.");
            }

            if (string.IsNullOrWhiteSpace(request.UpgradeRequestId))
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Missing UpgradeRequestId.");
            }

            if (timeProvider == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Time provider is null.");
            }

            if (request.Catalog == null
                || request.Catalog.BuildingIds == null
                || request.Catalog.UpgradeCostsByBuilding == null)
            {
                return AuthoritativeVillageUpgradeResult.Invalid("Upgrade catalog is missing.");
            }

            try
            {
                VillageUpgradeCatalog catalog;
                try
                {
                    catalog = new VillageUpgradeCatalog(
                        request.Catalog.BuildingIds,
                        request.Catalog.UpgradeCostsByBuilding);
                }
                catch (ArgumentException exception)
                {
                    return AuthoritativeVillageUpgradeResult.Invalid(
                        "Invalid village upgrade catalog: " + exception.Message);
                }

                PlayerProfile profile;
                try
                {
                    profile = PlayerProfile.FromSnapshot(snapshot, timeProvider);
                }
                catch (ArgumentException exception)
                {
                    return AuthoritativeVillageUpgradeResult.Invalid(
                        "Failed to create profile from snapshot: " + exception.Message);
                }

                // TODO: revisit revision-suppression for capacity grows. EnsureVillageCapacity is a no-op when
                // already at target length, so a fresh snapshot with matching length will not bump revision.
                profile.EnsureVillageCapacity(request.Catalog.BuildingIds.Length);
                profile.Village.ClampToCatalog(catalog);

                int probedBuildingIndex = ResolveBuildingIndex(catalog, request);

                if (profile.HasProcessedImpact(request.UpgradeRequestId))
                {
                    int currentLevel = probedBuildingIndex >= 0
                        ? profile.Village.GetLevelOrDefault(probedBuildingIndex)
                        : 0;

                    return AuthoritativeVillageUpgradeResult.FromUpgrade(
                        BuildingUpgradeResult.AlreadyApplied(probedBuildingIndex, currentLevel),
                        CreateSnapshotWithStamp(profile, request.UpgradeRequestId));
                }

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

                PlayerProfileSnapshot updatedSnapshot = CreateSnapshotWithStamp(profile, request.UpgradeRequestId);
                return AuthoritativeVillageUpgradeResult.FromUpgrade(upgradeResult, updatedSnapshot);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return AuthoritativeVillageUpgradeResult.Invalid(exception.Message);
            }
            catch (Exception exception)
            {
                return AuthoritativeVillageUpgradeResult.Error(exception.Message);
            }
        }

        private static int ResolveBuildingIndex(
            VillageUpgradeCatalog catalog,
            AuthoritativeVillageUpgradeRequest request)
        {
            if (request.UseBuildingIndex)
            {
                return request.BuildingIndex;
            }

            int buildingIndex;
            if (catalog.TryGetBuildingIndex(request.BuildingId, out buildingIndex))
            {
                return buildingIndex;
            }

            return -1;
        }

        private static PlayerProfileSnapshot CreateSnapshotWithStamp(
            PlayerProfile profile,
            string upgradeRequestId)
        {
            profile.ApplyTimeBasedRegen();
            PlayerProfileSnapshot snapshot = profile.CreateSnapshot();
            snapshot.processedImpactIds = AppendProcessedImpactId(
                snapshot.processedImpactIds,
                upgradeRequestId);
            return snapshot;
        }

        private static string[] AppendProcessedImpactId(string[] existing, string upgradeRequestId)
        {
            string trimmed = upgradeRequestId == null ? string.Empty : upgradeRequestId.Trim();
            if (trimmed.Length == 0)
            {
                return existing ?? Array.Empty<string>();
            }

            if (existing == null || existing.Length == 0)
            {
                return new[] { trimmed };
            }

            int i;
            for (i = 0; i < existing.Length; i++)
            {
                if (string.Equals(existing[i], trimmed, StringComparison.Ordinal))
                {
                    return existing;
                }
            }

            string[] copy = new string[existing.Length + 1];
            Array.Copy(existing, copy, existing.Length);
            copy[existing.Length] = trimmed;
            Array.Sort(copy, StringComparer.Ordinal);
            return copy;
        }
    }
}
