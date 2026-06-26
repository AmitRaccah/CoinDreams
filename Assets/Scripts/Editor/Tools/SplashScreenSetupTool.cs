#if UNITY_EDITOR
namespace Game.Editor.Tools
{
    using Game.Runtime.Bootstrap.UI;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;

    /// <summary>
    /// One-click setup for the 3-phase splash screen on 00_Boot.unity:
    ///   1. Black background + logo overlay
    ///   2. Wide loading background art + progress bar reveal
    ///   3. Final fade to gameplay
    ///
    /// Restructures the BootSplash hierarchy idempotently — running the
    /// tool twice keeps the same nodes, just re-wires them. Designed so
    /// the artist can drop in sprites afterwards without touching the
    /// hierarchy.
    /// </summary>
    public static class SplashScreenSetupTool
    {
        private const int CanvasSortOrder = 32767;
        private const float ProgressGroupWidth = 600f;
        private const float ProgressGroupHeight = 200f;
        private const string BlackBackgroundName = "BlackBackground";
        private const string LoadingBackgroundName = "LoadingBackground";
        private const string ProgressBarBackgroundName = "ProgressBarBackground";
        private const string ProgressGroupName = "ProgressUIGroup";

        [MenuItem("Tools/CoinDreams/Bootstrap/Setup Splash Layers")]
        public static void SetupSplashLayers()
        {
            BootSplashView view = Object.FindFirstObjectByType<BootSplashView>();
            if (view == null)
            {
                EditorUtility.DisplayDialog(
                    "Splash Setup",
                    "No BootSplashView found in any open scene. Open 00_Boot.unity first.",
                    "OK");
                return;
            }

            Transform splashRoot = view.transform;
            RectTransform splashRootRect = splashRoot as RectTransform;
            if (splashRootRect == null)
            {
                EditorUtility.DisplayDialog(
                    "Splash Setup",
                    "BootSplashView must live on a RectTransform.",
                    "OK");
                return;
            }

            Canvas canvas = splashRoot.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog(
                    "Splash Setup",
                    "BootSplashView is not under a Canvas. Cannot proceed.",
                    "OK");
                return;
            }

            Undo.SetCurrentGroupName("Setup Splash Layers");
            int undoGroup = Undo.GetCurrentGroup();

            // 1. Canvas sort order to the top so the splash covers
            //    everything that gets additively loaded behind it.
            Undo.RecordObject(canvas, "Set Canvas Sort Order");
            canvas.sortingOrder = CanvasSortOrder;
            EditorUtility.SetDirty(canvas);
            StretchToParent(splashRootRect);

            // 2. Root CanvasGroup on BootSplash → drives phase-3 fade.
            CanvasGroup rootGroup = splashRoot.GetComponent<CanvasGroup>();
            if (rootGroup == null)
            {
                rootGroup = Undo.AddComponent<CanvasGroup>(splashRoot.gameObject);
            }
            rootGroup.alpha = 1f;

            // 3. BlackBackground — opaque black, full screen, at the back
            //    of the splash sibling order so everything draws on top.
            Image blackBg = FindOrCreateChildImage(splashRoot, BlackBackgroundName, 0);
            blackBg.color = Color.black;
            blackBg.raycastTarget = false;
            blackBg.sprite = null;
            StretchToParent(blackBg.rectTransform);

            // 4. LoadingBackground — wide intro art that COVERS the screen.
            //    Image.preserveAspect implements "fit" (letterbox); we want
            //    "cover" (fill the screen, crop overflow). Unity ships
            //    exactly that as an AspectRatioFitter in EnvelopeParent
            //    mode — zero custom code required.
            Image loadingBg = FindOrCreateChildImage(splashRoot, LoadingBackgroundName, 1);
            loadingBg.color = Color.white;
            loadingBg.raycastTarget = false;
            loadingBg.preserveAspect = false; // AspectRatioFitter handles aspect
            StretchToParent(loadingBg.rectTransform);
            // Defensive: clear any orphaned components left over from
            // the previous custom-script attempt — keeps the scene clean
            // if the tool is re-run after the migration.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(loadingBg.gameObject);
            AspectRatioFitter aspectFitter = loadingBg.GetComponent<AspectRatioFitter>();
            if (aspectFitter == null)
            {
                aspectFitter = Undo.AddComponent<AspectRatioFitter>(loadingBg.gameObject);
            }
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            if (loadingBg.sprite != null && loadingBg.sprite.rect.height > 0f)
            {
                aspectFitter.aspectRatio =
                    loadingBg.sprite.rect.width / loadingBg.sprite.rect.height;
            }
            CanvasGroup loadingBgGroup = loadingBg.GetComponent<CanvasGroup>();
            if (loadingBgGroup == null)
            {
                loadingBgGroup = Undo.AddComponent<CanvasGroup>(loadingBg.gameObject);
            }
            loadingBgGroup.alpha = 0f;

