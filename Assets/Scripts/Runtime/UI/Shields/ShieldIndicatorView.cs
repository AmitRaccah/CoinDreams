#nullable enable

using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game.Runtime.UI.Shields
{
    // Drives the appear polish on a single shield indicator. Owns its own
    // MMF_Player so ShieldsHudPresenter doesn't need to know about Feel —
    // the presenter still just toggles GameObject.SetActive(true/false)
    // based on the player's shield count, and this component fires the
    // animation on the inactive→active transition.
    //
    // SRP: presenter = "which indicators are active"; this view =
    // "what visually happens when I appear". Splitting like this keeps
    // Feel-coupling local to the prefab and the presenter dependency-free.
    //
    // First-activation suppression: when the presenter spawns the prefab
    // pool, each indicator briefly enters active state before being
    // clamped by SetActive(false) to match the current shield count.
    // Without a guard, that initial OnEnable fires the Feel chain on
    // scene load — N "shield appears" flashes for shields the player
    // already had. The firstEnableConsumed flag absorbs that first
    // OnEnable so only the next inactive→active flip — a real shield
    // being earned — plays.
    [DisallowMultipleComponent]
    public sealed class ShieldIndicatorView : MonoBehaviour
    {
        [Tooltip("Feel chain played when this indicator transitions from " +
            "inactive to active (i.e. the player just earned this shield). " +
            "Null = no polish; the indicator silently snaps on, matching " +
            "pre-Feel behavior.")]
        [SerializeField] private MMF_Player? appearFeedbacks;

        private bool firstEnableConsumed;

        private void OnEnable()
        {
            // Skip the first OnEnable (pool bootstrap, see comment above).
            // After that, every inactive→active flip plays the polish.
            if (!firstEnableConsumed)
            {
                firstEnableConsumed = true;
                return;
            }
            appearFeedbacks?.PlayFeedbacks();
        }
    }
}
