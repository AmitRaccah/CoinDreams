namespace Game.Domain.Shields
{
    /// <summary>
    /// Default config values for the shield system. The runtime values live
    /// in <see cref="Game.Domain.Player.PlayerProfileSnapshot"/> (server is
    /// authoritative). These defaults are only used to seed brand-new
    /// profiles before the server has stamped them.
    /// </summary>
    public static class ShieldDefaults
    {
        /// <summary>
        /// How many shields a brand-new player starts holding. Always zero —
        /// shields are earned through draws, not granted at signup.
        /// </summary>
        public const int DefaultStartingShields = 0;

        /// <summary>
        /// How many shields a player can hold at once by default. Overflow
        /// past this cap when a Shield card is drawn at high multiplier is
        /// refunded as energy by the draw engine.
        /// </summary>
        public const int DefaultMaxShields = 3;
    }
}
