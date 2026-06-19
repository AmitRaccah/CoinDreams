#if UNITY_EDITOR
#nullable enable

using System.Text;
using Game.Runtime.UI.Buildings;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Wires the UpgradeObject prefab at <c>Assets/Prefabs/UpgradeObject.prefab</c>
    /// in one click: adds <see cref="UpgradeObjectView"/> to the root and
    /// resolves its SerializeFields by walking the prefab's children. Names
    /// expected (case-sensitive, deep search):
    ///
    ///   UpgradeObject (root)
    ///   ├── Image                ← buildingImage
    ///   ├── Progress             ← indicatorContainer (HorizontalLayoutGroup)
    ///   │   └── [any Image children become the pre-baked indicators]
    ///   └── Button               ← upgradeButton
    ///       └── Text (TMP)       ← costText
    ///
    /// The tool is idempotent. Fields already wired by hand are preserved
    /// (the user's drag-in always wins).
    /// </summary>
    internal static class UpgradeObjectPrefabSetupTool
    {
        private const string PrefabPath = "Assets/Prefabs/UpgradeObject.prefab";

        [MenuItem("CoinDreams/UI/Setup UpgradeObject Prefab")]
        private static void Run()
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "UpgradeObject Setup",
                    "Could not find " + PrefabPath + ". Adjust PrefabPath constant or move the prefab.",
                    "OK");
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                StringBuilder report = new StringBuilder();

                UpgradeObjectView view = contents.GetComponent<UpgradeObjectView>();
                if (view == null)
                {
                    view = contents.AddComponent<UpgradeObjectView>();
                    report.AppendLine("+ Added UpgradeObjectView to prefab root.");
                }
                else
                {
                    report.AppendLine("- UpgradeObjectView already present on prefab root.");
                }

                Transform root = contents.transform;

                // Direct-child "Image" is the building portrait.
                Transform? buildingImageT = FindDirectChild(root, "Image");
                if (buildingImageT != null)
                {
                    Image img = buildingImageT.GetComponent<Image>();
                    if (img != null) SetObjectField(view, "buildingImage", img, report);
                }
                else
                {
                    report.AppendLine("WARNING: direct child 'Image' not found.");
                }

                // "Progress" hosts the level indicators.
                Transform? progressT = FindDeep(root, "Progress");
                if (progressT != null)
                {
                    SetObjectField(view, "indicatorContainer", progressT, report);
                }
                else
                {
                    report.AppendLine("WARNING: child 'Progress' not found — indicators won't render.");
                }

                // "Button" is the upgrade button.
                Transform? buttonT = FindDeep(root, "Button");
                if (buttonT != null)
                {
                    Button btn = buttonT.GetComponent<Button>();
                    if (btn != null) SetObjectField(view, "upgradeButton", btn, report);

                    // Cost text is the TMP_Text inside the button (any child).
                    TMP_Text costText = buttonT.GetComponentInChildren<TMP_Text>(includeInactive: true);
                    if (costText != null) SetObjectField(view, "costText", costText, report);
                    else report.AppendLine("WARNING: no TMP_Text inside Button — costText left empty.");
                }
                else
                {
                    report.AppendLine("WARNING: child 'Button' not found — upgrade button left empty.");
                }

                EditorUtility.SetDirty(view);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);

                report.AppendLine();
                report.AppendLine("Still need (manual drag):");
                report.AppendLine(" • indicatorPrefab — for level counts beyond the prefab-baked indicators");
                report.AppendLine(" • filledIndicatorSprite / emptyIndicatorSprite — paints");
                report.AppendLine(" • coinIconGroup — only if you want the coin icon hidden at MAX");
                report.AppendLine();
                report.AppendLine("Then re-run 'Setup Buildings Panel' so the Presenter wires this prefab.");

                EditorUtility.DisplayDialog("UpgradeObject Setup", report.ToString(), "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Transform? FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) return c;
            }
            return null;
        }

        private static Transform? FindDeep(Transform parent, string name)
        {
            Transform? direct = FindDirectChild(parent, name);
            if (direct != null) return direct;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform? deep = FindDeep(parent.GetChild(i), name);
                if (deep != null) return deep;
            }
            return null;
        }

        private static void SetObjectField(Component target, string fieldName, UnityEngine.Object value, StringBuilder report)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            if (prop.objectReferenceValue == value) return;
            if (prop.objectReferenceValue != null && prop.objectReferenceValue != value)
            {
                report.AppendLine("- " + fieldName + " already wired — left untouched.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            report.AppendLine("+ Wired " + fieldName + " → '" + value.name + "'.");
        }
    }
}
#endif
