#nullable enable

using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    public interface IDrawCardPresentation
    {
        float BeginDraw();

        float Present(AuthoritativeDrawResult result);
    }

    public sealed class NullDrawCardPresentation : IDrawCardPresentation
    {
        public float BeginDraw()
        {
            return 0f;
        }

        public float Present(AuthoritativeDrawResult result)
        {
            return 0f;
        }
    }
}
