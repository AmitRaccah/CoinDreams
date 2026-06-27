namespace Game.Runtime.Cameras
{
    /// <summary>
    /// Single source of truth for "the pose the camera returns to when it goes
    /// back to the free City view". Captured the FIRST time the camera leaves
    /// City — by whichever flow leaves first (village build view or card draw) —
    /// and restored + cleared when it finally returns.
    ///
    /// Replaces the per-flow <c>lastCityPose</c> snapshots that each flow used to
    /// take independently: when one flow interrupted the other mid-transition,
    /// the second flow snapshotted a transient (mid-glide) pose and returned to
    /// it, causing a jump when the orbit rig then snapped to the real resting
    /// pose. With one shared capture (first-wins) both flows agree on the real
    /// city pose, which equals the orbit rig's frozen resting pose for the whole
    /// locked session — so the return lands exactly where the rig resumes.
    /// </summary>
    public interface ICameraCityPoseMemory
    {
        bool HasPose { get; }
        CameraPose Pose { get; }

        /// <summary>Store the city pose. First capture wins until <see cref="Clear"/>.</summary>
        void Capture(CameraPose pose);

        void Clear();
    }
}
