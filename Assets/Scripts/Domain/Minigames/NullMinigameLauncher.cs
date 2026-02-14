namespace Game.Domain.Minigames
{
    public sealed class NullMinigameLauncher : IMinigameLauncher
    {
        public static readonly NullMinigameLauncher Instance = new NullMinigameLauncher();

        private NullMinigameLauncher()
        {
        }

        public void Launch(string minigameId)
        {
            // Intentionally empty for current vertical slice.
        }
    }
}
