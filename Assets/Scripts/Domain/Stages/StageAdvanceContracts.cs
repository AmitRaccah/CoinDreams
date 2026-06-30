using System.Threading.Tasks;
using Game.Domain.Village;

namespace Game.Domain.Stages
{
    /// <summary>
    /// Outcome of an advanceStage call. Mirrors the server StageAdvanceStatus
    /// enum (functions/src/types.ts); client-only failures (auth not ready,
    /// network) collapse to <see cref="UnexpectedError"/>.
    /// </summary>
    public enum StageAdvanceStatus
    {
        Success = 0,
        NotAllBuildingsMaxed = 1,
        InvalidConfiguration = 2,
        UnexpectedError = 3,
    }

    public sealed class StageAdvanceResponse
    {
        public StageAdvanceStatus Status { get; }
        public int NewStage { get; }
        public string Message { get; }

        public bool IsSuccess
        {
            get { return Status == StageAdvanceStatus.Success; }
        }

        public StageAdvanceResponse(StageAdvanceStatus status, int newStage, string message)
        {
            Status = status;
            NewStage = newStage;
            Message = message ?? string.Empty;
        }

        public static StageAdvanceResponse Error(string message)
        {
            return new StageAdvanceResponse(StageAdvanceStatus.UnexpectedError, -1, message);
        }
    }

    /// <summary>
    /// Calls the server-authoritative advanceStage endpoint. The implementation
    /// (CloudFunctionsStageClient) lives in Infrastructure; the runtime depends
    /// only on this abstraction. On success the server commits the reset
    /// village + bumped stage to Firestore — the new state arrives on the client
    /// via the existing LiveSync → ProfileReplaced path, NOT through this result.
    /// </summary>
    public interface IStageAdvanceClient
    {
        Task<StageAdvanceResponse> AdvanceStageAsync(
            AuthoritativeVillageUpgradeCatalogData catalog,
            string stageAdvanceId);
    }
}
