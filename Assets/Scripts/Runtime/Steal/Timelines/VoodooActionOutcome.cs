#nullable enable

namespace Game.Runtime.Steal.Timelines
{
    /// <summary>
    /// Result of running <see cref="VoodooActionTimeline"/>. The timeline owns
    /// the server call + signal publish; this struct is the data the
    /// coordinator needs to update its own state (mutate the active session,
    /// decide whether to run the exit timeline). Keeping this as a readonly
    /// struct: zero GC and immutable communication across the timeline /
    /// coordinator boundary.
    /// </summary>
    public readonly struct VoodooActionOutcome
    {
        /// <summary>
        /// True iff the server accepted the stab (Success or VictimEmpty).
        /// When true, the coordinator should call session.RegisterStab.
        /// </summary>
        public readonly bool Resolved;

        public readonly int StolenAmount;
        public readonly int StabsRemaining;

        /// <summary>
        /// True when the doll just broke this stab — coordinator should run
        /// the exit timeline with dollBroken=true.
        /// </summary>
        public readonly bool DollBroken;

        /// <summary>
        /// True when the server reported the session is no longer usable
        /// (NotFound, Exhausted, Expired). Coordinator should run the exit
        /// timeline with dollBroken=false to clean up the UI mirror.
        /// </summary>
        public readonly bool SessionExpired;

        /// <summary>
        /// True when either branch above wants the session to end.
        /// </summary>
        public bool SessionShouldEnd => DollBroken || SessionExpired;

        private VoodooActionOutcome(bool resolved, int stolen, int remaining, bool dollBroken, bool sessionExpired)
        {
            Resolved = resolved;
            StolenAmount = stolen;
            StabsRemaining = remaining;
            DollBroken = dollBroken;
            SessionExpired = sessionExpired;
        }

        public static VoodooActionOutcome Stab(int stolen, int remaining, bool dollBroken)
            => new VoodooActionOutcome(resolved: true, stolen, remaining, dollBroken, sessionExpired: false);

        public static VoodooActionOutcome SessionGone()
            => new VoodooActionOutcome(resolved: false, stolen: 0, remaining: 0, dollBroken: false, sessionExpired: true);

        public static VoodooActionOutcome NoOp()
            => new VoodooActionOutcome(resolved: false, stolen: 0, remaining: 0, dollBroken: false, sessionExpired: false);
    }
}
