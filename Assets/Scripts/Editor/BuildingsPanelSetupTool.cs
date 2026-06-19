#if UNITY_EDITOR
#nullable enable

using System.Collections.Generic;
using System.Text;
using Game.Config.Village;
using Game.Runtime.UI.Buildings;
using Game.Runtime.UI.Panels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// One-click wiring for the Buildings panel. Expects this hierarchy on
    /// the selected GameObject (names are case-sensitive, position-agnostic):
    ///
    ///   BuildingsPanel              ← selected
    ///   ├── PanelHolder             ← becomes BuildingsPanelPresenter.contentRoot
    ///   │   └── (UpgradeObject instances spawn here at runtime)
    ///   └── CloseHolder
    ///       └── Close (Button)      ← PanelCloseButton attached here
    ///
    /// The tool is idempotent — re-running it doesn't duplicate components
    /// or overwrite asset references the user already set by hand.
    /// </summary>
    internal static class BuildingsPanelSetupTool
    {
        private const string PanelHolderName = "PanelHolder";
        private const string CloseHolderName = "CloseHolder";
        private const string CloseButtonName = "Close";

        [MenuItem("CoinDreams/UI/Setup Buildings Panel")]
        private static void Run()
        {
            GameObject? selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Buildings Panel Setup",
                    "Select the BuildingsPanel GameObject in the Hierarchy first.",
                    "OK");
                return;
            }

            StringBuilder report = new StringBuilder();

            BuildingsPanel panel = AddOrGet<BuildingsPanel>(selected, report);
            SetStringField(panel, "panelKey", "buildings", report);

            BuildingsPanelPresenter presenter = AddOrGet<BuildingsPanelPresenter>(selected, report);

            Transform? panelHolder = FindChildByName(selected.transform, PanelHolderName);
            if (panelHolder != null)
            {
                SetObjectField(presenter, "contentRoot", panelHolder, report);
            }
            else
            {
                report.AppendLine("WARNING: child '" + PanelHolderName + "' not found — contentRoot left unwired.");
            }

            UpgradeObjectView? prefab = LoadFirstAsset<UpgradeObjectView>(
                "Assets/Prefabs/UpgradeObject.prefab", "t:Prefab UpgradeObject");
            if (prefab != null)
            {
                SetObjectField(presenter, "upgradeObjectPrefab", prefab, report);
            }
            else
            {
                report.AppendLine("WARNING: UpgradeObject prefab not found (looked for Assets/Prefabs/UpgradeObject.prefab " +
                    "with an UpgradeObjectView component). Drag it in manually.");
            }

            VillageDefinitionSO? villageDef = LoadFirstAsset<VillageDefinitionSO>(null, "t:VillageDefinitionSO");
            if (villageDef != null)
            {
                SetObjectField(presenter, "villageDefinition", villageDef, report);
            }
            else
            {
                report.AppendLine("WARNING: no VillageDefinitionSO asset found. Drag it in manually.");
            }

            Transform? closeHolder = FindChildByName(selected.transform, CloseHolderName);
            if (closeHolder != null)
            {
                Transform? closeChild = FindChildByName(closeHolder, CloseButtonName);
                if (closeChild == null)
                {
                    report.AppendLine("WARNING: '" + CloseHolderName + "/" + CloseButtonName + "' not found.");
                }
                else if (closeChild.GetComponent<Button>() == null)
                {
                    report.AppendLine("WARNING: '" + CloseButtonName + "' has no Button component; PanelCloseButton not added.");
                }
                else
                {
                    AddOrGet<PanelCloseButton>(closeChild.gameObject, report);
                }
            }
            else
            {
                report.AppendLine("WARNING: child '" + CloseHolderName + "' not found — close button not wired.");
            }

            EditorUtility.SetDirty(selected);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(presenter);

            string summary = report.Length == 0 ? "All wired cleanly." : report.ToString();
            EditorUtility.DisplayDialog("Buildings Panel Setup", summary, "OK");
        }

        private static T AddOrGet<T>(GameObject host, StringBuilder report) where T : Component
        {
            T existing = host.GetComponent<T>();
            if (existing != null)
            {
                report.AppendLine("- " + typeof(T).Name + " already present on '" + host.name + "'.");
                return existing;
            }
            T added = Undo.AddComponent<T>(host);
            report.AppendLine("+ Added " + typeof(T).Name + " to '" + host.name + "'.");
            return added;
        }

        // Recursive name search — direct child first (fast path), then deep.
        private static Transform? FindChildByName(Transform parent, string name)
        {
            Transform direct = parent.Find(name);
            if (direct != null) return direct;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform deep = FindChildByName(parent.GetChild(i), name);
                if (deep != null) return deep;
            }
            return null;
        }

        // Loads the first asset of type T. Tries the explicit path first
        // (cheap, deterministic); falls back to AssetDatabase search.
        private static T? LoadFirstAsset<T>(string? explicitPath, string filter) where T : UnityEngine.Object
        {
            if (!string.IsNullOrEmpty(explicitPath))
            {
                T direct = AssetDatabase.LoadAssetAtPath<T>(explicitPath);
                if (direct != null) return direct;
            }

            string[] guids = AssetDatabase.FindAssets(filter);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T candidate = AssetDatabase.LoadAssetAtPath<T>(path);
                if (candidate != null) return candidate;
            }
            return null;
        }

        // Only writes if the slot is currently empty — preserves whatever
        // the user dragged in by hand previously.
        private static void SetObjectField(Component target, string fieldName, UnityEngine.Object value, StringBuilder report)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            if (prop.objectReferenceValue == value) return;
            if (prop.objectReferenceValue != null && prop.objectReferenceValue != value)
            {
                report.AppendLine("- " + target.GetType().Name + "." + fieldName
                    + " already wired to '" + prop.objectReferenceValue.name + "' — left untouched.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            report.AppendLine("+ Wired " + target.GetType().Name + "." + fieldName
                + " → '" + value.name + "'.");
        }

        private static void SetStringField(Component target, string fieldName, string value, StringBuilder report)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;
            if (prop.stringValue == value) return;
            prop.stringValue = value;
            so.ApplyModifiedProperties();
            report.AppendLine("+ Set " + target.GetType().Name + "." + fieldName + " = '" + value + "'.");
        }
    }
}
#endif
