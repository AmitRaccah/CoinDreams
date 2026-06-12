using UnityEngine;

namespace Game.Runtime.Cameras
{
    public readonly struct CameraPose
    {
        public CameraPose(Vector3 position, Quaternion rotation, bool hasOrthographicSize, float orthographicSize)
        {
            Position = position;
            Rotation = rotation;
            HasOrthographicSize = hasOrthographicSize;
            OrthographicSize = orthographicSize;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool HasOrthographicSize { get; }
        public float OrthographicSize { get; }

        public static CameraPose FromTransform(Transform transform)
        {
            return new CameraPose(transform.position, transform.rotation, false, 0f);
        }

        public static CameraPose FromCamera(UnityEngine.Camera camera)
        {
            Transform cameraTransform = camera.transform;
            return new CameraPose(
                cameraTransform.position,
                cameraTransform.rotation,
                camera.orthographic,
                camera.orthographicSize);
        }
    }
}
