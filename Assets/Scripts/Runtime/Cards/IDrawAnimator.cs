using System.Threading.Tasks;
using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    public interface IDrawGameActions
    {
        Task<AuthoritativeDrawResult> TryDrawAsync();
    }

    public interface IDrawAnimator
    {
        bool HasAnimation { get; }
        Task PlayDrawAnimationAsync();
    }
}
