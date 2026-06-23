namespace Game.Composition.Signals
{
    /// <summary>
    /// Published by the 3D doll presenter AFTER the per-stab Feel chain has
    /// fully settled. Subscribers can rely on this as the "point of truth"
    /// for committing the stab's side effects: coordinator releases the
    /// input gate, HUD sync ticks the coin balance, results screen counts
    /// the stab, etc.
    ///
    /// Why not just listen to <c>VoodooStabResolvedSignal</c>? That fires
    /// the instant the server replies — the animation is still mid-play,
    /// and committing now causes coins to pop in BEFORE the visual sells
    /// the steal. Mixing those two semantics into one signal also makes
    /// every subscriber re-decide "should I act now or wait for the
    /// animation". Keeping them separate keeps each subscriber single-
    /// purpose: server bridges read Resolved, gameplay-state mutators
    /// read this.
    /// </summary>
    public readonly struct VoodooStabAnimationCompletedSignal
    {
        public readonly string SessionId;
        public readonly int StolenAmount;
        public readonly bool IsDollBroken;

        public VoodooStabAnimationCompletedSignal(string sessionId, int stolenAmount, bool isDollBroken)
        {
            SessionId = sessionId ?? string.Empty;
            StolenAmount = stolenAmount;
            IsDollBroken = isDollBroken;
        }
    }
}
