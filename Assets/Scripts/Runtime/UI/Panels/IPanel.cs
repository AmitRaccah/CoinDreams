#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Runtime.UI.Panels
{
    /// <summary>
    /// A UI panel that participates in the navigator's mutex (only one
    /// active at a time). Today Show/Hide just toggle SetActive; tomorrow
    /// they can await fade-in / scale-bounce / slide animations without
    /// any caller-side change.
    /// </summary>
    public interface IPanel
    {
        /// <summary>
        /// Stable key used by <c>PanelOpenRequestedSignal</c> and by the
        /// navigator's lookup. Must be unique across registered panels.
        /// </summary>
        string PanelKey { get; }

        /// <summary>
        /// Bring this panel forward. Implementations should be idempotent
        /// (called on an already-visible panel = no-op).
        /// </summary>
        UniTask ShowAsync(CancellationToken ct);

        /// <summary>
        /// Send this panel away. Idempotent.
        /// </summary>
        UniTask HideAsync(CancellationToken ct);
    }
}
