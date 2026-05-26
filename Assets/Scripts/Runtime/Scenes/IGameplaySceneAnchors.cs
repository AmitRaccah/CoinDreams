namespace Game.Runtime.Scenes
{
    using UnityEngine;

    public interface IGameplaySceneAnchors
    {
        Transform CardBoardAnchor { get; }
        Transform CityViewAnchor { get; }
        Camera MainCamera { get; }
    }
}
