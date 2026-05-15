namespace Game.Infrastructure.Persistence
{
    public sealed class AutosaveScheduler
    {
        private const float MinIntervalSeconds = 0.25f;

        private readonly float intervalSeconds;
        private bool dirty;
        private bool isSaving;
        private float nextSaveTime;

        public AutosaveScheduler(float intervalSeconds)
        {
            this.intervalSeconds = intervalSeconds < MinIntervalSeconds
                ? MinIntervalSeconds
                : intervalSeconds;
        }

        public float IntervalSeconds
        {
            get { return intervalSeconds; }
        }

        public bool IsDirty
        {
            get { return dirty; }
        }

        public bool ShouldSave(float currentTime)
        {
            return dirty && !isSaving && currentTime >= nextSaveTime;
        }

        public void MarkDirty(float currentTime)
        {
            if (dirty)
            {
                return;
            }

            dirty = true;
            nextSaveTime = currentTime + intervalSeconds;
        }

        public void BeginSave()
        {
            isSaving = true;
        }

        public void EndSave()
        {
            isSaving = false;
        }

        public void RecordSaveSuccess(int currentRevision, int savedRevision, float currentTime)
        {
            dirty = currentRevision > savedRevision;
            if (dirty)
            {
                nextSaveTime = currentTime + intervalSeconds;
            }
        }

        public void RecordSaveFailure(float currentTime)
        {
            nextSaveTime = currentTime + intervalSeconds;
        }

        public void ClearDirty(float currentTime)
        {
            dirty = false;
            nextSaveTime = currentTime + intervalSeconds;
        }
    }
}
