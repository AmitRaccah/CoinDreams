#if UNITY_EDITOR
using Game.Runtime.Steal;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    internal static class VoodooSceneSetup
    {
        private const string RootName = "VoodooSystem";

        [MenuItem("CoinDreams/Voodoo/Setup Voodoo Scene Hierarchy")]
        private static void SetupVoodooScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog(
                    "No Canvas",
                    "No Canvas found in the active scene. Open 01_Persistent and try again.",
                    "OK");
                return;
            }

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Already Set Up",
                    "VoodooSystem already exists. Delete and recreate?",
                    "Yes, recreate",
                    "Cancel");
                if (!replace) return;
                Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(800f, 500f);
            rootRect.anchoredPosition = Vector2.zero;

            GameObject voodooPanel = CreateChild(root, "VoodooPanel");
            FillRect(voodooPanel.GetComponent<RectTransform>());
            voodooPanel.SetActive(false);

            Color needleColor = new Color(0.6f, 0.6f, 0.6f);
            GameObject needle1 = CreateImage(voodooPanel, "Needle1", new Vector2(-200f,  60f), new Vector2(40f, 40f), needleColor);
            GameObject needle2 = CreateImage(voodooPanel, "Needle2", new Vector2(-220f,   0f), new Vector2(40f, 40f), needleColor);
            GameObject needle3 = CreateImage(voodooPanel, "Needle3", new Vector2(-200f, -60f), new Vector2(40f, 40f), needleColor);

            GameObject dollIntact = CreateImage(
                voodooPanel,
                "DollIntact",
                Vector2.zero,
                new Vector2(200f, 250f),
                new Color(0.9f, 0.8f, 0.3f));
            Button stabButton = dollIntact.AddComponent<Button>();
            stabButton.targetGraphic = dollIntact.GetComponent<Image>();
            stabButton.transition = Selectable.Transition.ColorTint;

            GameObject dollBroken = CreateImage(
                voodooPanel,
                "DollBroken",
                Vector2.zero,
                new Vector2(200f, 250f),
                new Color(0.7f, 0.2f, 0.2f));
            dollBroken.SetActive(false);

            GameObject stolenAmountText = CreateText(
                voodooPanel,
                "StolenAmountText",
                new Vector2(0f, 200f),
                new Vector2(300f, 60f),
                "+0",
                36f);
            stolenAmountText.SetActive(false);

            GameObject victimNameText = CreateText(
                voodooPanel,
                "VictimNameText",
                new Vector2(250f, 80f),
                new Vector2(200f, 40f),
                "VICTIM",
                24f);

            VoodooStealCoordinator    coordinator        = root.AddComponent<VoodooStealCoordinator>();
            VoodooStabInputBinder     inputBinder        = root.AddComponent<VoodooStabInputBinder>();
            VoodooDollPresenter       dollPresenter      = root.AddComponent<VoodooDollPresenter>();
            VictimPedestalPresenter   pedestalPresenter  = root.AddComponent<VictimPedestalPresenter>();
            CenterStageModeController stageController    = root.AddComponent<CenterStageModeController>();

            WireRef(inputBinder, "stabButton", stabButton);

            WireArray(dollPresenter, "needleVisuals", new Object[] { needle1, needle2, needle3 });
            WireRef(dollPresenter, "dollIntactRoot", dollIntact);
            WireRef(dollPresenter, "dollBrokenRoot", dollBroken);
            WireRef(dollPresenter, "stolenAmountText", stolenAmountText.GetComponent<TMP_Text>());

            WireRef(pedestalPresenter, "displayNameText", victimNameText.GetComponent<TMP_Text>());

            WireRef(stageController, "voodooPanelRoot", voodooPanel);

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            string message =
                "VoodooSystem created under " + canvas.name + " with all 5 scripts attached.\n\n" +
                "Wired automatically:\n" +
                "  - VoodooStabInputBinder.stabButton -> DollIntact button\n" +
                "  - VoodooDollPresenter (needles, doll, stolen amount)\n" +
                "  - VictimPedestalPresenter.displayNameText\n" +
                "  - CenterStageModeController.voodooPanelRoot\n\n" +
                "STILL TO DO:\n" +
                "  - Drag your existing crystal-ball + cards GameObject into\n" +
                "    CenterStageModeController.Draw Panel Root  (so it hides during voodoo)\n" +
                "  - Edit a CardDefinitionSO to use effect type LaunchSteal\n" +
                "  - Save the scene (Ctrl+S)\n\n" +
                "The placeholder visuals (yellow square = doll, gray squares = needles, etc.)\n" +
                "are stand-ins. Replace with real art whenever you want.";

            EditorUtility.DisplayDialog("Voodoo Setup Complete", message, "Got it");
            Debug.Log("[VoodooSceneSetup] " + RootName + " created. See dialog for the remaining manual steps.");
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static GameObject CreateImage(GameObject parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static GameObject CreateText(GameObject parent, string name, Vector2 anchoredPosition, Vector2 size, string text, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return go;
        }

        private static void FillRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void WireRef(Component target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[VoodooSceneSetup] Field '" + fieldName + "' not found on " + target.GetType().Name + ".");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void WireArray(Component target, string fieldName, Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[VoodooSceneSetup] Array field '" + fieldName + "' not found on " + target.GetType().Name + ".");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedProperties();
        }
    }
}
#endif
