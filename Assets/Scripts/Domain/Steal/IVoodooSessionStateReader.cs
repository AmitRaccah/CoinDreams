#nullable enable

namespace Game.Domain.Steal
{
    /// <summary>Read-only view of voodoo-steal session state for mediator components that must route input based on whether a session is active.</summary>
    public interface IVoodooSessionStateReader
    {
        bool HasActiveSession { get; }
    }
}
