#if UNITY_EDITOR
using Game.Runtime.Cards;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    internal static class MultiplierButtonSetup
    {
        private const string ButtonName = "MultiplierButton";

        [MenuItem("CoinDreams/Cards/Wire Multiplier Button")]
        private static void WireMultiplierButton()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            GameObject buttonGameObject = GameObject.Find(ButtonName);
            if (buttonGameObject == null)
            {
                EditorUtility.DisplayDialog(
                    "Not Found",
                    "No GameObject named '" + ButtonName + "' found in the active scene.\n\n" +
                    "Make sure the button exists in the Hierarchy and is named exactly '" + ButtonName + "'.",
                    "OK");
                return;
            }

            Button buttonComponent = buttonGameObject.GetComponent<Button>();
            if (buttonComponent == null)
            {
                EditorUtility.DisplayDialog(
                    "No Button",
                    "'" + ButtonName + "' has no UnityEngine.UI.Button component.\n" +
                    "Add a Button component first, then re-run this menu.",
                    "OK");
                return;
            }

            TMP_Text labelComponent = buttonGameObject.GetComponentInChildren<TMP_Text>(true);
            if (labelComponent == null)
            {
                EditorUtility.DisplayDialog(
                    "No TMP_Text",
                    "'" + ButtonName + "' has no child with a TMP_Text component.\n" +
                    "Add a child with TextMeshProUGUI (UI > Text - TextMeshPro), then re-run this menu.",
                    "OK");
                return;
            }

            DrawMultiplierBinder binder = buttonGameObject.GetComponent<DrawMultiplierBinder>();
            if (binder == null)
            {
                binder = Undo.AddComponent<DrawMultiplierBinder>(buttonGameObject);
            }

            SerializedObject so = new SerializedObject(binder);

            SerializedProperty buttonProp = so.FindProperty("multiplierButton");
            if (buttonProp == null)
            {
                EditorUtility.DisplayDialog(
                    "Field Missing",
                    "DrawMultiplierBinder does not expose 'multiplierButton'. Did the field get renamed?",
                    "OK");
                return;
            }
            buttonProp.objectReferenceValue = buttonComponent;

            SerializedProperty labelProp = so.FindProperty("multiplierLabel");
            if (labelProp == null)
            {
                EditorUtility.DisplayDialog(
                    "Field Missing",
                    "DrawMultiplierBinder does not expose 'multiplierLabel'. Did the field get renamed?",
                    "OK");
                return;
            }
            labelProp.objectReferenceValue = labelComponent;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(binder);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = buttonGameObject;
            EditorGUIUtility.PingObject(buttonGameObject);

            string message =
                "DrawMultiplierBinder is now wired on '" + ButtonName + "':\n\n" +
                "  Multiplier Button -> " + buttonComponent.name + " (Button)\n" +
                "  Multiplier Label  -> " + labelComponent.name + " (" + labelComponent.GetType().Name + ")\n\n" +
                "Save the scene (Ctrl+S), press Play, and click the button to cycle x1 -> x2 -> x4 -> x8.";

            EditorUtility.DisplayDialog("Multiplier Wired", message, "Got it");
            Debug.Log("[MultiplierButtonSetup] Wired DrawMultiplierBinder on " + ButtonName + ".");
        }
    }
}
#endif
