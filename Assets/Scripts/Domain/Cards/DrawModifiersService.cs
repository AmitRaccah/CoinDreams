namespace Game.Domain.Cards
{
    public class DrawModifiersService
    {
        private const int DoubleStep = 2;

        private int pendingMultiplier;
        private int drawStartMultiplier;
        private int addedDuringDraw;

        public DrawModifiersService()
        {
            pendingMultiplier = 0;
            drawStartMultiplier = 0;
            addedDuringDraw = 0;
        }

        public void BeginDraw()
        {
            drawStartMultiplier = pendingMultiplier;
            addedDuringDraw = 0;
        }

        public int GetCurrentDrawMultiplier()
        {
            if (drawStartMultiplier <= 0)
            {
                return 1;
            }

            return drawStartMultiplier;
        }

        public void AddDoubleNextDrawMultiplier()
        {
            pendingMultiplier = SafeAdd(pendingMultiplier, DoubleStep);
            addedDuringDraw = SafeAdd(addedDuringDraw, DoubleStep);
        }

        public void CompleteDraw(bool usedMultiplierByResourceReward)
        {
            if (usedMultiplierByResourceReward && drawStartMultiplier > 0)
            {
                pendingMultiplier = addedDuringDraw;
            }

            drawStartMultiplier = 0;
            addedDuringDraw = 0;
        }

        private static int SafeAdd(int value, int increment)
        {
            if (value > int.MaxValue - increment)
            {
                return int.MaxValue;
            }

            return value + increment;
        }
    }
}
