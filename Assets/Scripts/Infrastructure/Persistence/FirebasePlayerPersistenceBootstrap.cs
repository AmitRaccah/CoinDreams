using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public static class FirebasePlayerPersistenceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeBootstrap()
        {
            FirebasePlayerPersistenceRuntime existing =
                Object.FindFirstObjectByType<FirebasePlayerPersistenceRuntime>();
            if (existing != null)
            {
                return;
            }

            GameObject runtimeObject = new GameObject("FirebasePlayerPersistenceRuntime");
            runtimeObject.AddComponent<FirebasePlayerPersistenceRuntime>();
            Object.DontDestroyOnLoad(runtimeObject);
        }
    }
}
