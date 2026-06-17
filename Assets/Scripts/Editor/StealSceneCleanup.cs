#if UNITY_EDITOR
using Game.Runtime.Cards;
using Game.Runtime.Steal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    internal static class StealSceneCleanup
    {
        private const string VoodooSystemName = "VoodooSystem";
        private const string LogicGameObjectName = "VoodooLogic";

        [MenuItem("CoinDreams/Steal/Clean Steal Scene")]
        private static void CleanStealScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            if (!activeScene.name.Contains("Steal"))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Wrong Scene?",
                    "Active scene is '" + activeScene.name + "' — this menu is designed for the 0.1_Steal scene. Continue anyway?",
                    "Continue",
                    "Cancel");
                if (!proceed) return;
            }

            // 1) Wipe the existing VoodooSystem hierarchy (placeholder visuals).
            GameObject existing = GameObject.Find(VoodooSystemName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject existingLogic = GameObject.Find(LogicGameObjectName);
            if (existingLogic != null)
            {
                Undo.DestroyObjectImmediate(existingLogic);
            }

            // 2) Create the logic-only GameObject. No UI children, no rect
            //    transform — the two scripts are the entire payload. The
            //    DrawButtonRouter is the new mediator that turns Draw clicks
            //    into stab requests when a voodoo session is active; it has
            //    no SerializeFields so no wiring is needed here.
            GameObject logicGo = new GameObject(LogicGameObjectName);
            Undo.RegisterCreatedObjectUndo(logicGo, "Create " + LogicGameObjectName);

            Undo.AddComponent<VoodooStealCoordinator>(logicGo);
            Undo.AddComponent<DrawButtonRouter>(logicGo);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = logicGo;
            EditorGUIUtility.PingObject(logicGo);

            string message =
                "Steal scene cleaned:\n\n" +
                "  Removed: " + VoodooSystemName + " (entire placeholder hierarchy)\n" +
                "  Created: " + LogicGameObjectName + " (no UI)\n" +
                "    + VoodooStealCoordinator\n" +
                "    + DrawButtonRouter (mediates Draw clicks into stabs)\n\n" +
                "Click the Draw button to stab while a session is active.\n" +
                "Save the scene (Ctrl+S).\n\n" +
                "Note: a voodoo session still needs to start before stabs do anything.\n" +
                "For now, a draw that resolves into a LaunchSteal effect is the only path —\n" +
                "if this scene is loaded standalone, no stab will land. We will design\n" +
                "the session-start trigger for this scene next.";

            EditorUtility.DisplayDialog("Steal Scene Cleaned", message, "Got it");
            Debug.Log("[StealSceneCleanup] Scene cleaned. DrawButtonRouter now mediates Draw clicks into VoodooStabRequestedSignal.");
        }
    }
}
#endif
