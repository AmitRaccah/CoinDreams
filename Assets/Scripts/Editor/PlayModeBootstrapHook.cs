#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    [InitializeOnLoad]
    internal static class PlayModeBootstrapHook
    {
        private const string BootScenePath = "Assets/Scenes/00_Boot.unity";
        private const string RestoreKey = "CoinDreams_PlayModeBootstrap_RestoreScene";
        private const string EnabledKey = "CoinDreams_PlayModeBootstrap_Enabled";

        static PlayModeBootstrapHook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("CoinDreams/Bootstrap/Enable Auto-Boot On Play")]
        private static void EnableAutoBoot()
        {
            EditorPrefs.SetBool(EnabledKey, true);
            Debug.Log("[Bootstrap] Auto-boot on Play: ENABLED. Pressing Play from any scene will switch to 00_Boot.");
        }

        [MenuItem("CoinDreams/Bootstrap/Disable Auto-Boot On Play")]
        private static void DisableAutoBoot()
        {
            EditorPrefs.SetBool(EnabledKey, false);
            Debug.Log("[Bootstrap] Auto-boot on Play: DISABLED. Play will start from the currently open scene.");
        }

        [MenuItem("CoinDreams/Bootstrap/Open Boot Scene")]
        private static void OpenBootScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(BootScenePath);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!EditorPrefs.GetBool(EnabledKey, defaultValue: true)) return;

            if (state == PlayModeStateChange.ExitingEditMode)
            {
                HandleExitingEditMode();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                HandleEnteredEditMode();
            }
        }

        private static void HandleExitingEditMode()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == BootScenePath) return; // already on boot

            if (!System.IO.File.Exists(BootScenePath))
            {
                Debug.LogWarning("[Bootstrap] " + BootScenePath + " does not exist yet. Skipping auto-boot.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorApplication.isPlaying = false;
                return;
            }

            EditorPrefs.SetString(RestoreKey, active.path);
            EditorSceneManager.OpenScene(BootScenePath);
        }

        private static void HandleEnteredEditMode()
        {
            if (!EditorPrefs.HasKey(RestoreKey)) return;
            string path = EditorPrefs.GetString(RestoreKey);
            EditorPrefs.DeleteKey(RestoreKey);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
            EditorSceneManager.OpenScene(path);
        }
    }
}
#endif
