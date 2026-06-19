#nullable enable

using System;
using System.Collections.Generic;

namespace Game.Runtime.UI.Context
{
    /// <summary>
    /// Read-only view of the active UI context — a set of string tags that
    /// describe "what the player is currently doing" (e.g. "panel-open",
    /// "steal-session", "camera-board"). UI elements query this via the
    /// <see cref="UiTaggedVisibility"/> binder to decide whether to show
    /// themselves; publishers (separate components) write to the underlying
    /// <see cref="UiContextService"/> so consumers and producers stay
    /// decoupled.
    /// </summary>
    public interface IUiContext
    {
        bool HasTag(string tag);
        IReadOnlyCollection<string> ActiveTags { get; }
        event Action TagsChanged;
    }
}
