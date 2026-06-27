namespace Game.Signals
{
    public readonly struct VoodooStabRequestedSignal
    {
        public readonly string SessionId;

        public VoodooStabRequestedSignal(string sessionId)
        {
            SessionId = sessionId ?? string.Empty;
        }
    }
}
