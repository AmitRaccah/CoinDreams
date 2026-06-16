namespace Game.Composition.Signals
{
    public readonly struct StealCardTriggeredSignal
    {
        public readonly string TriggerId;

        public StealCardTriggeredSignal(string triggerId)
        {
            TriggerId = triggerId ?? string.Empty;
        }
    }
}
