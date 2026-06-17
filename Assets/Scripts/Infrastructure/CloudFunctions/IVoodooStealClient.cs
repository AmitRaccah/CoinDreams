#nullable enable

using System.Threading.Tasks;

namespace Game.Infrastructure.CloudFunctions
{
    // Async wrapper over the voodoo callable cloud functions. Implementations
    // return typed result DTOs (no throws for normal-flow errors) — the
    // result-with-status pattern matches Game.Domain.Player.AuthoritativeStealResult.
    public interface IVoodooStealClient
    {
        /// <summary>
        /// Opens a new voodoo session for the calling thief.
        /// </summary>
        /// <param name="thiefMultiplier">
        /// Draw multiplier active when the steal card resolved (1, 2, 4, or 8).
        /// The server persists this on the session document and amplifies the
        /// thief's gain (not the victim's loss) on every stab.
        /// </param>
        Task<VoodooSessionBeginResponse> BeginVoodooSessionAsync(int thiefMultiplier);

        Task<VoodooStabResponse> ExecuteVoodooStabAsync(string sessionId);
    }
}
