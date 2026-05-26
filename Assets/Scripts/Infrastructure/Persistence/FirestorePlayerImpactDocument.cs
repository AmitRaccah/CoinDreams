using Firebase.Firestore;

namespace Game.Infrastructure.Persistence
{
    // Firestore DTO for steal-impact records.
    // Stored as players/{victimId}/impacts/{impactId} once wired up.
    [FirestoreData]
    public sealed class FirestorePlayerImpactDocument
    {
        [FirestoreProperty("impactId")]
        public string ImpactId { get; set; } = string.Empty;

        [FirestoreProperty("impactType")]
        public int ImpactType { get; set; }

        [FirestoreProperty("amount")]
        public int Amount { get; set; }

        [FirestoreProperty("sourcePlayerId")]
        public string SourcePlayerId { get; set; } = string.Empty;

        [FirestoreProperty("createdAtUtcTicks")]
        public long CreatedAtUtcTicks { get; set; }
    }
}
