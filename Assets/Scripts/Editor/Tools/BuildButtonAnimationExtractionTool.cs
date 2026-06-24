#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Game.Runtime.Cards;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor.Tools
{
    /// <summary>
    /// One-shot editor tool — performs an "Extract Method"-style refactor on
    /// the Feel chains that animate the BuildButton.
    ///
    /// Before:
    ///   BuildPanelOpenFeedbacks → MMF_Position(BuildButton) + 4 other position feedbacks
    ///   BuildPanelCloseFeedbacks → matching reverse
    ///   DrawButton_Disable / Enable → no BuildButton animation
    ///
    /// After:
    ///   BuildButton_SlideOut → MMF_Position(BuildButton)         ← extracted
    ///   BuildButton_SlideIn  → matching reverse                  ← extracted
    ///   BuildPanelOpenFeedbacks → 4 position feedbacks + bridge → BuildButton_SlideOut
    ///   BuildPanelCloseFeedbacks → 4 position feedbacks + bridge → BuildButton_SlideIn
    ///   DrawButton_Disable → existing feedbacks + bridge → BuildButton_SlideOut
    ///   DrawButton_Enable  → existing feedbacks + bridge → BuildButton_SlideIn
    ///
    /// Result: clicking DRAW plays the same BuildButton-out animation as
    /// clicking Build itself, with a single source of truth. Idempotent —
    /// re-runs are safe (won't re-extract or duplicate bridges).
    ///
    /// Delete this file when the extraction is done.
    /// </summary>
    public static class BuildButtonAnimationExtractionTool
    {
        private const string MenuRoot = "Tools/CoinDreams/Canvas/";

        // ------------------------------------------------------------------
        // ReturnButton trigger-hunter menu items: scans the whole scene for
        // anything that fires ReturnButtonAppear / ReturnButtonDisAppear OUTSIDE
        // of DrawWorkflowFeelTrigger.bindings (the legitimate driver). Used
        // when clicking BuildButton wrongly triggers the Return animation —
        // we hunt for stale UnityEvent OnClick wirings or MMF_Feedbacks
        // bridges that target the Return chains and remove them.
        // ------------------------------------------------------------------

        [MenuItem(MenuRoot + "Hunt Spurious Return Triggers (audit)", priority = 140)]
        public static void HuntReturnTriggersAudit() => HuntReturnTriggers(applyChanges: false);

        [MenuItem(MenuRoot + "Hunt Spurious Return Triggers (remove)", priority = 141)]
        public static void HuntReturnTriggersRemove() => HuntReturnTriggers(applyChanges: true);

        // ------------------------------------------------------------------
        // Hunt-and-fix for DrawButton & ReturnButton UnityEvent OnClick. These
        // buttons are CODE-DRIVEN (DrawButtonRouter / DrawHudInputBinder publish
        // signals from script; the workflow trigger plays feedbacks). Any
        // PlayFeedbacks UnityEvent on these buttons is leftover scene wiring
        // and breaks the steal-mode flow (ActionPanel hides + BuildButton
        // animates when stabbing). Designed to run AFTER a teammate finishes
        // scene edits, to scrub residual hand-wired listeners.
        // ------------------------------------------------------------------
        [MenuItem(MenuRoot + "Hunt DrawButton/ReturnButton OnClick (audit)", priority = 150)]
        public static void HuntDrawButtonOnClickAudit() => HuntCodeDrivenButtonOnClick(applyChanges: false);

        [MenuItem(MenuRoot + "Hunt DrawButton/ReturnButton OnClick (remove)", priority = 151)]
        public static void HuntDrawButtonOnClickRemove() => HuntCodeDrivenButtonOnClick(applyChanges: true);

        // ------------------------------------------------------------------
        // Read-only diagnostic. Dumps the complete content of every Feel chain
        // and every state binding the runtime uses — so we can SEE what's
        // really wired without guessing or reading YAML by hand. Run this
        // whenever a "Return / ActionPanel / BuildButton fires when it
        // shouldn't" bug shows up and the obvious hunts come back empty.
        // ------------------------------------------------------------------
        [MenuItem(MenuRoot + "Dump Feel Chains + Trigger Bindings", priority = 160)]
        public static void DumpFeelGraph() => DumpFeel();

        // Fix for the "BuildButton appears mid-steal" bug. DrawButton_Enable
        // currently bridges to BuildButton_SlideIn — so every voodoo phase
        // exit (Entry END, Stab END, etc.) flips the transition gate off,
        // fires DrawButton_Enable, and via the bridge plays SlideIn. Result:
        // BuildButton pops in during the steal mini-game, while ActionPanel
        // is still hidden by ActionButtons_Hide.
        //
        // The bridge belongs to a single moment: arriving back at the city.
        // Move it from the chain to a trigger binding (ReturningToCity.OnExit
        // — fires exactly once on the ReturningToCity → Idle transition).
        // ------------------------------------------------------------------
        // Removes the visual-gating feedbacks from DrawWorkflowFeelTrigger
        // and VoodooFeelTrigger. The state machine + router already drop
        // spam-clicks at the code layer (HandleDrawClicked returns None
        // outside Idle/DrawMode; DrawButtonRouter polls IsTransitioning),
        // so the visual dim/hide was redundant — and the ActionButtons_Hide
        // on session enter wrongly removed the player's UI during steal.
        //
        // Bindings stripped:
        //   DrawWorkflowFeelTrigger.bindings:
        //     MovingToBoard.OnEnter  (DrawButton_Disable → null)
        //     Drawing.OnEnter        (DrawButton_Disable → null)
        //     ReturningToCity.OnEnter(DrawButton_Disable → null)
        //     Idle.OnEnter           (DrawButton_Enable  → null)
        //   VoodooFeelTrigger fields:
        //     onTransitionEnter (DrawButton_Disable → null)
        //     onTransitionExit  (DrawButton_Enable  → null)
        //     onSessionEnter    (ActionButtons_Hide → null)
        //     onSessionExit     (ActionButtons_Show → null)
        //
        // Preserved: DrawMode's Return chains, Idle.OnExit BuildButton_SlideOut,
        // ReturningToCity.OnExit BuildButton_SlideIn.
        //
        // Resets DrawButton's + ActionPanel's CanvasGroups to alpha=1,
        // blocksRaycasts=true so any stuck state from prior play sessions
        // doesn't leak through.
        // ------------------------------------------------------------------
        [MenuItem(MenuRoot + "Remove Visual Gating (DrawButton + ActionPanel)", priority = 180)]
        public static void RemoveVisualGating()
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove visual gating");

            StringBuilder report = new StringBuilder(2048);
            report.AppendLine("=== Remove Visual Gating ===");

            List<GameObject> roots = CollectRoots(report);
            if (roots.Count == 0) return;

            MMF_Player? mmfDrawDisable = FindMmfByExactName(roots, MmfDrawDisable, report);
            MMF_Player? mmfDrawEnable = FindMmfByExactName(roots, MmfDrawEnable, report);
            MMF_Player? mmfActionHide = FindMmfByExactName(roots, "ActionButtons_Hide", report);
            MMF_Player? mmfActionShow = FindMmfByExactName(roots, "ActionButtons_Show", report);

            DrawWorkflowFeelTrigger? drawTrigger = FindComponent<DrawWorkflowFeelTrigger>(roots, report);
            Game.Runtime.Steal.VoodooFeelTrigger? voodooTrigger =
                FindComponent<Game.Runtime.Steal.VoodooFeelTrigger>(roots, report);

            report.AppendLine();
            report.AppendLine("--- Clear DrawWorkflowFeelTrigger.bindings (OnEnter only — OnExit preserved) ---");
            if (drawTrigger != null)
            {
                ClearBindingOnEnterIfMatches(drawTrigger, CardDrawWorkflowState.MovingToBoard, mmfDrawDisable, report);
                ClearBindingOnEnterIfMatches(drawTrigger, CardDrawWorkflowState.Drawing, mmfDrawDisable, report);
                ClearBindingOnEnterIfMatches(drawTrigger, CardDrawWorkflowState.ReturningToCity, mmfDrawDisable, report);
                ClearBindingOnEnterIfMatches(drawTrigger, CardDrawWorkflowState.Idle, mmfDrawEnable, report);
            }

            report.AppendLine();
            report.AppendLine("--- Clear VoodooFeelTrigger fields ---");
            if (voodooTrigger != null)
            {
                ClearVoodooFieldIfMatches(voodooTrigger, "onTransitionEnter", mmfDrawDisable, report);
                ClearVoodooFieldIfMatches(voodooTrigger, "onTransitionExit", mmfDrawEnable, report);
                ClearVoodooFieldIfMatches(voodooTrigger, "onSessionEnter", mmfActionHide, report);
                ClearVoodooFieldIfMatches(voodooTrigger, "onSessionExit", mmfActionShow, report);
            }

            report.AppendLine();
            report.AppendLine("--- Reset CanvasGroups (safety — clear stuck states) ---");
            GameObject? drawButtonGo = FindButtonGameObject(roots, new[] { "DrawButton", "Draw_Button" }, "DrawButton", report);
            GameObject? actionPanel = FindGameObject(roots, new[] { "ActionPanel", "Action_Panel" }, "ActionPanel", report);
            if (drawButtonGo != null) ResetCanvasGroup(drawButtonGo, report);
            if (actionPanel != null) ResetCanvasGroup(actionPanel, report);

            MarkAllDirty(roots);
            Undo.CollapseUndoOperations(undoGroup);

            report.AppendLine();
            report.AppendLine("--- Orphan chains (now safe to delete in Inspector if you want full SOLID) ---");
            report.AppendLine("  • DrawButton_Disable");
            report.AppendLine("  • DrawButton_Enable");
            report.AppendLine("  • ActionButtons_Hide");
            report.AppendLine("  • ActionButtons_Show");

            Debug.Log(report.ToString());
        }

        private static void ClearBindingOnEnterIfMatches(
            DrawWorkflowFeelTrigger trigger,
            CardDrawWorkflowState state,
            MMF_Player? expected,
            StringBuilder report)
        {
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty bindings = so.FindProperty("bindings");
            if (bindings == null) return;

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("State").enumValueIndex != (int)state) continue;

                SerializedProperty onEnter = entry.FindPropertyRelative("OnEnter");
                UnityEngine.Object current = onEnter.objectReferenceValue;
                if (current == null)
                {
                    report.Append("    ✓ ").Append(state).AppendLine(".OnEnter already null");
                }
                else if (current == expected)
                {
                    onEnter.objectReferenceValue = null;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(trigger);
                    report.Append("    - cleared ").Append(state).Append(".OnEnter (was ")
                        .Append(current.name).AppendLine(")");
                }
                else
                {
                    report.Append("    ⊘ ").Append(state).Append(".OnEnter is ")
                        .Append(current.name).AppendLine(" — not the gating chain, preserved");
                }
                return;
            }
            report.Append("    ℹ️  no binding for ").AppendLine(state.ToString());
        }

        private static void ClearVoodooFieldIfMatches(
            Component trigger,
            string fieldName,
            MMF_Player? expected,
            StringBuilder report)
        {
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                report.Append("    ⚠️  field ").Append(fieldName).AppendLine(" not found");
                return;
            }
            UnityEngine.Object current = prop.objectReferenceValue;
            if (current == null)
            {
                report.Append("    ✓ ").Append(fieldName).AppendLine(" already null");
            }
            else if (current == expected)
            {
                prop.objectReferenceValue = null;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(trigger);
                report.Append("    - cleared ").Append(fieldName).Append(" (was ")
                    .Append(current.name).AppendLine(")");
            }
            else
            {
                report.Append("    ⊘ ").Append(fieldName).Append(" is ")
                    .Append(current.name).AppendLine(" — not the gating chain, preserved");
            }
        }

        private static void ResetCanvasGroup(GameObject go, StringBuilder report)
        {
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                report.Append("    ✓ ").Append(go.name).AppendLine(" has no CanvasGroup — nothing to reset");
                return;
            }
            bool needs = !Mathf.Approximately(cg.alpha, 1f) || !cg.blocksRaycasts || !cg.interactable;
            if (!needs)
            {
                report.Append("    ✓ ").Append(go.name).AppendLine(" CG already alpha=1, blocks=true, interactable=true");
                return;
            }
            Undo.RecordObject(cg, "Reset CanvasGroup");
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
            EditorUtility.SetDirty(cg);
            report.Append("    + reset ").Append(go.name).AppendLine(" CG → alpha=1, blocks=true, interactable=true");
        }

        [MenuItem(MenuRoot + "Fix BuildButton SlideIn timing", priority = 170)]
        public static void FixBuildButtonSlideInTiming()
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fix BuildButton SlideIn timing");

            StringBuilder report = new StringBuilder(1024);
            report.AppendLine("=== Fix BuildButton SlideIn timing ===");

            List<GameObject> roots = CollectRoots(report);
            if (roots.Count == 0) return;

            MMF_Player? mmfDrawEnable = FindMmfByExactName(roots, MmfDrawEnable, report);
            MMF_Player? mmfSlideIn = FindMmfByExactName(roots, SlideInGoName, report);
            DrawWorkflowFeelTrigger? drawTrigger = FindComponent<DrawWorkflowFeelTrigger>(roots, report);

            if (mmfDrawEnable == null || mmfSlideIn == null || drawTrigger == null)
            {
                report.AppendLine("🚨 Missing prerequisite — aborting.");
                Debug.LogError(report.ToString());
                return;
            }

            report.AppendLine();
            report.AppendLine("--- Unwire DrawButton_Enable → BuildButton_SlideIn ---");
            UnwireBridgeIfPresent(mmfDrawEnable, mmfSlideIn, report);

            report.AppendLine();
            report.AppendLine("--- Wire ReturningToCity.OnExit → BuildButton_SlideIn ---");
            SetBindingOnExit(drawTrigger, CardDrawWorkflowState.ReturningToCity, mmfSlideIn, report);

            MarkAllDirty(roots);
            Undo.CollapseUndoOperations(undoGroup);

            report.AppendLine();
            report.AppendLine("--- Audit ---");
            AuditBridgeAbsent(mmfDrawEnable, mmfSlideIn,
                "DrawButton_Enable ⊘ BuildButton_SlideIn (moved to ReturningToCity.OnExit)", report);
            AuditBindingOnExit(drawTrigger, CardDrawWorkflowState.ReturningToCity, mmfSlideIn,
                "ReturningToCity.OnExit → BuildButton_SlideIn", report);

            Debug.Log(report.ToString());
        }

        private static readonly string[] ChainsToDump =
        {
            "DrawButton_Disable", "DrawButton_Enable",
            "ActionButtons_Hide", "ActionButtons_Show",
            "ReturnButtonAppear", "ReturnButtonDisAppear",
            "BuildButton_SlideOut", "BuildButton_SlideIn",
            "BuildPanelOpenFeedbacks", "BuildPanelCloseFeedbacks",
        };

        private static void DumpFeel()
        {
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("=== Feel Graph Dump ===");

            List<GameObject> roots = CollectRoots(report);
            if (roots.Count == 0) return;

            // 1) Per-chain feedback dump.
            for (int n = 0; n < ChainsToDump.Length; n++)
            {
                string name = ChainsToDump[n];
                MMF_Player? mmf = FindMmfByExactName(roots, name, report);
                report.AppendLine();
                report.Append("--- ").Append(name).AppendLine(" ---");
                if (mmf == null) { report.AppendLine("  (chain not present)"); continue; }
                DumpFeedbacks(mmf, report);
            }

            // 2) DrawWorkflowFeelTrigger bindings.
            report.AppendLine();
            report.AppendLine("--- DrawWorkflowFeelTrigger.bindings ---");
            DrawWorkflowFeelTrigger? drawTrigger = FindComponent<DrawWorkflowFeelTrigger>(roots, report);
            if (drawTrigger != null) DumpDrawBindings(drawTrigger, report);

            // 3) VoodooFeelTrigger fields.
            report.AppendLine();
            report.AppendLine("--- VoodooFeelTrigger fields ---");
            DumpVoodooFields(roots, report);

            Debug.Log(report.ToString());
        }

        private static void DumpFeedbacks(MMF_Player mmf, StringBuilder report)
        {
            if (mmf.FeedbacksList == null || mmf.FeedbacksList.Count == 0)
            {
                report.AppendLine("  (no feedbacks)");
                return;
            }
            for (int i = 0; i < mmf.FeedbacksList.Count; i++)
            {
                MMF_Feedback fb = mmf.FeedbacksList[i];
                if (fb == null) { report.Append("  [").Append(i).AppendLine("] <null>"); continue; }
                report.Append("  [").Append(i).Append("] ").Append(fb.GetType().Name);
                if (!string.IsNullOrEmpty(fb.Label)) report.Append(" \"").Append(fb.Label).Append('"');

                // Type-specific target / value details.
                if (fb is MMF_CanvasGroup cg)
                {
                    string target = cg.TargetCanvasGroup != null ? cg.TargetCanvasGroup.gameObject.name : "<null>";
                    report.Append("  → ").Append(target)
                        .Append("  Mode=").Append(cg.Mode)
                        .Append("  InstantAlpha=").Append(cg.InstantAlpha);
                }
                else if (fb is MMF_CanvasGroupBlocksRaycasts cgb)
                {
                    string target = cgb.TargetCanvasGroup != null ? cgb.TargetCanvasGroup.gameObject.name : "<null>";
                    report.Append("  → ").Append(target).Append("  Block=").Append(cgb.ShouldBlockRaycasts);
                }
                else if (fb is MMF_Position pos)
                {
                    string target = pos.AnimatePositionTarget != null ? pos.AnimatePositionTarget.name : "<null>";
                    report.Append("  → ").Append(target);
                }
                else if (fb is MMF_Feedbacks bridge)
                {
                    string target = bridge.TargetFeedbacks != null ? bridge.TargetFeedbacks.name : "<null>";
                    report.Append("  → ").Append(target).Append("  Mode=").Append(bridge.Mode);
                }
                report.AppendLine();
            }
        }

        private static void DumpDrawBindings(DrawWorkflowFeelTrigger trigger, StringBuilder report)
        {
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty bindings = so.FindProperty("bindings");
            if (bindings == null) { report.AppendLine("  (bindings field not found)"); return; }
            if (bindings.arraySize == 0) { report.AppendLine("  (no bindings)"); return; }

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                CardDrawWorkflowState state = (CardDrawWorkflowState)entry.FindPropertyRelative("State").enumValueIndex;
                UnityEngine.Object onEnter = entry.FindPropertyRelative("OnEnter").objectReferenceValue;
                UnityEngine.Object onExit = entry.FindPropertyRelative("OnExit").objectReferenceValue;

                report.Append("  [").Append(i).Append("] ").Append(state)
                    .Append("  OnEnter=").Append(onEnter != null ? onEnter.name : "<null>")
                    .Append("  OnExit=").Append(onExit != null ? onExit.name : "<null>")
                    .AppendLine();
            }
        }

        private static void DumpVoodooFields(List<GameObject> roots, StringBuilder report)
        {
            foreach (GameObject root in roots)
            {
                Game.Runtime.Steal.VoodooFeelTrigger trigger = root.GetComponentInChildren<Game.Runtime.Steal.VoodooFeelTrigger>(true);
                if (trigger == null) continue;
                SerializedObject so = new SerializedObject(trigger);
                string[] fields = { "onTransitionEnter", "onTransitionExit", "onSessionEnter", "onSessionExit" };
                foreach (string name in fields)
                {
                    SerializedProperty prop = so.FindProperty(name);
                    if (prop == null) continue;
                    UnityEngine.Object val = prop.objectReferenceValue;
                    report.Append("  ").Append(name).Append(" = ")
                        .AppendLine(val != null ? val.name : "<null>");
                }
                return;
            }
            report.AppendLine("  (VoodooFeelTrigger not found)");
        }


        private static readonly string[] CodeDrivenButtonNames =
        {
            "DrawButton", "Draw_Button", "ReturnButton", "Return_Button",
        };

        private static void HuntCodeDrivenButtonOnClick(bool applyChanges)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(applyChanges
                ? "Remove code-driven button OnClick PlayFeedbacks"
                : "Audit code-driven button OnClick");

            StringBuilder report = new StringBuilder(1024);
            report.Append("=== Hunt DrawButton/ReturnButton OnClick — ");
            report.Append(applyChanges ? "REMOVE" : "AUDIT");
            report.AppendLine(" ===");

            List<GameObject> roots = CollectRoots(report);
            if (roots.Count == 0) return;

            int removed = 0;
            int found = 0;
            foreach (GameObject root in roots)
            {
                Button[] all = root.GetComponentsInChildren<Button>(true);
                for (int b = 0; b < all.Length; b++)
                {
                    Button button = all[b];
                    string name = button.gameObject.name;
                    bool nameMatches = false;
                    for (int n = 0; n < CodeDrivenButtonNames.Length; n++)
                    {
                        if (string.Equals(name, CodeDrivenButtonNames[n], StringComparison.OrdinalIgnoreCase))
                        {
                            nameMatches = true;
                            break;
                        }
                    }
                    if (!nameMatches) continue;

                    int count = button.onClick.GetPersistentEventCount();
                    for (int i = count - 1; i >= 0; i--)
                    {
                        string m = button.onClick.GetPersistentMethodName(i);
                        if (m != "PlayFeedbacks") continue;
                        UnityEngine.Object t = button.onClick.GetPersistentTarget(i);
                        string targetName = t != null ? t.name : "<null>";

                        string label = name + ".onClick[" + i + "] → " + targetName + ".PlayFeedbacks()";
                        if (applyChanges)
                        {
                            Undo.RecordObject(button, "Remove OnClick PlayFeedbacks");
                            UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
                            EditorUtility.SetDirty(button);
                            report.Append("    - removed ").AppendLine(label);
                            removed++;
                        }
                        else
                        {
                            report.Append("    🚨 found ").AppendLine(label);
                            found++;
                        }
                    }
                }
            }

            if (applyChanges)
            {
                MarkAllDirty(roots);
                Undo.CollapseUndoOperations(undoGroup);
            }

            report.AppendLine();
            int total = applyChanges ? removed : found;
            if (total == 0)
            {
                report.AppendLine("✓ No PlayFeedbacks listeners on DrawButton/ReturnButton.");
                report.AppendLine("  These buttons are code-driven (signals via DrawButtonRouter +");
                report.AppendLine("  DrawWorkflowFeelTrigger), so an empty UnityEvent OnClick is correct.");
            }
            else
            {
                report.Append(applyChanges ? "Removed " : "Found ").Append(total)
                    .AppendLine(" stray PlayFeedbacks wiring(s).");
            }

            Debug.Log(report.ToString());
        }


        private static void HuntReturnTriggers(bool applyChanges)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(applyChanges
                ? "Remove spurious Return triggers"
                : "Audit spurious Return triggers");

            StringBuilder report = new StringBuilder(1024);
            report.Append("=== Hunt Spurious Return Triggers — ");
            report.Append(applyChanges ? "REMOVE" : "AUDIT");
            report.AppendLine(" ===");

            List<GameObject> roots = CollectRoots(report);
            if (roots.Count == 0) return;

            // Resolve the Return chains.
            MMF_Player? returnAppear = FindMmfByExactName(roots, "ReturnButtonAppear", report);
            MMF_Player? returnDisappear = FindMmfByExactName(roots, "ReturnButtonDisAppear", report);
            if (returnAppear == null && returnDisappear == null)
            {
                report.AppendLine("⚠️  Neither Return chain found — nothing to hunt.");
                Debug.LogWarning(report.ToString());
                return;
            }

            report.AppendLine();
            report.AppendLine("--- Scanning every Button.onClick for PlayFeedbacks → Return chains ---");
            int onClickRemoved = 0;
            foreach (GameObject root in roots)
            {
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                for (int b = 0; b < buttons.Length; b++)
                {
                    Button button = buttons[b];
                    int count = button.onClick.GetPersistentEventCount();
                    for (int i = count - 1; i >= 0; i--)
                    {
                        UnityEngine.Object t = button.onClick.GetPersistentTarget(i);
                        string m = button.onClick.GetPersistentMethodName(i);
                        if (m != "PlayFeedbacks") continue;
                        if (t != returnAppear && t != returnDisappear) continue;

                        string label = button.gameObject.name + ".onClick[" + i + "] → " +
                            (t == returnAppear ? "ReturnButtonAppear" : "ReturnButtonDisAppear");
                        if (applyChanges)
                        {
                            Undo.RecordObject(button, "Remove spurious Return OnClick");
                            UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
                            EditorUtility.SetDirty(button);
                            report.Append("    - removed ").AppendLine(label);
                        }
                        else
                        {
                            report.Append("    🚨 found ").AppendLine(label);
                        }
                        onClickRemoved++;
                    }
                }
            }
            if (onClickRemoved == 0) report.AppendLine("    ✓ no spurious OnClick listeners");

            report.AppendLine();
            report.AppendLine("--- Scanning every MMF_Player chain for bridges → Return chains ---");
            int bridgeRemoved = 0;
            foreach (GameObject root in roots)
            {
                MMF_Player[] players = root.GetComponentsInChildren<MMF_Player>(true);
                for (int p = 0; p < players.Length; p++)
                {
                    MMF_Player player = players[p];
                    if (player == returnAppear || player == returnDisappear) continue;
                    if (player.FeedbacksList == null) continue;

                    for (int i = player.FeedbacksList.Count - 1; i >= 0; i--)
                    {
                        if (player.FeedbacksList[i] is MMF_Feedbacks bridge
                            && bridge.Mode == MMF_Feedbacks.Modes.PlayTargetFeedbacks
                            && (bridge.TargetFeedbacks == returnAppear
                                || bridge.TargetFeedbacks == returnDisappear))
                        {
                            string label = player.gameObject.name + " → " +
                                (bridge.TargetFeedbacks == returnAppear
                                    ? "ReturnButtonAppear" : "ReturnButtonDisAppear");
                            if (applyChanges)
                            {
                                Undo.RecordObject(player, "Remove spurious Return bridge");
                                player.FeedbacksList.RemoveAt(i);
                                EditorUtility.SetDirty(player);
                                report.Append("    - removed bridge ").AppendLine(label);
                            }
                            else
                            {
                                report.Append("    🚨 found bridge ").AppendLine(label);
                            }
                            bridgeRemoved++;
                        }
                    }
                }
            }
            if (bridgeRemoved == 0) report.AppendLine("    ✓ no spurious bridges");

            if (applyChanges)
            {
                MarkAllDirty(roots);
                Undo.CollapseUndoOperations(undoGroup);
            }

            report.AppendLine();
            int total = onClickRemoved + bridgeRemoved;
            if (total == 0)
            {
                report.AppendLine("✓ Scene is clean — no spurious triggers found.");
                report.AppendLine("  If the Return animation still plays on Build click, the cause");
                report.AppendLine("  may be parent-transform inheritance (ActionPanel slides down,");
                report.AppendLine("  ReturnButton goes with it as a child of the panel).");
            }
            else
            {
                report.Append(applyChanges ? "Removed " : "Found ").Append(total)
                    .AppendLine(" spurious trigger(s).");
            }

            Debug.Log(report.ToString());
        }


        private const string SlideOutGoName = "BuildButton_SlideOut";
        private const string SlideInGoName = "BuildButton_SlideIn";

        private static readonly string[] BuildButtonNames = { "BuildButton", "Build_Button" };
        private static readonly string[] ReturnButtonNames = { "ReturnButton", "Return_Button" };
        private static readonly string[] UiFeelParentNames = { "UIfeel", "UIFeel", "UI_Feel" };
        private static readonly string[] BuildPanelOpenNames =
            { "BuildPanelOpenFeedbacks", "BuildPanelOpen", "BuildPanel_Open" };
        private static readonly string[] BuildPanelCloseNames =
            { "BuildPanelCloseFeedbacks", "BuildPanelClose", "BuildPanel_Close" };
        private static readonly string[] ReturnDisappearNames =
            { "ReturnButtonDisAppear", "ReturnButtonDisappear", "ReturnButton_Disappear" };
        private const string MmfDrawDisable = "DrawButton_Disable";
        private const string MmfDrawEnable = "DrawButton_Enable";

        [MenuItem(MenuRoot + "Extract BuildButton Animation (audit)", priority = 130)]
        public static void AuditOnly() => Run(applyChanges: false);

        [MenuItem(MenuRoot + "Extract BuildButton Animation (apply)", priority = 131)]
        public static void Apply() => Run(applyChanges: true);

        private static void Run(bool applyChanges)
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(applyChanges
                ? "Extract BuildButton Animation"
                : "Extract BuildButton Animation (audit)");

            StringBuilder report = new StringBuilder(2048);
            report.Append("=== Extract BuildButton Animation — ");
            report.Append(applyChanges ? "APPLY" : "AUDIT");
            report.AppendLine(" ===");

            List<GameObject> roots = CollectRoots(report);
            if (roots.Count == 0)
            {
                Debug.LogWarning("[BuildButtonAnimationExtractionTool] Nothing to scan.");
                return;
            }

            // ============================================================
            // PHASE 1 — Resolve prerequisites.
            // ============================================================
            report.AppendLine();
            report.AppendLine("--- Resolve ---");

            GameObject? buildButton = FindButtonGameObject(roots, BuildButtonNames, "BuildButton", report);
            GameObject? uiFeel = FindGameObject(roots, UiFeelParentNames, "UIfeel parent", report);
            MMF_Player? mmfBuildOpen = FindMmfByAnyName(roots, BuildPanelOpenNames, "BuildPanelOpen", report);
            MMF_Player? mmfBuildClose = FindMmfByAnyName(roots, BuildPanelCloseNames, "BuildPanelClose", report);
            MMF_Player? mmfDrawDisable = FindMmfByExactName(roots, MmfDrawDisable, report);
            MMF_Player? mmfDrawEnable = FindMmfByExactName(roots, MmfDrawEnable, report);

            if (buildButton == null || uiFeel == null
                || mmfBuildOpen == null || mmfBuildClose == null
                || mmfDrawDisable == null || mmfDrawEnable == null)
            {
                report.AppendLine();
                report.AppendLine("🚨 Missing prerequisite — aborting. Run CanvasCleanup + FeedbackFiller first.");
                Debug.LogError(report.ToString());
                return;
            }

            MMF_Player? mmfSlideOut = FindMmfByExactName(roots, SlideOutGoName, report);
            MMF_Player? mmfSlideIn = FindMmfByExactName(roots, SlideInGoName, report);
            MMF_Player? mmfReturnDisappear = FindMmfByAnyName(roots, ReturnDisappearNames, "ReturnButtonDisAppear", report);
            GameObject? returnButton = FindButtonGameObject(roots, ReturnButtonNames, "ReturnButton", report);
            DrawWorkflowFeelTrigger? drawTrigger = FindComponent<DrawWorkflowFeelTrigger>(roots, report);

            if (applyChanges)
            {
                report.AppendLine();
                report.AppendLine("--- Apply ---");

                mmfSlideOut = EnsureMmfChain(uiFeel, SlideOutGoName, report);
                mmfSlideIn = EnsureMmfChain(uiFeel, SlideInGoName, report);

                ExtractAndBridge(
                    sourceChain: mmfBuildOpen,
                    extractedChain: mmfSlideOut,
                    targetGo: buildButton,
                    bridgeLabel: "→ BuildButton_SlideOut",
                    report);

                ExtractAndBridge(
                    sourceChain: mmfBuildClose,
                    extractedChain: mmfSlideIn,
                    targetGo: buildButton,
                    bridgeLabel: "→ BuildButton_SlideIn",
                    report);

                // BuildButton_SlideOut MUST NOT fire on every state that
                // locks the draw button — only when LEAVING the city (Idle
                // → MovingToBoard). Bridging it from DrawButton_Disable made
                // it fire on Drawing.OnEnter and ReturningToCity.OnEnter too,
                // causing the spurious exit animation when clicking Return.
                // Strip the bridge and rewire SlideOut to Idle.OnExit on the
                // DrawWorkflowFeelTrigger.
                UnwireBridgeIfPresent(mmfDrawDisable, mmfSlideOut, report);
                if (drawTrigger != null)
                {
                    SetBindingOnExit(drawTrigger, CardDrawWorkflowState.Idle, mmfSlideOut, report);
                }

                // SlideIn STAYS bridged from DrawButton_Enable — Enable is
                // only wired to Idle.OnEnter, so the bridge fires exactly
                // once per cycle at the right moment (arriving at city).
                AddBridgeIfMissing(mmfDrawEnable, mmfSlideIn, "→ BuildButton_SlideIn", report);

                // ReturnButton disappear used to set CanvasGroup alpha=0
                // INSTANT (added by the original feedback-filler pass). That
                // killed the visual on the same frame the chain started, so
                // the MMF_Scale exit animation never showed up. Strip the
                // alpha=0 feedback — scale-to-0 is enough to hide the button,
                // and BlocksRaycasts on the same chain still gates clicks.
                if (mmfReturnDisappear != null && returnButton != null)
                {
                    CanvasGroup returnCg = returnButton.GetComponent<CanvasGroup>();
                    if (returnCg != null)
                    {
                        RemoveCanvasGroupAlphaFeedback(mmfReturnDisappear, returnCg, report);
                    }
                }

                MarkAllDirty(roots);
                Undo.CollapseUndoOperations(undoGroup);
            }

            // ============================================================
            // PHASE 3 — Audit.
            // ============================================================
            report.AppendLine();
            report.AppendLine("--- Audit ---");

            AuditChain(mmfBuildOpen, mmfSlideOut, buildButton, "BuildPanelOpen / SlideOut", report);
            AuditChain(mmfBuildClose, mmfSlideIn, buildButton, "BuildPanelClose / SlideIn", report);
            AuditBridgeAbsent(mmfDrawDisable, mmfSlideOut,
                "DrawButton_Disable ⊘ BuildButton_SlideOut (moved to Idle.OnExit)", report);
            AuditBridge(mmfDrawEnable, mmfSlideIn, "DrawButton_Enable → BuildButton_SlideIn", report);
            AuditBindingOnExit(drawTrigger, CardDrawWorkflowState.Idle, mmfSlideOut,
                "DrawWorkflowFeelTrigger.Idle.OnExit → BuildButton_SlideOut", report);
            AuditReturnDisappearChain(mmfReturnDisappear, returnButton, report);

            Debug.Log(report.ToString());
        }

        // Verifies the ReturnButton scale-out animation isn't blocked by an
        // alpha=0 INSTANT sitting in the same chain.
        private static void AuditReturnDisappearChain(
            MMF_Player? chain, GameObject? returnButton, StringBuilder report)
        {
            if (chain == null || returnButton == null) return;
            CanvasGroup cg = returnButton.GetComponent<CanvasGroup>();
            if (cg == null) return;
            if (chain.FeedbacksList == null) return;

            for (int i = 0; i < chain.FeedbacksList.Count; i++)
            {
                if (chain.FeedbacksList[i] is MMF_CanvasGroup found
                    && found.TargetCanvasGroup == cg)
                {
                    report.AppendLine("    🚨 ReturnButtonDisAppear still has CanvasGroup alpha — scale animation blocked");
                    return;
                }
            }
            report.AppendLine("    ✓ ReturnButtonDisAppear has no alpha feedback — scale animation visible");
        }

        // ============================================================
        // Resolve helpers.
        // ============================================================
        private static List<GameObject> CollectRoots(StringBuilder report)
        {
            List<GameObject> result = new List<GameObject>();
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                result.Add(prefabStage.prefabContentsRoot);
                report.Append("Scanning prefab stage: ").AppendLine(prefabStage.assetPath);
                return result;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                report.Append("Scanning scene: ").AppendLine(scene.name);
                foreach (GameObject root in scene.GetRootGameObjects()) result.Add(root);
            }
            return result;
        }

        private static GameObject? FindButtonGameObject(
            List<GameObject> roots, string[] candidates, string label, StringBuilder report)
        {
            foreach (GameObject root in roots)
            {
                Button[] all = root.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    string n = all[i].gameObject.name;
                    for (int c = 0; c < candidates.Length; c++)
                    {
                        if (string.Equals(n, candidates[c], StringComparison.OrdinalIgnoreCase))
                        {
                            report.Append("  ✓ ").Append(label).Append(": ")
                                .AppendLine(PathOf(all[i].transform));
                            return all[i].gameObject;
                        }
                    }
                }
            }
            report.Append("  ⚠️  ").Append(label).AppendLine(" not found.");
            return null;
        }

        private static GameObject? FindGameObject(
            List<GameObject> roots, string[] candidates, string label, StringBuilder report)
        {
            foreach (GameObject root in roots)
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    string n = all[i].gameObject.name;
                    for (int c = 0; c < candidates.Length; c++)
                    {
                        if (string.Equals(n, candidates[c], StringComparison.OrdinalIgnoreCase))
                        {
                            report.Append("  ✓ ").Append(label).Append(": ").AppendLine(PathOf(all[i]));
                            return all[i].gameObject;
                        }
                    }
                }
            }
            report.Append("  ⚠️  ").Append(label).AppendLine(" not found.");
            return null;
        }

        private static MMF_Player? FindMmfByAnyName(
            List<GameObject> roots, string[] candidates, string label, StringBuilder report)
        {
            foreach (GameObject root in roots)
            {
                MMF_Player[] all = root.GetComponentsInChildren<MMF_Player>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    string n = all[i].gameObject.name;
                    for (int c = 0; c < candidates.Length; c++)
                    {
                        if (string.Equals(n, candidates[c], StringComparison.OrdinalIgnoreCase))
                        {
                            report.Append("  ✓ MMF '").Append(label).Append("': ")
                                .AppendLine(PathOf(all[i].transform));
                            return all[i];
                        }
                    }
                }
            }
            report.Append("  ⚠️  MMF '").Append(label).AppendLine("' not found.");
            return null;
        }

        private static MMF_Player? FindMmfByExactName(
            List<GameObject> roots, string exactName, StringBuilder report)
        {
            foreach (GameObject root in roots)
            {
                MMF_Player[] all = root.GetComponentsInChildren<MMF_Player>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (string.Equals(all[i].gameObject.name, exactName, StringComparison.OrdinalIgnoreCase))
                    {
                        report.Append("  ✓ MMF '").Append(exactName).Append("': ")
                            .AppendLine(PathOf(all[i].transform));
                        return all[i];
                    }
                }
            }
            report.Append("  ℹ️  MMF '").Append(exactName).AppendLine("' not yet present.");
            return null;
        }

        // ============================================================
        // Apply helpers.
        // ============================================================
        private static MMF_Player EnsureMmfChain(GameObject parent, string name, StringBuilder report)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null)
            {
                MMF_Player existingMmf = existing.GetComponent<MMF_Player>();
                if (existingMmf != null)
                {
                    report.Append("  ✓ Chain '").Append(name).AppendLine("' already exists");
                    return existingMmf;
                }
                MMF_Player added = Undo.AddComponent<MMF_Player>(existing.gameObject);
                report.Append("  + Added MMF_Player to existing GO '").Append(name).AppendLine("'");
                return added;
            }

            GameObject created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create extracted chain");
            Undo.SetTransformParent(created.transform, parent.transform, "Parent extracted chain");
            MMF_Player newMmf = Undo.AddComponent<MMF_Player>(created);
            report.Append("  + Created '").Append(name).Append("' under ").AppendLine(parent.name);
            return newMmf;
        }

        // Move all BuildButton-targeting feedbacks out of sourceChain into
        // extractedChain, then add a bridge in sourceChain that plays
        // extractedChain at the same point in the sequence. Idempotent: if
        // the bridge is already in place, nothing moves.
        private static void ExtractAndBridge(
            MMF_Player sourceChain,
            MMF_Player extractedChain,
            GameObject targetGo,
            string bridgeLabel,
            StringBuilder report)
        {
            report.Append("  --- ").Append(sourceChain.gameObject.name)
                .Append(" → ").Append(extractedChain.gameObject.name).AppendLine(" ---");

            if (HasBridgeTo(sourceChain, extractedChain))
            {
                report.Append("    ✓ bridge already exists — extraction skipped (idempotent).");
                report.AppendLine();
                return;
            }

            if (sourceChain.FeedbacksList == null)
            {
                report.AppendLine("    ⚠️  source FeedbacksList null — skipping.");
                return;
            }

            // Snapshot the list FIRST, then mutate. Iterating while removing
            // from FeedbacksList would skip elements.
            List<MMF_Position> toMove = new List<MMF_Position>();
            for (int i = 0; i < sourceChain.FeedbacksList.Count; i++)
            {
                if (sourceChain.FeedbacksList[i] is MMF_Position pos
                    && pos.AnimatePositionTarget == targetGo)
                {
                    toMove.Add(pos);
                }
            }

            if (toMove.Count == 0)
            {
                report.AppendLine("    ⚠️  no MMF_Position feedbacks target this GameObject — nothing to extract.");
                return;
            }

            Undo.RecordObject(sourceChain, "Extract BuildButton feedbacks");
            Undo.RecordObject(extractedChain, "Receive extracted feedbacks");

            for (int i = 0; i < toMove.Count; i++)
            {
                CloneFeedbackInto(extractedChain, toMove[i]);
                sourceChain.FeedbacksList.Remove(toMove[i]);
                report.Append("    + moved feedback '").Append(toMove[i].Label)
                    .AppendLine("' into extracted chain");
            }

            EditorUtility.SetDirty(sourceChain);
            EditorUtility.SetDirty(extractedChain);

            AddBridgeIfMissing(sourceChain, extractedChain, bridgeLabel, report);
        }

        // Reflection-based clone: copies every serialized field (public or
        // [SerializeField]-tagged, skipping [NonSerialized]) from source to
        // a fresh feedback added through MMF_Player.AddFeedback. Skips Owner
        // + UniqueID — AddFeedback set those, and reusing the source UniqueID
        // would collide.
        private static MMF_Feedback CloneFeedbackInto(MMF_Player target, MMF_Feedback source)
        {
            Type t = source.GetType();
            MMF_Feedback clone = target.AddFeedback(t);

            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsLiteral || field.IsInitOnly) continue;
                if (field.Name == "Owner" || field.Name == "UniqueID") continue;
                if (field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;
                field.SetValue(clone, field.GetValue(source));
            }
            return clone;
        }

        private static bool HasBridgeTo(MMF_Player chain, MMF_Player target)
        {
            if (chain.FeedbacksList == null) return false;
            for (int i = 0; i < chain.FeedbacksList.Count; i++)
            {
                if (chain.FeedbacksList[i] is MMF_Feedbacks bridge
                    && bridge.Mode == MMF_Feedbacks.Modes.PlayTargetFeedbacks
                    && bridge.TargetFeedbacks == target)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddBridgeIfMissing(
            MMF_Player source, MMF_Player target, string label, StringBuilder report)
        {
            if (HasBridgeTo(source, target))
            {
                report.Append("    ✓ bridge ").Append(source.gameObject.name)
                    .Append(" → ").Append(target.gameObject.name).AppendLine(" already present");
                return;
            }

            Undo.RecordObject(source, "Add MMF_Feedbacks bridge");
            MMF_Feedbacks bridge = (MMF_Feedbacks)source.AddFeedback(typeof(MMF_Feedbacks));
            bridge.Mode = MMF_Feedbacks.Modes.PlayTargetFeedbacks;
            bridge.TargetFeedbacks = target;
            bridge.Label = label;
            EditorUtility.SetDirty(source);
            report.Append("    + bridge added on ").Append(source.gameObject.name)
                .Append(" ").AppendLine(label);
        }

        // Strips any MMF_CanvasGroup feedback in `chain` that targets the
        // given CanvasGroup. Used to undo the alpha=0 INSTANT that the
        // original feedback-filler installed on ReturnButtonDisAppear — it
        // hid the button on the same frame the MMF_Scale exit animation
        // started, so designers never saw the scale-out. Idempotent.
        private static void RemoveCanvasGroupAlphaFeedback(
            MMF_Player chain, CanvasGroup target, StringBuilder report)
        {
            if (chain.FeedbacksList == null) return;

            int removed = 0;
            for (int i = chain.FeedbacksList.Count - 1; i >= 0; i--)
            {
                if (chain.FeedbacksList[i] is MMF_CanvasGroup cg
                    && cg.TargetCanvasGroup == target)
                {
                    Undo.RecordObject(chain, "Remove CanvasGroup alpha feedback");
                    chain.FeedbacksList.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(chain);
                report.Append("    - removed CanvasGroup alpha on ").Append(chain.gameObject.name)
                    .Append(" → ").Append(target.gameObject.name)
                    .Append(" (").Append(removed).AppendLine(" feedback(s)) — scale animation now visible");
            }
            else
            {
                report.Append("    ✓ no CanvasGroup alpha feedback on ").Append(chain.gameObject.name)
                    .AppendLine(" — already clean");
            }
        }

        // Reverse of AddBridgeIfMissing — strips bridges from source that
        // play the given target. Iterates in reverse so removal doesn't
        // invalidate indices. Idempotent: silent no-op if no bridge present.
        private static void UnwireBridgeIfPresent(
            MMF_Player source, MMF_Player target, StringBuilder report)
        {
            if (source.FeedbacksList == null) return;

            int removed = 0;
            for (int i = source.FeedbacksList.Count - 1; i >= 0; i--)
            {
                if (source.FeedbacksList[i] is MMF_Feedbacks bridge
                    && bridge.Mode == MMF_Feedbacks.Modes.PlayTargetFeedbacks
                    && bridge.TargetFeedbacks == target)
                {
                    Undo.RecordObject(source, "Remove MMF_Feedbacks bridge");
                    source.FeedbacksList.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(source);
                report.Append("    - bridge removed: ").Append(source.gameObject.name)
                    .Append(" → ").Append(target.gameObject.name)
                    .Append(" (").Append(removed).AppendLine(" listener(s))");
            }
            else
            {
                report.Append("    ✓ no bridge from ").Append(source.gameObject.name)
                    .Append(" → ").Append(target.gameObject.name).AppendLine(" — already clean");
            }
        }

        // Sets the OnExit MMF on the DrawWorkflowFeelTrigger binding for the
        // given state via SerializedObject. Idempotent: writes only when the
        // current value differs from the target. Creates the binding entry
        // if it doesn't exist yet (with the matching State enum).
        private static void SetBindingOnExit(
            DrawWorkflowFeelTrigger trigger,
            CardDrawWorkflowState state,
            MMF_Player onExit,
            StringBuilder report)
        {
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty bindings = so.FindProperty("bindings");
            if (bindings == null)
            {
                report.AppendLine("    ⚠️  DrawWorkflowFeelTrigger.bindings field not found.");
                return;
            }

            int idx = -1;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("State").enumValueIndex == (int)state)
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                bindings.arraySize += 1;
                idx = bindings.arraySize - 1;
                SerializedProperty fresh = bindings.GetArrayElementAtIndex(idx);
                fresh.FindPropertyRelative("State").enumValueIndex = (int)state;
                fresh.FindPropertyRelative("OnEnter").objectReferenceValue = null;
                fresh.FindPropertyRelative("OnExit").objectReferenceValue = null;
                report.Append("    + Added binding for ").Append(state).AppendLine();
            }

            SerializedProperty target = bindings.GetArrayElementAtIndex(idx);
            SerializedProperty onExitProp = target.FindPropertyRelative("OnExit");
            if (onExitProp.objectReferenceValue == onExit)
            {
                report.Append("    ✓ ").Append(state).Append(".OnExit already wired to ")
                    .AppendLine(onExit.gameObject.name);
            }
            else
            {
                onExitProp.objectReferenceValue = onExit;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(trigger);
                report.Append("    + ").Append(state).Append(".OnExit → ")
                    .AppendLine(onExit.gameObject.name);
            }
        }

        // Generic component finder for the resolve phase.
        private static T? FindComponent<T>(List<GameObject> roots, StringBuilder report) where T : Component
        {
            foreach (GameObject root in roots)
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    report.Append("  ✓ ").Append(typeof(T).Name).Append(": ")
                        .AppendLine(PathOf(found.transform));
                    return found;
                }
            }
            report.Append("  ⚠️  ").Append(typeof(T).Name).AppendLine(" not found.");
            return null;
        }

        // ============================================================
        // Audit helpers.
        // ============================================================
        private static void AuditChain(
            MMF_Player sourceChain,
            MMF_Player? extractedChain,
            GameObject buildButton,
            string label,
            StringBuilder report)
        {
            int residual = 0;
            if (sourceChain.FeedbacksList != null)
            {
                for (int i = 0; i < sourceChain.FeedbacksList.Count; i++)
                {
                    if (sourceChain.FeedbacksList[i] is MMF_Position pos
                        && pos.AnimatePositionTarget == buildButton)
                    {
                        residual++;
                    }
                }
            }

            if (residual > 0)
            {
                report.Append("    ⚠️  ").Append(label).Append(": ").Append(residual)
                    .AppendLine(" BuildButton feedback(s) STILL in source chain — extraction incomplete");
            }
            else
            {
                report.Append("    ✓ ").Append(label).AppendLine(": source chain has no residual BuildButton feedbacks");
            }

            if (extractedChain == null)
            {
                report.Append("    ⚠️  ").Append(label).AppendLine(": extracted chain does not exist");
                return;
            }

            int extracted = 0;
            if (extractedChain.FeedbacksList != null)
            {
                for (int i = 0; i < extractedChain.FeedbacksList.Count; i++)
                {
                    if (extractedChain.FeedbacksList[i] is MMF_Position pos
                        && pos.AnimatePositionTarget == buildButton)
                    {
                        extracted++;
                    }
                }
            }
            report.Append("    ✓ ").Append(label).Append(": extracted chain holds ")
                .Append(extracted).AppendLine(" BuildButton feedback(s)");
        }

        private static void AuditBridge(
            MMF_Player source, MMF_Player? expected, string label, StringBuilder report)
        {
            if (expected == null)
            {
                report.Append("    ⚠️  ").Append(label).AppendLine(" — extracted chain missing");
                return;
            }
            if (HasBridgeTo(source, expected))
            {
                report.Append("    ✓ ").AppendLine(label);
            }
            else
            {
                report.Append("    🚨 ").Append(label).AppendLine(" — bridge missing");
            }
        }

        // Inverse of AuditBridge — succeeds when the bridge is ABSENT (the
        // post-rewire expectation for DrawButton_Disable → SlideOut).
        private static void AuditBridgeAbsent(
            MMF_Player source, MMF_Player? targetChain, string label, StringBuilder report)
        {
            if (targetChain == null) return;
            if (HasBridgeTo(source, targetChain))
            {
                report.Append("    🚨 stale ").AppendLine(label);
            }
            else
            {
                report.Append("    ✓ ").AppendLine(label);
            }
        }

        private static void AuditBindingOnExit(
            DrawWorkflowFeelTrigger? trigger,
            CardDrawWorkflowState state,
            MMF_Player? expected,
            string label,
            StringBuilder report)
        {
            if (trigger == null || expected == null)
            {
                report.Append("    ⚠️  ").Append(label).AppendLine(" — trigger or chain missing");
                return;
            }
            SerializedObject so = new SerializedObject(trigger);
            SerializedProperty bindings = so.FindProperty("bindings");
            if (bindings == null) return;

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty entry = bindings.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("State").enumValueIndex == (int)state)
                {
                    UnityEngine.Object onExit = entry.FindPropertyRelative("OnExit").objectReferenceValue;
                    if (onExit == expected)
                    {
                        report.Append("    ✓ ").AppendLine(label);
                    }
                    else
                    {
                        report.Append("    🚨 ").Append(label).Append(" — points at ")
                            .AppendLine(onExit != null ? onExit.name : "null");
                    }
                    return;
                }
            }
            report.Append("    🚨 ").Append(label).AppendLine(" — no binding for state");
        }

        // ============================================================
        // Misc.
        // ============================================================
        private static void MarkAllDirty(List<GameObject> roots)
        {
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                return;
            }
            HashSet<Scene> done = new HashSet<Scene>();
            foreach (GameObject root in roots)
            {
                if (done.Add(root.scene)) EditorSceneManager.MarkSceneDirty(root.scene);
            }
        }

        private static string PathOf(Transform t)
        {
            if (t.parent == null) return t.name;
            return PathOf(t.parent) + "/" + t.name;
        }
    }
}
