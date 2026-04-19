using Game.Domain.Cards;

namespace Game.Runtime.Cards
{
    public interface IDrawResultSink
    {
        void Present(AuthoritativeDrawResult result);
    }
}
