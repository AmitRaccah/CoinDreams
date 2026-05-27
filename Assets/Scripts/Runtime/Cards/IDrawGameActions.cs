#nullable enable

using System.Threading.Tasks;
using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Authoritative entry point for "draw a card" gameplay actions. Implementations
    /// reconcile against the persistence layer and return the canonical result.
    /// Restored as a standalone file after it was co-located with the deleted
    /// IDrawAnimator interface.
    /// </summary>
    public interface IDrawGameActions
    {
        Task<AuthoritativeDrawResult> TryDrawAsync();
    }
}
