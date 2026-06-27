namespace Game.Signals
{
    /// <summary>
    /// Fired by a UI source (typically a button on a side rail) asking the
    /// PanelNavigator to bring a specific panel forward and hide any other
    /// panel that's currently visible. PanelKey matches IPanel.PanelKey of
    /// the target panel — unknown keys are no-ops.
    /// </summary>
    public readonly struct PanelOpenRequestedSignal
    {
        public readonly string PanelKey;

        public PanelOpenRequestedSignal(string panelKey)
        {
            PanelKey = panelKey ?? string.Empty;
        }
    }
}
