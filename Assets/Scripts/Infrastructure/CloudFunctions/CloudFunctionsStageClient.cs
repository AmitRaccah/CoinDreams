#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using Game.Domain.Stages;
using Game.Domain.Village;
using Game.Infrastructure.Persistence;
using UnityEngine;

namespace Game.Infrastructure.CloudFunctions
{
    /// <summary>
    /// Firebase.Functions-backed <see cref="IStageAdvanceClient"/>. Calls the
    /// advanceStage callable defined under functions/. The server validates that
    /// every building is maxed, resets villageLevels to zero, and increments
    /// currentStage. We only read the status here — the reset state propagates
    /// back through LiveSync → ProfileReplaced, exactly like the voodoo stab.
    /// </summary>
    public sealed class CloudFunctionsStageClient : IStageAdvanceClient
    {
        private const string AdvanceStageName = "advanceStage";

        private readonly IFirebaseAuthService auth;
        private FirebaseFunctions? functions;

        public CloudFunctionsStageClient(IFirebaseAuthService auth)
        {
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        public async Task<StageAdvanceResponse> AdvanceStageAsync(
            AuthoritativeVillageUpgradeCatalogData catalog,
            string stageAdvanceId)
        {
            if (catalog == null || catalog.BuildingIds == null || catalog.UpgradeCostsByBuilding == null)
            {
                return StageAdvanceResponse.Error("Catalog is missing.");
            }
            if (string.IsNullOrWhiteSpace(stageAdvanceId))
            {
                return StageAdvanceResponse.Error("stageAdvanceId is required.");
            }
            if (!auth.IsReady)
            {
                return StageAdvanceResponse.Error("Firebase auth not ready.");
            }

            try
            {
                FirebaseFunctions resolved = ResolveFunctions();
                HttpsCallableReference callable = resolved.GetHttpsCallable(AdvanceStageName);

                var payload = new Dictionary<string, object>
                {
                    { "catalog", BuildCatalogPayload(catalog) },
                    { "stageAdvanceId", stageAdvanceId },
                };

                HttpsCallableResult result = await callable.CallAsync(payload);
                IDictionary<string, object>? data = result.Data as IDictionary<string, object>;
                if (data == null)
                {
                    return StageAdvanceResponse.Error("Empty response.");
                }

                int statusInt = data.TryGetInt("status", fallback: (int)StageAdvanceStatus.UnexpectedError);
                int newStage = data.TryGetInt("newStage", fallback: -1);
                string message = data.TryGetString("message");
                return new StageAdvanceResponse((StageAdvanceStatus)statusInt, newStage, message);
            }
            catch (FunctionsException ex)
            {
                Debug.LogWarning("[CloudFunctionsStageClient] advanceStage FunctionsException: " + ex.Message);
                return StageAdvanceResponse.Error(ex.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("[CloudFunctionsStageClient] advanceStage failed: " + ex.Message);
                return StageAdvanceResponse.Error(ex.Message);
            }
        }

        // Builds nested Lists (not raw jagged arrays) so the Firebase callable
        // serializer always sees List/primitive shapes it can map cleanly.
        private static Dictionary<string, object> BuildCatalogPayload(
            AuthoritativeVillageUpgradeCatalogData catalog)
        {
            var buildingIds = new List<object>(catalog.BuildingIds.Length);
            for (int i = 0; i < catalog.BuildingIds.Length; i++)
            {
                buildingIds.Add(catalog.BuildingIds[i] ?? string.Empty);
            }

            var costsByBuilding = new List<object>(catalog.UpgradeCostsByBuilding.Length);
            for (int i = 0; i < catalog.UpgradeCostsByBuilding.Length; i++)
            {
                int[] stepCosts = catalog.UpgradeCostsByBuilding[i];
                int stepCount = stepCosts != null ? stepCosts.Length : 0;
                var steps = new List<object>(stepCount);
                for (int j = 0; j < stepCount; j++)
                {
                    steps.Add(stepCosts![j]);
                }
                costsByBuilding.Add(steps);
            }

            return new Dictionary<string, object>
            {
                { "buildingIds", buildingIds },
                { "upgradeCostsByBuilding", costsByBuilding },
            };
        }

        // Production-only mode: the rest of the stack (Firestore, Auth, the
        // voodoo functions) also runs against production, so a callable here
        // resolves the same default instance.
        private FirebaseFunctions ResolveFunctions()
        {
            if (functions == null)
            {
                functions = FirebaseFunctions.DefaultInstance;
            }
            return functions;
        }
    }
}
