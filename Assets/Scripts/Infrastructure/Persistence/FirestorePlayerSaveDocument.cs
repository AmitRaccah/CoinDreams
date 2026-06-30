using System;
using System.Collections.Generic;
using Firebase.Firestore;

namespace Game.Infrastructure.Persistence
{
    [FirestoreData]
    public sealed class FirestorePlayerSaveDocument
    {
        public const int CurrentSchemaVersion = 3;

        [FirestoreProperty("playerId")]
        public string PlayerId { get; set; } = string.Empty;

        [FirestoreProperty("revision")]
        public int Revision { get; set; }

        [FirestoreProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [FirestoreProperty("coins")]
        public int Coins { get; set; }

        [FirestoreProperty("currentEnergy")]
        public int CurrentEnergy { get; set; }

        [FirestoreProperty("maxEnergy")]
        public int MaxEnergy { get; set; }

        [FirestoreProperty("regenIntervalSeconds")]
        public int RegenIntervalSeconds { get; set; }

        [FirestoreProperty("lastRegenUtcTicks")]
        public long LastRegenUtcTicks { get; set; }

        // Server-side helpers (executeDraw / executeSteal / executeVoodooStab)
        // read and write these field names directly. Without these properties
        // the Firestore SDK logs "No writable property for Firestore field …"
        // every read and silently drops the values, so client-side shields
        // never reflect server-side state.
        [FirestoreProperty("regenMaxEnergy")]
        public int RegenMaxEnergyServer { get; set; }

        [FirestoreProperty("shields")]
        public int Shields { get; set; }

        [FirestoreProperty("maxShields")]
        public int MaxShields { get; set; }

        [FirestoreProperty("villageLevels")]
        public List<int> VillageLevels { get; set; } = new List<int>(0);

        // Stage-progression counter. Server-authoritative (advanceStage); the
        // client only reads it. Missing on pre-feature docs → defaults to 0.
        [FirestoreProperty("currentStage")]
        public int CurrentStage { get; set; }

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
                SchemaVersion = CurrentSchemaVersion,
                Coins = saveData.coins,
                CurrentEnergy = saveData.currentEnergy,
                MaxEnergy = saveData.maxEnergy,
                // Mirror to the server's field name so legacy doc writers
                // (this class) and the server agree on the value.
                RegenMaxEnergyServer = saveData.maxEnergy,
                Shields = saveData.shields,
                MaxShields = saveData.maxShields,
                RegenIntervalSeconds = saveData.regenIntervalSeconds,
                LastRegenUtcTicks = saveData.lastRegenUtcTicks,
                VillageLevels = ToIntList(saveData.villageLevels),
                CurrentStage = saveData.currentStage,
                ProcessedImpactIds = ToStringList(saveData.processedImpactIds),
                UpdatedAtUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        public PlayerSaveData ToSaveData()
        {
            PlayerSaveData saveData = new PlayerSaveData();
            saveData.playerId = PlayerId ?? string.Empty;
            saveData.revision = Revision;
            saveData.schemaVersion = SchemaVersion;
            saveData.coins = Coins;
            saveData.currentEnergy = CurrentEnergy;
            // Prefer the server-written regenMaxEnergy if present; fall back
            // to the legacy maxEnergy field for documents written before the
            // schema migration.
            saveData.maxEnergy = RegenMaxEnergyServer > 0 ? RegenMaxEnergyServer : MaxEnergy;
            saveData.regenIntervalSeconds = RegenIntervalSeconds;
            saveData.lastRegenUtcTicks = LastRegenUtcTicks;
            saveData.shields = Shields;
            saveData.maxShields = MaxShields;
            saveData.villageLevels = ToIntArray(VillageLevels);
            saveData.currentStage = CurrentStage;
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
