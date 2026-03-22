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

        private AuthoritativeVillageUpgradeRequest(
            AuthoritativeVillageUpgradeCatalogData catalog,
            string buildingId,
            int buildingIndex,
            bool useBuildingIndex)
        {
            Catalog = catalog;
            BuildingId = buildingId;
            BuildingIndex = buildingIndex;
            UseBuildingIndex = useBuildingIndex;
        }

        public static AuthoritativeVillageUpgradeRequest ForBuildingId(
            AuthoritativeVillageUpgradeCatalogData catalog,
            string buildingId)
        {
            return new AuthoritativeVillageUpgradeRequest(catalog, buildingId, -1, false);
        }

        public static AuthoritativeVillageUpgradeRequest ForBuildingIndex(
            AuthoritativeVillageUpgradeCatalogData catalog,
            int buildingIndex)
        {
            return new AuthoritativeVillageUpgradeRequest(catalog, string.Empty, buildingIndex, true);
        }
    }

    public sealed class AuthoritativeVillageUpgradeResult
    {
        public readonly BuildingUpgradeResult UpgradeResult;
        public readonly PlayerProfileSnapshot Snapshot;
        public readonly string Message;

        public bool IsSuccess
        {
            get { return UpgradeResult.Status == BuildingUpgradeStatus.Success; }
        }

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
                BuildingUpgradeResult.InvalidConfiguration(),
                null,
                message);
        }

        public static AuthoritativeVillageUpgradeResult Error(string message)
        {
            return new AuthoritativeVillageUpgradeResult(
                BuildingUpgradeResult.InvalidConfiguration(),
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
