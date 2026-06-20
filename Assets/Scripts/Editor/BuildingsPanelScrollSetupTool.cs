#if UNITY_EDITOR
#nullable enable

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Restructures the BuildingsPanel/PanelHolder inside Canvas.prefab into
    /// a working horizontal ScrollRect: adds ScrollRect + RectMask2D to
    /// PanelHolder, removes the layout that doesn't belong there, and
    /// creates a Content child with HorizontalLayoutGroup +
    /// ContentSizeFitter. Idempotent — re-running it skips work that's
    /// already done.
    ///
    /// After this runs, ONE manual step remains: in the scene, set
    /// BuildingsPanelPresenter.contentRoot to the new "Content" child
    /// (instead of PanelHolder).
    /// </summary>
    internal static class BuildingsPanelScrollSetupTool
    {
        private const string PrefabPath = "Assets/Prefabs/Canvas.prefab";
        private const string PanelHolderName = "PanelHolder";
        private const string ContentName = "Content";

        [MenuItem("CoinDreams/UI/Setup Buildings Panel Scroll")]
        private static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                EditorUtility.DisplayDialog("Scroll Setup",
                    "Could not find " + PrefabPath, "OK");
                return;
            }

            StringBuilder report = new StringBuilder();
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform? panelHolder = FindDeep(contents.transform, PanelHolderName);
                if (panelHolder == null)
                {
                    EditorUtility.DisplayDialog("Scroll Setup",
                        "Could not find '" + PanelHolderName + "' inside Canvas.prefab. " +
                        "Make sure the BuildingsPanel/PanelHolder structure exists.",
                        "OK");
                    return;
                }

                GameObject panelHolderGo = panelHolder.gameObject;

                // 1) Strip the HorizontalLayoutGroup that lives on PanelHolder
                //    — it belongs on Content now, not on the viewport.
                HorizontalLayoutGroup oldLayout = panelHolderGo.GetComponent<HorizontalLayoutGroup>();
                if (oldLayout != null)
                {
                    Object.DestroyImmediate(oldLayout);
                    report.AppendLine("- Removed HorizontalLayoutGroup from PanelHolder.");
                }

                // 2) ScrollRect on PanelHolder (it's the viewport).
                ScrollRect scrollRect = panelHolderGo.GetComponent<ScrollRect>();
                if (scrollRect == null)
                {
                    scrollRect = panelHolderGo.AddComponent<ScrollRect>();
                    report.AppendLine("+ Added ScrollRect to PanelHolder.");
                }
                scrollRect.horizontal = true;
                scrollRect.vertical = false;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
                scrollRect.elasticity = 0.1f;
                scrollRect.inertia = true;
                scrollRect.decelerationRate = 0.135f;
                scrollRect.scrollSensitivity = 1f;
                scrollRect.viewport = (RectTransform)panelHolder; // PanelHolder is the viewport

                // 3) RectMask2D so overflow gets clipped to the viewport.
                RectMask2D mask = panelHolderGo.GetComponent<RectMask2D>();
                if (mask == null)
                {
                    mask = panelHolderGo.AddComponent<RectMask2D>();
                    report.AppendLine("+ Added RectMask2D to PanelHolder.");
                }

                // 4) Content child (where UpgradeObjects will live).
                Transform? content = FindDirectChild(panelHolder, ContentName);
                if (content == null)
                {
                    GameObject contentGo = new GameObject(ContentName, typeof(RectTransform));
                    contentGo.layer = panelHolderGo.layer; // UI layer
                    contentGo.transform.SetParent(panelHolder, false);
                    content = contentGo.transform;
                    report.AppendLine("+ Created child 'Content' under PanelHolder.");
                }

                RectTransform contentRect = (RectTransform)content;
                // Left-anchor, pivot at left, height stretches to parent —
                // so width is the only thing the layout/sizefitter manages.
                contentRect.anchorMin = new Vector2(0f, 0f);
                contentRect.anchorMax = new Vector2(0f, 1f);
                contentRect.pivot = new Vector2(0f, 0.5f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0f, 0f);

                // 5) HorizontalLayoutGroup on Content (arranges the rows).
                HorizontalLayoutGroup hlg = content.GetComponent<HorizontalLayoutGroup>();
                if (hlg == null)
                {
                    hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                    report.AppendLine("+ Added HorizontalLayoutGroup to Content.");
                }
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.spacing = 20f;
                hlg.childControlWidth = false;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.padding = new RectOffset(10, 10, 0, 0);

                // 6) ContentSizeFitter on Content so the width grows with
                //    the number of children (drives the scroll range).
                ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                    report.AppendLine("+ Added ContentSizeFitter to Content.");
                }
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

                // 7) Wire ScrollRect.content → Content.
                scrollRect.content = contentRect;
                report.AppendLine("+ Wired ScrollRect.content → Content.");

                // Save prefab.
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                report.AppendLine();
                report.AppendLine("Prefab saved.");
                report.AppendLine();
                report.AppendLine("ONE MANUAL STEP LEFT:");
                report.AppendLine("• Open the scene with BuildingsPanel.");
                report.AppendLine("• Select BuildingsPanel.");
                report.AppendLine("• In BuildingsPanelPresenter, drag the new 'Content' child into the 'Content Root' field");
                report.AppendLine("  (instead of PanelHolder).");
                report.AppendLine("• Save scene.");

                EditorUtility.DisplayDialog("Scroll Setup", report.ToString(), "OK");
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
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform? hit = FindDeep(parent.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
#endif
