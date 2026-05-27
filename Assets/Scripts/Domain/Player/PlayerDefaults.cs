#nullable enable

namespace Game.Domain.Player
{
    /// <summary>
    /// Single source of truth for placeholder player identifiers used before remote auth lands.
    /// </summary>
    public static class PlayerDefaults
    {
        public const string PlaceholderPlayerId = "local_player";
    }
}
