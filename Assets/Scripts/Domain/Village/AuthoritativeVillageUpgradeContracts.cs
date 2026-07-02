using System.Threading.Tasks;
using Game.Domain.Player;

namespace Game.Domain.Village
{
    public sealed class AuthoritativeVillageUpgradeCatalogData
    {
        public readonly string[] BuildingIds;
        public readonly int[][] UpgradeCostsByBuilding;

        public AuthoritativeVillageUpgradeCatalogData(
            string[] buildingIds,
            int[][] upgradeCostsByBuilding)
        {
            BuildingIds = buildingIds;
            UpgradeCostsByBuilding = upgradeCostsByBuilding;
        }
    }

    public sealed class AuthoritativeVillageUpgradeRequest
    {
        public readonly AuthoritativeVillageUpgradeCatalogData Catalog;
        public readonly string BuildingId;
        public readonly int BuildingIndex;
        public readonly bool UseBuildingIndex;
        public readonly string UpgradeRequestId;

        private AuthoritativeVillageUpgradeRequest(
            AuthoritativeVillageUpgradeCatalogData catalog,
            string buildingId,
            int buildingIndex,
            bool useBuildingIndex,
            string upgradeRequestId)
        {
            Catalog = catalog;
            BuildingId = buildingId;
            BuildingIndex = buildingIndex;
            UseBuildingIndex = useBuildingIndex;
            UpgradeRequestId = upgradeRequestId;
        }

        public static AuthoritativeVillageUpgradeRequest ForBuildingIndex(
            AuthoritativeVillageUpgradeCatalogData catalog,
            int buildingIndex,
            string upgradeRequestId)
        {
            return new AuthoritativeVillageUpgradeRequest(catalog, string.Empty, buildingIndex, true, upgradeRequestId);
        }
    }

    public sealed class AuthoritativeVillageUpgradeResult
    {
        public readonly BuildingUpgradeResult UpgradeResult;
        public readonly PlayerProfileSnapshot Snapshot;
        public readonly string Message;

        private AuthoritativeVillageUpgradeResult(
            BuildingUpgradeResult upgradeResult,
            PlayerProfileSnapshot snapshot,
            string message)
        {
            UpgradeResult = upgradeResult;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }

        public static AuthoritativeVillageUpgradeResult FromUpgrade(
            BuildingUpgradeResult upgradeResult,
            PlayerProfileSnapshot snapshot)
        {
            return new AuthoritativeVillageUpgradeResult(upgradeResult, snapshot, string.Empty);
        }

        public static AuthoritativeVillageUpgradeResult Invalid(string message)
        {
            return new AuthoritativeVillageUpgradeResult(
                BuildingUpgradeResult.InvalidConfiguration(),
                null,
                message);
        }

        public static AuthoritativeVillageUpgradeResult Unavailable(string message)
        {
            return new AuthoritativeVillageUpgradeResult(
                BuildingUpgradeResult.ServiceUnavailable(),
                null,
                message);
        }

        public static AuthoritativeVillageUpgradeResult Error(string message)
        {
            return new AuthoritativeVillageUpgradeResult(
                BuildingUpgradeResult.UnexpectedError(message),
                null,
                message);
        }
    }

    public interface IAuthoritativeVillageUpgradeService
    {
        bool IsReady { get; }
        Task<AuthoritativeVillageUpgradeResult> TryUpgradeAsync(
            AuthoritativeVillageUpgradeRequest request);
    }
}
