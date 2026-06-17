namespace Game.Composition.Signals
{
    public readonly struct StealCardTriggeredSignal
    {
        public readonly string TriggerId;

        /// <summary>
        /// The draw multiplier that was active when the steal card resolved
        /// (1, 2, 4, or 8). Captured at draw-time inside LaunchStealEffect and
        /// passed through to the voodoo session so the thief's gain per stab
        /// is amplified without changing the victim's loss.
        /// </summary>
        public readonly int Multiplier;

        public StealCardTriggeredSignal(string triggerId, int multiplier)
        {
            TriggerId = triggerId ?? string.Empty;
            Multiplier = multiplier > 0 ? multiplier : 1;
        }
    }
}
