#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// One-shot cleanup: select prefab assets in Project, run the menu item,
    /// and every "Missing Script" component on the selected prefabs and their
    /// children is stripped. Saves the assets at the end so the change sticks
    /// in version control.
    /// </summary>
    internal static class MissingScriptCleanup
    {
        [MenuItem("CoinDreams/Cleanup/Remove Missing Scripts From Selected Prefabs")]
        private static void Run()
        {
            Object[] selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Missing Script Cleanup",
                    "Select one or more prefab assets in the Project window first.",
                    "OK");
                return;
            }

            int totalRemoved = 0;
            int prefabsTouched = 0;

            for (int i = 0; i < selected.Length; i++)
            {
                Object obj = selected[i];
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                {
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                int removed = CleanRecursive(root);
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    totalRemoved += removed;
                    prefabsTouched++;
                    Debug.Log("[MissingScriptCleanup] Removed " + removed
                        + " missing script(s) from '" + path + "'.");
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            string summary = totalRemoved == 0
                ? "No missing scripts found in the selected prefabs."
                : "Removed " + totalRemoved + " missing script(s) across "
                  + prefabsTouched + " prefab(s).";
            EditorUtility.DisplayDialog("Missing Script Cleanup", summary, "OK");
            AssetDatabase.Refresh();
        }

        private static int CleanRecursive(GameObject go)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                removed += CleanRecursive(go.transform.GetChild(i).gameObject);
            }
            return removed;
        }
    }
}
#endif
