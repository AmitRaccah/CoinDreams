#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using Game.Domain.Player;
using Game.Infrastructure.Persistence;
using UnityEngine;

namespace Game.Infrastructure.CloudFunctions
{
    // Firebase.Functions-backed implementation of IVoodooStealClient. Talks to the
    // beginVoodooSession / executeVoodooStab callables defined under functions/.
    //
    // Normal-flow errors come back inside the typed VoodooStabResult payload (the
    // server only throws HttpsError for auth / malformed-input failures), so the
    // happy path is "deserialize into typed response". Exceptions from the SDK
    // are mapped to the matching factory on the response DTOs.
    public sealed class CloudFunctionsStealClient : IVoodooStealClient
    {
        private const string EmulatorOrigin = "http://localhost:5001";
        private const string BeginVoodooSessionName = "beginVoodooSession";
        private const string ExecuteVoodooStabName = "executeVoodooStab";
        private const int DefaultMaxStabs = 3;

        private readonly IFirebaseAuthService auth;
        private FirebaseFunctions? functions;
        private bool emulatorConfigured;

        public CloudFunctionsStealClient(IFirebaseAuthService auth)
        {
            this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        public async Task<VoodooSessionBeginResponse> BeginVoodooSessionAsync()
        {
            if (!auth.IsReady)
            {
                return VoodooSessionBeginResponse.Error("Firebase auth not ready.");
            }
            try
            {
                FirebaseFunctions resolved = ResolveFunctions();
                HttpsCallableReference callable = resolved.GetHttpsCallable(BeginVoodooSessionName);
                HttpsCallableResult result = await callable.CallAsync();
                IDictionary<string, object>? data = result.Data as IDictionary<string, object>;
                if (data == null)
                {
                    return VoodooSessionBeginResponse.Error("Empty response.");
                }

                string sessionId = data.TryGetString("sessionId");
                string victimId = data.TryGetString("victimPlayerId");
                string displayName = data.TryGetString("victimDisplayName");
                int maxStabs = data.TryGetInt("maxStabs", fallback: DefaultMaxStabs);

                if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(victimId))
                {
                    return VoodooSessionBeginResponse.Error("Malformed response — sessionId or victimPlayerId missing.");
                }
                return VoodooSessionBeginResponse.Success(sessionId, victimId, displayName, maxStabs);
            }
            catch (FunctionsException ex)
            {
                return MapBeginFunctionError(ex);
            }
            catch (Exception ex)
            {
                Debug.LogError("[CloudFunctionsStealClient] BeginVoodooSession failed: " + ex.Message);
                return VoodooSessionBeginResponse.Error(ex.Message);
            }
        }

