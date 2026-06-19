#nullable enable

using System;
using System.Collections.Generic;

namespace Game.Runtime.UI.Context
{
    /// <summary>
    /// Owns the active set of UI tags. Writers (panel/steal/camera publishers)
    /// flip individual tags on and off via <see cref="SetTag"/>; readers
    /// (UiTaggedVisibility binders) subscribe to <see cref="TagsChanged"/> and
    /// re-evaluate. Single source of truth — no scattered "is X happening?"
    /// booleans across presenters.
    ///
    /// SRP: only state + fan-out. Tag MEANING (what triggers it) belongs in
    /// the publishers; tag REACTION (what to do about it) belongs in the
    /// binders.
    /// </summary>
    public sealed class UiContextService : IUiContext
    {
        private readonly HashSet<string> tags = new HashSet<string>();

        public bool HasTag(string tag)
        {
            return !string.IsNullOrEmpty(tag) && tags.Contains(tag);
        }

        public IReadOnlyCollection<string> ActiveTags => tags;

        public event Action? TagsChanged;

        /// <summary>
        /// Idempotent: adds tag when active=true, removes when active=false.
        /// Fires <see cref="TagsChanged"/> only when the set actually changed,
        /// so repeated SetTag(x, true) calls don't spam binders.
        /// </summary>
        public void SetTag(string tag, bool active)
        {
            if (string.IsNullOrEmpty(tag)) return;
            bool changed = active ? tags.Add(tag) : tags.Remove(tag);
            if (changed) TagsChanged?.Invoke();
        }
    }
}
