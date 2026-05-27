using System;
using System.IO;
using Game.Domain.Player;
using UnityEngine;

namespace Game.Infrastructure.Persistence
{
    public sealed class LocalPlayerCacheStore
    {
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

            if (string.IsNullOrEmpty(cachePath))
            {
                return false;
            }

            if (File.Exists(cachePath))
            {
                if (TryParseCacheFile(cachePath, out snapshot, out string primaryError))
                {
                    return true;
                }

                error = primaryError;
            }

            // Primary file missing or unreadable; attempt the .bak fallback.
            string backupPath = cachePath + ".bak";
            if (!File.Exists(backupPath))
            {
                return false;
            }

            if (TryParseCacheFile(backupPath, out snapshot, out string backupError))
            {
                // Surface that we fell back, but do not treat it as an error.
                error = string.Empty;
                return true;
            }

            if (string.IsNullOrEmpty(error))
            {
                error = backupError;
            }

            return false;
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

                string tempPath = cachePath + ".tmp";
                string backupPath = cachePath + ".bak";
                File.WriteAllText(tempPath, json);

                if (File.Exists(cachePath))
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                    File.Replace(tempPath, cachePath, backupPath);
                }
                else
                {
                    File.Move(tempPath, cachePath);
                }
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
                if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }

                string backupPath = cachePath + ".bak";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                string tempPath = cachePath + ".tmp";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryParseCacheFile(
            string path,
            out PlayerProfileSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;

            try
            {
                string json = File.ReadAllText(path);
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

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException(
                    "Local cache file name must be provided via PersistenceSettings.",
                    nameof(fileName));
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
                throw new ArgumentException(
                    "Local cache file name contains only invalid characters.",
                    nameof(fileName));
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
