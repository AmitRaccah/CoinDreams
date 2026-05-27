#nullable enable

using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    [CreateAssetMenu(menuName = "CoinDreams/Persistence/Persistence Settings", fileName = "PersistenceSettings")]
    public sealed class PersistenceSettings : ScriptableObject
    {
        [Header("Flow")]
        [SerializeField] private bool autoLoadOnStart = true;
        [SerializeField] private bool autoSave = true;
        [SerializeField] private float autosaveIntervalSeconds = 0.25f;
        [SerializeField] private bool saveOnApplicationPause = true;
        [SerializeField] private bool saveOnApplicationQuit = true;
        [SerializeField] private bool createRemoteDocumentIfMissing = true;
        [SerializeField] private bool verboseLogging = true;

        [Header("Local Cache")]
        [SerializeField] private bool useLocalCache = true;
        [SerializeField] private string localCacheFileName = "player_save_cache.json";

        [Header("Debug / Testing")]
        [SerializeField] private bool clearLocalCacheOnStart;
        [SerializeField] private bool forceFreshAnonymousIdentityOnStart;

        [Header("Firestore")]
        [SerializeField] private string playersCollectionName = "players";

        public bool AutoLoadOnStart => autoLoadOnStart;
        public bool AutoSave => autoSave;
        public float AutosaveIntervalSeconds => autosaveIntervalSeconds;
        public bool SaveOnApplicationPause => saveOnApplicationPause;
        public bool SaveOnApplicationQuit => saveOnApplicationQuit;
        public bool CreateRemoteDocumentIfMissing => createRemoteDocumentIfMissing;
        public bool VerboseLogging => verboseLogging;
        public bool UseLocalCache => useLocalCache;
        public string LocalCacheFileName => localCacheFileName;
        public bool ClearLocalCacheOnStart => clearLocalCacheOnStart;
        public bool ForceFreshAnonymousIdentityOnStart => forceFreshAnonymousIdentityOnStart;
        public string PlayersCollectionName => playersCollectionName;
    }
}
