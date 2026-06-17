#if UNITY_EDITOR
using Game.Runtime.Steal;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Adds a cube placeholder for the voodoo doll plus a Voodoo3DDollPresenter
    /// wired to drive it. Replace the cube mesh with real art whenever — the
    /// presenter only needs a GameObject root with renderers somewhere in its
    /// hierarchy.
    /// </summary>
    internal static class Voodoo3DDollSetup
    {
        private const string RigName = "VoodooDoll3D";
        private const string MeshChildName = "DollMesh";
        private const string NameLabelChildName = "VictimName";

        [MenuItem("CoinDreams/Steal/Add 3D Doll Placeholder")]
        private static void AddDollPlaceholder()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            GameObject existing = GameObject.Find(RigName);
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Already Set Up",
                    "'" + RigName + "' already exists in the scene. Delete and recreate?",
                    "Yes, recreate",
                    "Cancel");
                if (!replace) return;
                Object.DestroyImmediate(existing);
            }

            GameObject rig = new GameObject(RigName);
            Undo.RegisterCreatedObjectUndo(rig, "Create " + RigName);
            rig.transform.position = new Vector3(0f, 1f, 0f);

            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = MeshChildName;
            mesh.transform.SetParent(rig.transform, false);
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
            // The cube's BoxCollider is unwanted on a UI-style placeholder.
            Collider col = mesh.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            Voodoo3DDollPresenter presenter = Undo.AddComponent<Voodoo3DDollPresenter>(rig);

            SerializedObject so = new SerializedObject(presenter);
            SerializedProperty rootProp = so.FindProperty("dollRoot");
            if (rootProp != null)
            {
                rootProp.objectReferenceValue = mesh;
                so.ApplyModifiedProperties();
            }

            // Start hidden — the presenter also enforces this in Awake but
            // setting it now keeps the Editor preview honest.
            mesh.SetActive(false);

            // 3D world-space TMP label that floats above the cube. The label
            // GameObject is what the presenter toggles on/off — the cube
            // disappears when broken but the label tracks the session, not
            // the doll, so it's a sibling under the rig (not a child of mesh).
            GameObject labelGo = new GameObject(NameLabelChildName);
            labelGo.transform.SetParent(rig.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            labelGo.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            TextMeshPro nameLabel = labelGo.AddComponent<TextMeshPro>();
            nameLabel.text = "VICTIM";
            nameLabel.fontSize = 24f;
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.color = Color.white;
            labelGo.SetActive(false);

            VoodooVictimNamePresenter namePresenter = Undo.AddComponent<VoodooVictimNamePresenter>(rig);
            SerializedObject nameSo = new SerializedObject(namePresenter);
            SerializedProperty nameProp = nameSo.FindProperty("nameText");
            if (nameProp != null)
            {
                nameProp.objectReferenceValue = nameLabel;
                nameSo.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Selection.activeGameObject = rig;
            EditorGUIUtility.PingObject(rig);

            string message =
                "Created '" + RigName + "' at (0, 1, 0) with:\n" +
                "  - '" + MeshChildName + "' (cube, hidden until session starts)\n" +
                "  - '" + NameLabelChildName + "' (3D TMP_Text label, shows victim name)\n\n" +
                "Components wired:\n" +
                "  - Voodoo3DDollPresenter -> cube (color flash, broken state, show/hide)\n" +
                "  - VoodooVictimNamePresenter -> label (text + show/hide)\n\n" +
                "Move/rotate/scale the rig to wherever the doll should appear.\n" +
                "Swap the cube mesh and the TMP label for real art whenever.\n\n" +
                "Save the scene (Ctrl+S).";

            EditorUtility.DisplayDialog("Voodoo Doll Placeholder Created", message, "Got it");
            Debug.Log("[Voodoo3DDollSetup] '" + RigName + "' created with cube placeholder.");
        }
    }
}
#endif
