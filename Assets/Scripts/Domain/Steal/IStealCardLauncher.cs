namespace Game.Domain.Steal
{
    public interface IStealCardLauncher
    {
        /// <summary>
        /// Triggered by LaunchStealEffect when a steal card resolves. The
        /// multiplier reflects the draw multiplier active at that moment and
        /// MUST be carried through to the voodoo session — the thief's coins
        /// gained per stab are amplified by it (the victim's loss is not).
        /// </summary>
        void Launch(string triggerId, int multiplier);
    }
}
