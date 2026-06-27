namespace Game.Signals
{
    /// <summary>
    /// Fired by <c>PanelNavigator</c> after every successful Show / Hide so
    /// other UI can react — typically a background-visibility controller
    /// that hides the side rail + action panel while a modal is up. The
    /// payload carries enough for both "any panel open" toggles and
    /// per-key conditional logic.
    /// </summary>
    public readonly struct PanelVisibilityChangedSignal
    {
        /// <summary>True iff a panel is currently shown.</summary>
        public readonly bool IsAnyPanelOpen;

        /// <summary>
        /// Key of the currently-open panel (empty when none).
        /// </summary>
        public readonly string CurrentPanelKey;

        public PanelVisibilityChangedSignal(bool isAnyPanelOpen, string currentPanelKey)
        {
            IsAnyPanelOpen = isAnyPanelOpen;
            CurrentPanelKey = currentPanelKey ?? string.Empty;
        }
    }
}
