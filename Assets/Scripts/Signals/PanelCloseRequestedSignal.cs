namespace Game.Signals
{
    /// <summary>
    /// Fired by a panel's own close button (or any source) asking the
    /// PanelNavigator to hide whichever panel is currently visible. The
    /// signal carries no payload — the navigator owns "which is current".
    /// </summary>
    public readonly struct PanelCloseRequestedSignal
    {
    }
}
