using System.Threading.Tasks;

namespace Game.Runtime.Cards
{
    public interface IDrawAnimator
    {
        bool HasAnimation { get; }
        Task PlayDrawAnimationAsync();
    }
}
