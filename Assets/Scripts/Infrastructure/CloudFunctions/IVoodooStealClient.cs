#nullable enable

using System.Threading.Tasks;

namespace Game.Infrastructure.CloudFunctions
{
    // Async wrapper over the voodoo callable cloud functions. Implementations
    // return typed result DTOs (no throws for normal-flow errors) — the
    // result-with-status pattern matches Game.Domain.Player.AuthoritativeStealResult.
    public interface IVoodooStealClient
    {
        Task<VoodooSessionBeginResponse> BeginVoodooSessionAsync();

        Task<VoodooStabResponse> ExecuteVoodooStabAsync(string sessionId);
    }
}