            // 5. ProgressUIGroup wrapper — fades the bar + status text
            //    together. Existing progress bar and status text get
            //    re-parented into this group.
            Transform progressGroupTransform = splashRoot.Find(ProgressGroupName);
            CanvasGroup progressGroup;
            if (progressGroupTransform == null)
            {
                GameObject progressGo = new GameObject(ProgressGroupName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(progressGo, "Create ProgressUIGroup");
                progressGo.layer = splashRoot.gameObject.layer;
                Undo.SetTransformParent(progressGo.transform, splashRoot, "Parent ProgressUIGroup");
                progressGroupTransform = progressGo.transform;
                progressGroup = Undo.AddComponent<CanvasGroup>(progressGo);
            }
            else
            {
                progressGroup = progressGroupTransform.GetComponent<CanvasGroup>();
                if (progressGroup == null)
                {
                    progressGroup = Undo.AddComponent<CanvasGroup>(progressGroupTransform.gameObject);
                }
            }
            RectTransform progressGroupRect = progressGroupTransform as RectTransform;
            if (progressGroupRect == null)
            {
                EditorUtility.DisplayDialog(
                    "Splash Setup",
                    "ProgressUIGroup must be a RectTransform.",
                    "OK");
                return;
            }

            CenterFixedSize(progressGroupRect, ProgressGroupWidth, ProgressGroupHeight);
            progressGroup.alpha = 0f;

            // Re-parent the existing progress bar and status text under
            // the group so they fade together. ReadView via SerializedObject
            // so we don't depend on public getters.
            SerializedObject viewObj = new SerializedObject(view);
            SerializedProperty progressBarProp = viewObj.FindProperty("progressBar");
            SerializedProperty statusTextProp = viewObj.FindProperty("statusText");
            SerializedProperty logoImageProp = viewObj.FindProperty("logoImage");

            Transform existingProgressBackground = splashRoot.Find(ProgressBarBackgroundName);
            if (existingProgressBackground != null)
            {
                ReparentToGroup(existingProgressBackground, progressGroupTransform);
            }

            Image existingProgressBar = progressBarProp != null ? progressBarProp.objectReferenceValue as Image : null;
            if (existingProgressBar != null)
            {
                ReparentToGroup(existingProgressBar.transform, progressGroupTransform);
            }
            TMPro.TMP_Text existingStatusText = statusTextProp != null ? statusTextProp.objectReferenceValue as TMPro.TMP_Text : null;
            if (existingStatusText != null)
            {
                // Skip if the status text sits inside the progress bar
                // hierarchy already (some bars nest the label inside the
                // bar background). We just move top-level scene siblings.
                if (existingStatusText.transform.parent == splashRoot)
                {
                    ReparentToGroup(existingStatusText.transform, progressGroupTransform);
                }
            }

            // 6. Force the sibling order so render layering is:
            //    Black → LoadingBg → ProgressGroup → (logo last, on top).
            blackBg.transform.SetSiblingIndex(0);
            loadingBg.transform.SetSiblingIndex(1);
            progressGroupTransform.SetSiblingIndex(2);
            // Existing SplashLogo (if present) goes last so the logo
            // overlay always sits above everything in phase 1.
            Image existingLogoImage = logoImageProp != null ? logoImageProp.objectReferenceValue as Image : null;
            if (existingLogoImage != null)
            {
                existingLogoImage.transform.SetAsLastSibling();
            }

            // 7. Wire all the new references on BootSplashView via SO so
            //    SerializeField rules apply — same path the inspector uses.
            viewObj.FindProperty("blackBackground").objectReferenceValue = blackBg;
            viewObj.FindProperty("loadingBackground").objectReferenceValue = loadingBg;
            viewObj.FindProperty("loadingBackgroundCanvasGroup").objectReferenceValue = loadingBgGroup;
            viewObj.FindProperty("progressUIGroup").objectReferenceValue = progressGroup;
            viewObj.FindProperty("rootCanvasGroup").objectReferenceValue = rootGroup;
            viewObj.ApplyModifiedProperties();

            EditorUtility.SetDirty(view);
            Scene scene = view.gameObject.scene;
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Undo.CollapseUndoOperations(undoGroup);

            EditorUtility.DisplayDialog(
                "Splash Setup",
                "Splash layers wired:\n" +
                $"  • Canvas sortOrder = {CanvasSortOrder}\n" +
                "  • BlackBackground (opaque)\n" +
                "  • LoadingBackground (drop your wide art into Image.sprite)\n" +
                "  • ProgressUIGroup (existing bar + status moved here)\n" +
                "  • Root CanvasGroup for the final fade-out\n\n" +
                "Next: assign the wide intro sprite to LoadingBackground's Image if it is still empty.",
                "OK");
        }

        private static Image FindOrCreateChildImage(Transform parent, string name, int siblingIndex)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Image existingImage = existing.GetComponent<Image>();
                if (existingImage == null)
                {
                    existingImage = Undo.AddComponent<Image>(existing.gameObject);
                }
                return existingImage;
            }

            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.layer = parent.gameObject.layer;
            Undo.SetTransformParent(go.transform, parent, "Parent " + name);
            go.transform.SetSiblingIndex(siblingIndex);
            return go.GetComponent<Image>();
        }

        private static void StretchToParent(RectTransform rt)
        {
            Undo.RecordObject(rt, "Stretch " + rt.name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            EditorUtility.SetDirty(rt);
        }

        private static void CenterFixedSize(RectTransform rt, float width, float height)
        {
            Undo.RecordObject(rt, "Center " + rt.name);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
            rt.pivot = new Vector2(0.5f, 0.5f);
            EditorUtility.SetDirty(rt);
        }

        private static void ReparentToGroup(Transform child, Transform newParent)
        {
            if (child.parent == newParent)
            {
                return;
            }
            // Preserve world position so the artist's existing layout
            // isn't snapped to the group's center.
            Undo.SetTransformParent(child, newParent, "Move " + child.name + " under " + newParent.name);
        }
    }
}
#endif
