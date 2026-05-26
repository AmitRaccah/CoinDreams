namespace Game.Runtime.Scenes
{
    using System;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class GameplaySceneAnchors : MonoBehaviour, IGameplaySceneAnchors
    {
        [SerializeField] private Transform cardBoardAnchor;
        [SerializeField] private Transform cityViewAnchor;
        [SerializeField] private Camera mainCamera;

        public static IGameplaySceneAnchors Current { get; private set; }

        public static event Action<IGameplaySceneAnchors> AnchorsAvailable;

        public Transform CardBoardAnchor
        {
            get { return cardBoardAnchor; }
        }

        public Transform CityViewAnchor
        {
            get { return cityViewAnchor; }
        }

        public Camera MainCamera
        {
            get { return mainCamera; }
        }

        private void Awake()
        {
            if (Current != null && !ReferenceEquals(Current, this))
            {
                Debug.LogWarning(
                    "[GameplaySceneAnchors] Multiple GameplaySceneAnchors found. The newest instance is now Current.",
                    this);
            }

            Current = this;

            Action<IGameplaySceneAnchors> handler = AnchorsAvailable;
            if (handler != null)
            {
                handler(this);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }
    }
}
