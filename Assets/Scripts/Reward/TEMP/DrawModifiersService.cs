namespace Game.Services.Cards
{
    public class DrawModifiersService
    {
        private float nextDrawMultiplier = 1f;

        public float ConsumeNextDrawMultiplier()
        {
            float value = nextDrawMultiplier;
            nextDrawMultiplier = 1f;
            return value;
        }

        public void SetNextDrawMultiplier(float multiplier)
        {
            if (multiplier <= 0f) multiplier = 1f;
            nextDrawMultiplier = multiplier;
        }
    }
}