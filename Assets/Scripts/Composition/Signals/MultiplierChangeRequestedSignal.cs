namespace Game.Composition.Signals
{
    public readonly struct MultiplierChangeRequestedSignal
    {
        public readonly int Multiplier;

        public MultiplierChangeRequestedSignal(int multiplier)
        {
            Multiplier = multiplier;
        }
    }
}
