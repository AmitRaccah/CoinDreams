namespace Game.Runtime.Cameras
{
    /// <inheritdoc cref="ICameraCityPoseMemory"/>
    public sealed class CameraCityPoseMemory : ICameraCityPoseMemory
    {
        private CameraPose? pose;

        public bool HasPose
        {
            get { return pose.HasValue; }
        }

        public CameraPose Pose
        {
            get { return pose ?? default; }
        }

        public void Capture(CameraPose value)
        {
            // First-wins: only the first leave-from-City captures the real resting
            // pose; later (possibly transient) captures while already away are
            // ignored until Clear.
            if (pose.HasValue)
            {
                return;
            }

            pose = value;
        }

        public void Clear()
        {
            pose = null;
        }
    }
}
