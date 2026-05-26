using UnityEngine;
using Game.Domain.Minigames;

namespace Game.Runtime.Minigames
{
    public sealed class LoggingMinigameLauncher : IMinigameLauncher
    {
        public void Launch(string minigameId)
        {
            Debug.Log("[Minigame] Launch requested: " + minigameId);
        }
    }
}
