using System.Threading.Tasks;
using Game.Runtime.Cameras;
using UnityEngine;

namespace Game.Runtime.Cards
{
    public interface ICameraTransitionService
    {
        bool IsTransitioning { get; }
        CameraPose CurrentPose { get; }
        Task StartTransitionAsync(Transform destination);
        Task StartTransitionAsync(CameraPose destination);
    }
}
