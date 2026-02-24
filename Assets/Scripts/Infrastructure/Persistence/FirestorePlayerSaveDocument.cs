using System;
using System.Collections.Generic;
using Firebase.Firestore;

namespace Game.Infrastructure.Persistence
{
    [FirestoreData]
    public sealed class FirestorePlayerSaveDocument
    {
        [FirestoreProperty("playerId")]
        public string PlayerId { get; set; } = string.Empty;

        [FirestoreProperty("revision")]
        public int Revision { get; set; }

        [FirestoreProperty("coins")]
        public int Coins { get; set; }

        [FirestoreProperty("currentEnergy")]
        public int CurrentEnergy { get; set; }

        [FirestoreProperty("maxEnergy")]
        public int MaxEnergy { get; set; }

        [FirestoreProperty("storageMaxEnergy")]
        public int StorageMaxEnergy { get; set; }

        [FirestoreProperty("regenIntervalSeconds")]
        public int RegenIntervalSeconds { get; set; }

        [FirestoreProperty("lastRegenUtcTicks")]
        public long LastRegenUtcTicks { get; set; }

        [FirestoreProperty("villageLevels")]
        public List<int> VillageLevels { get; set; } = new List<int>(0);

        [FirestoreProperty("processedImpactIds")]
        public List<string> ProcessedImpactIds { get; set; } = new List<string>(0);

        [FirestoreProperty("updatedAtUtcTicks")]
        public long UpdatedAtUtcTicks { get; set; }

        public static FirestorePlayerSaveDocument FromSaveData(PlayerSaveData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException("saveData");
            }

            return new FirestorePlayerSaveDocument
            {
                PlayerId = saveData.playerId ?? string.Empty,
                Revision = saveData.revision,
                Coins = saveData.coins,
                CurrentEnergy = saveData.currentEnergy,
                MaxEnergy = saveData.maxEnergy,
                StorageMaxEnergy = saveData.storageMaxEnergy,
                RegenIntervalSeconds = saveData.regenIntervalSeconds,
                LastRegenUtcTicks = saveData.lastRegenUtcTicks,
                VillageLevels = ToIntList(saveData.villageLevels),
                ProcessedImpactIds = ToStringList(saveData.processedImpactIds),
                UpdatedAtUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        public PlayerSaveData ToSaveData()
        {
            PlayerSaveData saveData = new PlayerSaveData();
            saveData.playerId = PlayerId ?? string.Empty;
            saveData.revision = Revision;
            saveData.coins = Coins;
            saveData.currentEnergy = CurrentEnergy;
            saveData.maxEnergy = MaxEnergy;
            saveData.storageMaxEnergy = StorageMaxEnergy;
            saveData.regenIntervalSeconds = RegenIntervalSeconds;
            saveData.lastRegenUtcTicks = LastRegenUtcTicks;
            saveData.villageLevels = ToIntArray(VillageLevels);
            saveData.processedImpactIds = ToStringArray(ProcessedImpactIds);
            return saveData;
        }

        private static List<int> ToIntList(int[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new List<int>(0);
            }

            List<int> result = new List<int>(source.Length);
            int i;
            for (i = 0; i < source.Length; i++)
            {
                result.Add(source[i]);
            }

            return result;
        }

        private static int[] ToIntArray(List<int> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<int>();
            }

            return source.ToArray();
        }

        private static List<string> ToStringList(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new List<string>(0);
            }

            List<string> result = new List<string>(source.Length);
            int i;
            for (i = 0; i < source.Length; i++)
            {
                result.Add(source[i] ?? string.Empty);
            }

            return result;
        }

        private static string[] ToStringArray(List<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            return source.ToArray();
        }
    }
}
