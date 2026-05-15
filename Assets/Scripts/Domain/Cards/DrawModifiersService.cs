namespace Game.Domain.Cards
{
    public sealed class DrawModifiersService
    {
        private readonly int multiplier;

        public DrawModifiersService(int multiplier)
        {
            this.multiplier = multiplier < 1 ? 1 : multiplier;
        }

        public int GetCurrentDrawMultiplier()
        {
            return multiplier;
        }
    }
}
