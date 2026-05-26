namespace Game.Runtime.Scenes
{
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;

    public readonly struct SceneHandle
    {
        public readonly AsyncOperationHandle<SceneInstance> Operation;
        public readonly string AddressableKey;

        public SceneHandle(AsyncOperationHandle<SceneInstance> op, string addressableKey)
        {
            Operation = op;
            AddressableKey = addressableKey ?? string.Empty;
        }

        public bool IsValid
        {
            get { return Operation.IsValid(); }
        }

        public Scene Scene
        {
            get { return Operation.IsValid() ? Operation.Result.Scene : default(Scene); }
        }
    }
}
