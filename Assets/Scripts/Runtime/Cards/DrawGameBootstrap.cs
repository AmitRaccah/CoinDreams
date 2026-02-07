using UnityEngine;

namespace Game.Runtime.Cards
{
    public static class DrawGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePresenterExists()
        {
            DrawGamePresenter existingPresenter = Object.FindFirstObjectByType<DrawGamePresenter>();
            if (existingPresenter != null)
            {
                return;
            }

            GameObject bootstrapObject = new GameObject("DrawGamePresenter_Auto");
            bootstrapObject.AddComponent<DrawGamePresenter>();

            Debug.LogWarning("DrawGamePresenter was missing in scene. Created fallback presenter automatically.");
        }
    }
}
