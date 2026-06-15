namespace Game.Domain.Steal
{
    public sealed class NullStealCardLauncher : IStealCardLauncher
    {
        public static readonly NullStealCardLauncher Instance = new NullStealCardLauncher();

        private NullStealCardLauncher()
        {
        }

        public void Launch(string triggerId)
        {
            // Intentionally empty for current vertical slice.
        }
    }
}
