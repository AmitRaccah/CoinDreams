#if UNITY_EDITOR
#nullable enable

using System;
using System.Collections.Generic;
using Game.Runtime.UI.Animations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor menu that wires the entire crystal ball UI in one pass on the
    /// selected GameObject:
    ///   1. Walks descendant Images and applies an ambient "shimmer" preset
    ///      to each sprite layer (rotate / pulse / alpha / bob) based on its
    ///      Image.sprite.name.
    ///   2. Reserves the outer_glow layer for a triggered effect: strips any
    ///      ambient components off it, disables it at edit-time, and stores
    ///      a reference.
    ///   3. Attaches UiPressScalePunch + UiPressGlowBurst to the selected
    ///      root so every PointerDown produces tactile feedback regardless
    ///      of whether the underlying Button.onClick is gated downstream.
    ///   4. Wires the reserved outer glow as the burst's target.
    ///
    /// SOLID note: the wizard is composition-only. It picks which existing
    /// SRP component goes where. Adding a new sprite layer = add another
    /// case to ApplyPreset; the animation components stay untouched.
    /// </summary>
    internal static class CrystalBallShimmerWizard
    {
        [MenuItem("CoinDreams/UI/Apply Crystal Ball Shimmer Preset")]
        private static void Apply()
        {
            GameObject? selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Crystal Ball Shimmer",
                    "Select the crystal ball Button (the GameObject that should receive press feedback). The wizard searches its descendants for the sprite layers.",
                    "OK");
                return;
            }

            int ambientTouched = 0;
            int skippedDisabled = 0;
            GameObject? outerGlowLayer = null;
            List<RectTransform> staticAnchors = new List<RectTransform>();

            // Recursive search so the wizard works whether the sprite layers
            // are direct children of the Button (flat) or wrapped in a
            // container GameObject (nested).
            Image[] descendantImages = selected.GetComponentsInChildren<Image>(includeInactive: true);
            foreach (Image image in descendantImages)
            {
                // The selected GameObject is the press-feedback host — never
                // treat its own Image (if any) as an animated sprite layer.
                if (image.gameObject == selected) continue;
                if (image.sprite == null) continue;

                GameObject layer = image.gameObject;
                string spriteName = image.sprite.name;

                // The outer glow is reserved for the press burst. Capture
                // it, strip any pre-existing ambient components that would
                // fight the burst over alpha, and force it off at edit time
                // so the scene view matches the runtime "off at rest" state.
                if (spriteName.Contains("outer_glow"))
                {
                    outerGlowLayer = layer;
                    StripAmbientComponents(layer);
                    if (layer.activeSelf)
                    {
                        Undo.RecordObject(layer, "Reserve outer glow for press burst");
                        layer.SetActive(false);
                    }
                    continue;
                }

                // Pedestal is the "base" the player conceptually holds —
                // it stays visually still while the ball above pulses. We
                // queue it for UiPressScalePunch.excludedFromScale; inverse
                // scaling cancels the press squish for it each frame.
                if (spriteName.Contains("pedestal"))
                {
                    if (layer.activeSelf)
                    {
                        staticAnchors.Add(image.rectTransform);
                    }
                    continue;
                }

                if (!layer.activeSelf)
                {
                    skippedDisabled++;
                    continue;
                }

                if (ApplyPreset(layer, spriteName))
                {
                    ambientTouched++;
                }
            }

            // Press feedback + shake intensity attach to the selected root.
            // Pointer events from hits on any descendant Graphic bubble up to
            // it, so press scale + glow burst + shake-intensity charge fire
            // on every tap regardless of which sprite layer was hit. The
            // shake provider is queried by the descendant rotators/pulsers/
            // bobbers via GetComponentInParent<IUiAnimationSpeedModulator>
            // at their Awake — placing it on the root means EVERY inner
            // layer reacts to taps automatically.
            UiPressScalePunch punch = AddOrGet<UiPressScalePunch>(selected);
            UiPressGlowBurst burst = AddOrGet<UiPressGlowBurst>(selected);
            AddOrGet<UiShakeIntensityProvider>(selected);
            if (outerGlowLayer != null)
            {
                SetObjectField(burst, "glowTarget", outerGlowLayer);
            }
            if (staticAnchors.Count > 0)
            {
                // Appends with dedup — preserves any manual additions the
                // user dragged in before re-running the wizard.
                AppendToReferenceArrayField(punch, "excludedFromScale", staticAnchors.ToArray());
            }

            EditorUtility.SetDirty(selected);

            string glowLine = outerGlowLayer != null
                ? "Wired '" + outerGlowLayer.name + "' as glow burst target (disabled at rest)."
                : "WARNING: no 'outer_glow' sprite found in descendants — glow burst added but UNWIRED.";

            string anchorLine = staticAnchors.Count > 0
                ? "Pinned " + staticAnchors.Count + " pedestal layer(s) into 'Excluded From Scale' (stay visually static during press)."
                : "No pedestal layer detected — nothing pinned to 'Excluded From Scale'.";

            EditorUtility.DisplayDialog(
                "Crystal Ball Shimmer",
                "Ambient: applied to " + ambientTouched + " layer(s).\n" +
                "Skipped " + skippedDisabled + " disabled layer(s).\n\n" +
                "Press feedback: UiPressScalePunch + UiPressGlowBurst + UiShakeIntensityProvider added to '" + selected.name + "'.\n" +
                glowLine + "\n" +
                anchorLine + "\n\n" +
                "Tap rapidly to charge shake intensity — inner layers spin/pulse/bob faster the harder you press.",
                "OK");
        }

        // Removes ambient animation components from a layer so they don't
        // compete with a triggered effect. Example: the press glow burst
        // owns the alpha of the outer glow Image; an ambient UiAlphaPulser
        // sitting there would race it every frame.
        private static void StripAmbientComponents(GameObject layer)
        {
            UiAlphaPulser pulser = layer.GetComponent<UiAlphaPulser>();
            if (pulser != null) Undo.DestroyObjectImmediate(pulser);

            UiPulseScaler scaler = layer.GetComponent<UiPulseScaler>();
            if (scaler != null) Undo.DestroyObjectImmediate(scaler);

            UiContinuousRotator rotator = layer.GetComponent<UiContinuousRotator>();
            if (rotator != null) Undo.DestroyObjectImmediate(rotator);

            UiPositionBobber bobber = layer.GetComponent<UiPositionBobber>();
            if (bobber != null) Undo.DestroyObjectImmediate(bobber);
        }

        private static bool ApplyPreset(GameObject layer, string spriteName)
        {
            // Substring match so renamed sprites like "01_outer_glow (1)"
            // still resolve to the right preset.
            if (spriteName.Contains("outer_glow"))
            {
                ConfigureAlphaPulser(layer, freq: 0.25f, min: 0.55f, max: 1f, phase: 0f);
                ConfigurePulseScaler(layer, freq: 0.25f, amp: 0.03f, phase: 0f);
                return true;
            }

            if (spriteName.Contains("glow_core"))
            {
                // Heartbeat-like core.
                ConfigurePulseScaler(layer, freq: 0.6f, amp: 0.06f, phase: 0.1f);
                ConfigureAlphaPulser(layer, freq: 0.6f, min: 0.75f, max: 1f, phase: 0.1f);
                return true;
            }

            if (spriteName.Contains("mist_a"))
            {
                ConfigureContinuousRotator(layer, dps: 3f);
                ConfigureAlphaPulser(layer, freq: 0.18f, min: 0.5f, max: 0.9f, phase: 0f);
                return true;
            }

            if (spriteName.Contains("mist_b"))
            {
                // Opposite direction + phase offset so the two mists never
                // line up — that's what sells "swirling smoke."
                ConfigureContinuousRotator(layer, dps: -4.5f);
                ConfigureAlphaPulser(layer, freq: 0.22f, min: 0.45f, max: 0.85f, phase: 0.5f);
                return true;
            }

            if (spriteName.Contains("sparkles"))
            {
                // shakeBoost: at multiplier 6 (max-charged shake), sparkle
                // amplitude grows 1 + 0.5*5 = 3.5×. They fly chaotically
                // instead of just bobbing faster — the snow-globe particle
                // feel.
                ConfigurePositionBobber(layer, freq: 0.7f, amplitudeY: 3f, phase: 0f, shakeBoost: 0.5f);
                ConfigureAlphaPulser(layer, freq: 1.1f, min: 0.3f, max: 1f, phase: 0.25f);
                return true;
            }

            // pedestal, glass_back, glass_front, highlights — intentionally static.
            return false;
        }

        // The Configure* helpers exist because each component has a small,
        // distinct set of fields. Reflection-via-string-name would be tighter
        // but less type-safe; explicit helpers fail at compile time if a
        // field is renamed.
        private static void ConfigureContinuousRotator(GameObject layer, float dps)
        {
            UiContinuousRotator component = AddOrGet<UiContinuousRotator>(layer);
            SetFloatField(component, "degreesPerSecond", dps);
        }

        private static void ConfigurePulseScaler(GameObject layer, float freq, float amp, float phase)
        {
            UiPulseScaler component = AddOrGet<UiPulseScaler>(layer);
            SetFloatField(component, "frequencyHz", freq);
            SetFloatField(component, "amplitude", amp);
            SetFloatField(component, "phaseOffset01", phase);
        }

        private static void ConfigureAlphaPulser(GameObject layer, float freq, float min, float max, float phase)
        {
            UiAlphaPulser component = AddOrGet<UiAlphaPulser>(layer);
            SetFloatField(component, "frequencyHz", freq);
            SetFloatField(component, "minAlpha", min);
            SetFloatField(component, "maxAlpha", max);
            SetFloatField(component, "phaseOffset01", phase);
        }

        private static void ConfigurePositionBobber(GameObject layer, float freq, float amplitudeY, float phase, float shakeBoost = 0f)
        {
            UiPositionBobber component = AddOrGet<UiPositionBobber>(layer);
            SetFloatField(component, "frequencyHz", freq);
            SetVector2Field(component, "amplitude", new Vector2(0f, amplitudeY));
            SetFloatField(component, "phaseOffset01", phase);
            SetFloatField(component, "shakeAmplitudeBoost", shakeBoost);
        }

        private static T AddOrGet<T>(GameObject host) where T : Component
        {
            T existing = host.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(host);
        }

        private static void SetFloatField(Component target, string fieldName, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[CrystalBallShimmerWizard] " + target.GetType().Name + " has no serialized field '" + fieldName + "'.");
                return;
            }
            prop.floatValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetVector2Field(Component target, string fieldName, Vector2 value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[CrystalBallShimmerWizard] " + target.GetType().Name + " has no serialized field '" + fieldName + "'.");
                return;
            }
            prop.vector2Value = value;
            so.ApplyModifiedProperties();
        }

        private static void SetObjectField(Component target, string fieldName, UnityEngine.Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[CrystalBallShimmerWizard] " + target.GetType().Name + " has no serialized field '" + fieldName + "'.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        // Appends references to an array SerializedProperty with dedup
        // against the values already present. The wizard uses this so
        // re-running it doesn't duplicate the auto-pinned pedestal entries,
        // and any references the user dragged in manually are preserved.
        private static void AppendToReferenceArrayField(Component target, string fieldName, UnityEngine.Object[] toAppend)
        {
            if (toAppend == null || toAppend.Length == 0) return;

            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning("[CrystalBallShimmerWizard] " + target.GetType().Name + " has no array field '" + fieldName + "'.");
                return;
            }

            HashSet<UnityEngine.Object> existing = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < prop.arraySize; i++)
            {
                UnityEngine.Object current = prop.GetArrayElementAtIndex(i).objectReferenceValue;
                if (current != null) existing.Add(current);
            }

            foreach (UnityEngine.Object item in toAppend)
            {
                if (item == null) continue;
                if (existing.Contains(item)) continue;
                int newIndex = prop.arraySize;
                prop.InsertArrayElementAtIndex(newIndex);
                prop.GetArrayElementAtIndex(newIndex).objectReferenceValue = item;
                existing.Add(item);
            }

            so.ApplyModifiedProperties();
        }
    }
}
#endif
