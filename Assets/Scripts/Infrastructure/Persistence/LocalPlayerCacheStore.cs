using System;
using System.IO;
using Game.Domain.Player;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public sealed class LocalPlayerCacheStore
    {
        private const string DefaultCacheFileName = "player_save_cache.json";

        private readonly bool isEnabled;
        private readonly string cachePath;

        private LocalPlayerCacheStore(bool isEnabled, string cachePath)
        {
            this.isEnabled = isEnabled;
            this.cachePath = cachePath ?? string.Empty;
        }

        public bool IsEnabled
        {
            get { return isEnabled; }
        }

        public string CachePath
        {
            get { return cachePath; }
        }

        public static LocalPlayerCacheStore Create(bool enabled, string fileName)
        {
            if (!enabled)
            {
                return new LocalPlayerCacheStore(false, string.Empty);
            }

            string sanitizedFileName = SanitizeFileName(fileName);
            string path = Path.Combine(Application.persistentDataPath, sanitizedFileName);
            return new LocalPlayerCacheStore(true, path);
        }

        public bool TryLoadSnapshot(out PlayerProfileSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;

            if (!isEnabled)
            {
                return false;
            }

            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(cachePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                PlayerSaveData saveData = JsonUtility.FromJson<PlayerSaveData>(json);
                if (saveData == null)
                {
                    error = "Cache file is empty or malformed.";
                    return false;
                }

                snapshot = PlayerSaveDataMapper.ToSnapshot(saveData);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryPersistSnapshot(PlayerProfileSnapshot snapshot, out string error)
        {
            error = string.Empty;

            if (!isEnabled || snapshot == null)
            {
                return false;
            }

            try
            {
                EnsureDirectoryExists(cachePath);

                PlayerSaveData saveData = PlayerSaveDataMapper.ToSaveData(snapshot);
                string json = JsonUtility.ToJson(saveData, false);
                File.WriteAllText(cachePath, json);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryDelete(out string error)
        {
            error = string.Empty;

            if (!isEnabled || string.IsNullOrEmpty(cachePath))
            {
                return true;
            }

            try
            {
                if (!File.Exists(cachePath))
                {
                    return true;
                }

                File.Delete(cachePath);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return DefaultCacheFileName;
            }

            string sanitized = fileName.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            int i;
            for (i = 0; i < invalidChars.Length; i++)
            {
                sanitized = sanitized.Replace(invalidChars[i], '_');
            }

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return DefaultCacheFileName;
            }

            return sanitized;
        }

        private static void EnsureDirectoryExists(string fullPath)
        {
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directoryPath))
            {
                return;
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}
