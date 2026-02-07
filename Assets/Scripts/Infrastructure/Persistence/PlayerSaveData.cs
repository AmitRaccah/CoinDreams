namespace Game.Persistence
{
    [System.Serializable]
    public class PlayerSaveData
    {
        // Currency
        public int coins;

        // Energy
        public int currentEnergy;
        public int maxEnergy;
        public int regenIntervalSeconds;
        public long lastRegenUtcTicks;

        public PlayerSaveData()
        {
            //Default values
            coins = 0;

            currentEnergy = 5;
            maxEnergy = 10;
            regenIntervalSeconds = 300;
            lastRegenUtcTicks = 0;
        }
    }
}