        public async Task<VoodooStabResponse> ExecuteVoodooStabAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return VoodooStabResponse.Invalid("sessionId is required.");
            }
            if (!auth.IsReady)
            {
                return VoodooStabResponse.Error("Firebase auth not ready.");
            }
            try
            {
                FirebaseFunctions resolved = ResolveFunctions();
                HttpsCallableReference callable = resolved.GetHttpsCallable(ExecuteVoodooStabName);
                var payload = new Dictionary<string, object> { { "sessionId", sessionId } };
                HttpsCallableResult result = await callable.CallAsync(payload);
                IDictionary<string, object>? data = result.Data as IDictionary<string, object>;
                if (data == null)
                {
                    return VoodooStabResponse.Error("Empty response.");
                }

                int statusInt = data.TryGetInt("status", fallback: (int)VoodooStabStatus.Error);
                VoodooStabStatus status = (VoodooStabStatus)statusInt;
                int stolenAmount = data.TryGetInt("stolenAmount");
                int stabsRemaining = data.TryGetInt("stabsRemaining");
                bool isDollBroken = data.TryGetBool("isDollBroken");
                string message = data.TryGetString("message");
                PlayerProfileSnapshot? thiefSnapshot = ParseThiefSnapshot(data);

                switch (status)
                {
                    case VoodooStabStatus.Success:
                        // Success factory requires a non-null snapshot; if the server
                        // omitted it we fall back to an empty snapshot rather than
                        // crashing the caller.
                        return VoodooStabResponse.Success(
                            stolenAmount,
                            stabsRemaining,
                            isDollBroken,
                            thiefSnapshot ?? new PlayerProfileSnapshot());
                    case VoodooStabStatus.VictimEmpty:
                        return VoodooStabResponse.VictimEmpty(stabsRemaining, isDollBroken);
                    case VoodooStabStatus.SessionNotFound:
                        return VoodooStabResponse.SessionNotFound();
                    case VoodooStabStatus.SessionExhausted:
                        return VoodooStabResponse.SessionExhausted();
                    case VoodooStabStatus.SessionExpired:
                        return VoodooStabResponse.SessionExpired();
                    case VoodooStabStatus.Unauthorized:
                        return VoodooStabResponse.Unauthorized();
                    case VoodooStabStatus.InvalidRequest:
                        return VoodooStabResponse.Invalid(message);
                    default:
                        return VoodooStabResponse.Error(message);
                }
            }
            catch (FunctionsException ex)
            {
                return MapStabFunctionError(ex);
            }
            catch (Exception ex)
            {
                Debug.LogError("[CloudFunctionsStealClient] ExecuteVoodooStab failed: " + ex.Message);
                return VoodooStabResponse.Error(ex.Message);
            }
        }

        private FirebaseFunctions ResolveFunctions()
        {
            if (functions == null)
            {
                functions = FirebaseFunctions.DefaultInstance;
            }
            if (!emulatorConfigured)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                functions.UseFunctionsEmulator(EmulatorOrigin);
                Debug.Log("[CloudFunctionsStealClient] Using functions emulator at " + EmulatorOrigin);
#endif
                emulatorConfigured = true;
            }
            return functions;
        }

        private static PlayerProfileSnapshot? ParseThiefSnapshot(IDictionary<string, object> data)
        {
            IDictionary<string, object>? snap = data.TryGetDictionary("thiefSnapshot");
            if (snap == null)
            {
                return null;
            }
            // Field-name mapping mirrors PlayerProfileSnapshot.cs exactly — the
            // server already emits these in camelCase. updatedAtUtcTicks /
            // schemaVersion are read defensively (ignored if absent) per design.
            PlayerProfileSnapshot snapshot = new PlayerProfileSnapshot
            {
                playerId = snap.TryGetString("playerId"),
                revision = snap.TryGetInt("revision"),
                coins = snap.TryGetInt("coins"),
                currentEnergy = snap.TryGetInt("currentEnergy"),
                regenMaxEnergy = snap.TryGetInt("regenMaxEnergy"),
                regenIntervalSeconds = snap.TryGetInt("regenIntervalSeconds"),
                lastRegenUtcTicks = snap.TryGetLong("lastRegenUtcTicks"),
                villageLevels = snap.TryGetIntArray("villageLevels"),
                processedImpactIds = snap.TryGetStringArray("processedImpactIds"),
            };
            return snapshot;
        }

        private static VoodooSessionBeginResponse MapBeginFunctionError(FunctionsException ex)
        {
            switch (ex.ErrorCode)
            {
                case FunctionsErrorCode.Unauthenticated:
                case FunctionsErrorCode.PermissionDenied:
                    return VoodooSessionBeginResponse.Unauthorized(ex.Message);
                case FunctionsErrorCode.FailedPrecondition:
                    return VoodooSessionBeginResponse.NoVictimsAvailable();
                default:
                    return VoodooSessionBeginResponse.Error(ex.Message);
            }
        }

        private static VoodooStabResponse MapStabFunctionError(FunctionsException ex)
        {
            switch (ex.ErrorCode)
            {
                case FunctionsErrorCode.Unauthenticated:
                case FunctionsErrorCode.PermissionDenied:
                    return VoodooStabResponse.Unauthorized();
                case FunctionsErrorCode.InvalidArgument:
                    return VoodooStabResponse.Invalid(ex.Message);
                case FunctionsErrorCode.NotFound:
                    return VoodooStabResponse.SessionNotFound();
                default:
                    return VoodooStabResponse.Error(ex.Message);
            }
        }
    }
}
