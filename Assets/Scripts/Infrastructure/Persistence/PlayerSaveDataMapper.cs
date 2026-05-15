using System;
using Game.Domain.Player;

namespace Game.Infrastructure.Persistence
{
    public static class PlayerSaveDataMapper
    {
        public static PlayerSaveData ToSaveData(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            PlayerSaveData saveData = new PlayerSaveData();
            saveData.playerId = snapshot.playerId;
            saveData.revision = snapshot.revision;
            saveData.coins = snapshot.coins;
            saveData.currentEnergy = snapshot.currentEnergy;
            saveData.maxEnergy = snapshot.regenMaxEnergy;
            saveData.storageMaxEnergy = snapshot.storageMaxEnergy;
            saveData.regenIntervalSeconds = snapshot.regenIntervalSeconds;
            saveData.lastRegenUtcTicks = snapshot.lastRegenUtcTicks;
            saveData.villageLevels = CopyIntArray(snapshot.villageLevels);
            saveData.processedImpactIds = CopyStringArray(snapshot.processedImpactIds);
            return saveData;
        }

        public static PlayerProfileSnapshot ToSnapshot(PlayerSaveData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException("saveData");
            }

            PlayerProfileSnapshot snapshot = new PlayerProfileSnapshot();
            snapshot.playerId = saveData.playerId;
            snapshot.revision = saveData.revision;
            snapshot.coins = saveData.coins;
            snapshot.currentEnergy = saveData.currentEnergy;
            snapshot.regenMaxEnergy = saveData.maxEnergy;
            snapshot.storageMaxEnergy = saveData.storageMaxEnergy;
            snapshot.regenIntervalSeconds = saveData.regenIntervalSeconds;
            snapshot.lastRegenUtcTicks = saveData.lastRegenUtcTicks;
            snapshot.villageLevels = CopyIntArray(saveData.villageLevels);
            snapshot.processedImpactIds = CopyStringArray(saveData.processedImpactIds);
            return snapshot;
        }

        private static int[] CopyIntArray(int[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<int>();
            }

            int[] copy = new int[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static string[] CopyStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            string[] copy = new string[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}
