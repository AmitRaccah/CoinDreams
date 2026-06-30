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
            saveData.schemaVersion = FirestorePlayerSaveDocument.CurrentSchemaVersion;
            saveData.coins = snapshot.coins;
            saveData.currentEnergy = snapshot.currentEnergy;
            saveData.maxEnergy = snapshot.regenMaxEnergy;
            saveData.regenIntervalSeconds = snapshot.regenIntervalSeconds;
            saveData.lastRegenUtcTicks = snapshot.lastRegenUtcTicks;
            saveData.shields = snapshot.shields;
            saveData.maxShields = snapshot.maxShields;
            saveData.villageLevels = CopyIntArray(snapshot.villageLevels);
            saveData.currentStage = snapshot.currentStage;
            saveData.processedImpactIds = CopyStringArray(snapshot.processedImpactIds);
            return saveData;
        }

        public static PlayerProfileSnapshot ToSnapshot(PlayerSaveData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException("saveData");
            }

            // Schema version is read for logging only; no migration logic yet.
            if (saveData.schemaVersion != 0
                && saveData.schemaVersion != FirestorePlayerSaveDocument.CurrentSchemaVersion)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[PlayerSaveDataMapper] Schema version mismatch. saved="
                    + saveData.schemaVersion
                    + " current="
                    + FirestorePlayerSaveDocument.CurrentSchemaVersion);
            }

            PlayerProfileSnapshot snapshot = new PlayerProfileSnapshot();
            snapshot.playerId = saveData.playerId;
            snapshot.revision = saveData.revision;
            snapshot.coins = saveData.coins;
            snapshot.currentEnergy = saveData.currentEnergy;
            snapshot.regenMaxEnergy = saveData.maxEnergy;
            snapshot.regenIntervalSeconds = saveData.regenIntervalSeconds;
            snapshot.lastRegenUtcTicks = saveData.lastRegenUtcTicks;
            snapshot.shields = saveData.shields;
            snapshot.maxShields = saveData.maxShields;
            snapshot.villageLevels = CopyIntArray(saveData.villageLevels);
            snapshot.currentStage = saveData.currentStage;
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
