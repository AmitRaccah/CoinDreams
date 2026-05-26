namespace Game.Runtime.Bootstrap
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    public abstract class BootstrapStepAsset : ScriptableObject, IBootstrapStep
    {
        [SerializeField] private string displayName = "Step";
        [SerializeField, Min(0.01f)] private float weight = 1f;

        public string DisplayName
        {
            get { return displayName; }
        }

        public float Weight
        {
            get { return weight; }
        }

        public abstract UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken);
    }
}
