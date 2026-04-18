using System.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Cards
{
    public interface ICameraTransitionService
    {
        bool IsTransitioning { get; }
        Task StartTransitionAsync(Transform destination);
    }
}
