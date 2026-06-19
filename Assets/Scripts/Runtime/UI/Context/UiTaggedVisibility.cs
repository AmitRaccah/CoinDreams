#nullable enable

using System;
using UnityEngine;
using VContainer;

namespace Game.Runtime.UI.Context
{
    /// <summary>
    /// Drop on any UI GameObject to make its visibility a function of the
    /// active <see cref="IUiContext"/> tags. Three orthogonal rules — order
    /// matters: a hit on "hide" always wins; otherwise both "require" sets
    /// must pass for the element to be visible.
    ///
    /// Wire-up: place the component on the element that should toggle. The
    /// element MUST start enabled in the scene (otherwise Awake / Update
    /// never run and the binder never subscribes). After the first context
    /// change the binder may SetActive(false) it — the C# event subscription
    /// stays alive even while the MonoBehaviour is disabled, so it can wake
    /// itself back up.
    ///
    /// SRP: pure rule evaluator + SetActive. Doesn't know which signal
    /// flipped which tag, doesn't know what its target represents.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiTaggedVisibility : MonoBehaviour
    {
        [Header("Hide rule (any-match wins)")]
        [Tooltip("If ANY tag in this list is currently active, hide the GameObject.")]
        [SerializeField] private string[] hideWhenAnyTagActive = Array.Empty<string>();

        [Header("Require-any rule (OR)")]
        [Tooltip("If this list is non-empty, at least ONE tag must be active for the GameObject to show.")]
        [SerializeField] private string[] requireAnyTagActive = Array.Empty<string>();

        [Header("Require-all rule (AND)")]
        [Tooltip("If this list is non-empty, EVERY tag must be active for the GameObject to show.")]
        [SerializeField] private string[] requireAllTagsActive = Array.Empty<string>();

        [Inject] private IUiContext? context;

        private bool subscribed;

        // Subscription deferred to Update because the [Inject] field is null
        // during Awake/OnEnable when the container build hasn't reached us
        // yet (cross-scope timing — late injection via Update polling).
        private void Update()
        {
            if (subscribed) return;
            if (context == null) return;
            context.TagsChanged += Evaluate;
            subscribed = true;
            Evaluate();
        }

        private void OnDestroy()
        {
            // Deliberately NOT unsubscribed in OnDisable: SetActive(false) on
            // a successful hide-rule match disables this MonoBehaviour, and
            // if we dropped the subscription there, the binder would never
            // wake up when a later tag change should make it visible again.
            // OnDestroy is the only safe drop point.
            if (subscribed && context != null)
            {
                context.TagsChanged -= Evaluate;
                subscribed = false;
            }
        }

        private void Evaluate()
        {
            if (context == null) return;

            bool visible = ComputeVisibility();
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        private bool ComputeVisibility()
        {
            // Rule 1 — hide takes precedence.
            for (int i = 0; i < hideWhenAnyTagActive.Length; i++)
            {
                if (context!.HasTag(hideWhenAnyTagActive[i])) return false;
            }

            // Rule 2 — at least one require-any tag must be active (when configured).
            if (requireAnyTagActive.Length > 0)
            {
                bool anyActive = false;
                for (int i = 0; i < requireAnyTagActive.Length; i++)
                {
                    if (context!.HasTag(requireAnyTagActive[i])) { anyActive = true; break; }
                }
                if (!anyActive) return false;
            }

            // Rule 3 — every require-all tag must be active (when configured).
            for (int i = 0; i < requireAllTagsActive.Length; i++)
            {
                if (!context!.HasTag(requireAllTagsActive[i])) return false;
            }

            return true;
        }
    }
}
