namespace Game.Runtime.UI.Context
{
    /// <summary>
    /// Constants for tags published into <see cref="IUiContext"/>. Use these
    /// in <see cref="UiTaggedVisibility"/> arrays instead of typing magic
    /// strings — typos in the SerializeField won't survive a refactor here.
    /// Per-panel tags are dynamic ("panel-buildings", "panel-attack") and
    /// follow <see cref="PanelKeyPrefix"/> + the panel's key.
    /// </summary>
    public static class UiTags
    {
        // ─── Panel ───
        /// <summary>Any panel currently shown via PanelNavigator.</summary>
        public const string PanelOpen = "panel-open";
        /// <summary>Prefix used by PanelTagsPublisher to emit "panel-{key}".</summary>
        public const string PanelKeyPrefix = "panel-";

        // ─── Voodoo / Steal ───
        /// <summary>VoodooStealCoordinator has an active session.</summary>
        public const string StealSession = "steal-session";

        // ─── Camera ───
        /// <summary>Camera mode currently == City.</summary>
        public const string CameraCity = "camera-city";
        /// <summary>Camera mode currently == Board.</summary>
        public const string CameraBoard = "camera-board";
        /// <summary>Camera mode currently == Transitioning.</summary>
        public const string CameraTransitioning = "camera-transitioning";
    }
}
