using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GPTLabyrinth.Save
{
    /// <summary>
    /// Owns persistence of the SaveRoot. Responsibilities (and ONLY these):
    ///   - serialize / deserialize SaveRoot
    ///   - run SaveValidator before save and after load
    ///   - safe (atomic-ish) write with backup
    ///   - quarantine corrupted saves
    ///   - fall back to a safe default save
    ///
    /// SaveManager does NOT create DeathEvents, interpret AI output, drive RunManager,
    /// or make game-logic decisions. Those live in their own managers.
    /// </summary>
    public static class SaveManager
    {
        private const string SaveFileName = "save.json";
        private const string BackupFileName = "save.backup.json";
        private const string TempFileName = "save.tmp.json";
        private const string QuarantineDir = "quarantine";

        public static string SaveDir => Application.persistentDataPath;
        public static string SavePath => Path.Combine(SaveDir, SaveFileName);
        public static string BackupPath => Path.Combine(SaveDir, BackupFileName);
        public static string TempPath => Path.Combine(SaveDir, TempFileName);
        public static string QuarantinePath => Path.Combine(SaveDir, QuarantineDir);

        public static bool SaveExists() => File.Exists(SavePath);

        /// <summary>
        /// Safe write per the design blueprint:
        /// 1) sanitize in memory
        /// 2) serialize to temp file
        /// 3) read temp back and validate (round-trip + forbidden-key scan)
        /// 4) if valid: back up current save, then replace it with temp
        /// 5) if invalid: keep the existing save.json untouched
        /// </summary>
        public static bool Save(SaveRoot root)
        {
            if (root == null)
            {
                Debug.LogError("[SaveManager] Save called with null root; aborting.");
                return false;
            }

            try
            {
                Directory.CreateDirectory(SaveDir);

                // 1) Sanitize before serialize.
                var pre = SaveValidator.Sanitize(root);
                if (!pre.IsValid)
                {
                    Debug.LogError("[SaveManager] Pre-save validation rejected the save: " + string.Join("; ", pre.Messages));
                    return false;
                }

                // 2) Serialize to temp.
                string json = JsonUtility.ToJson(root, prettyPrint: true);
                File.WriteAllText(TempPath, json);

                // 3) Round-trip: read temp back, deserialize, re-sanitize, scan for forbidden keys.
                string readBack = File.ReadAllText(TempPath);
                if (!SaveValidator.ScanRawJson(readBack, out var violations))
                {
                    Debug.LogError("[SaveManager] FORBIDDEN KEYS detected in serialized save; aborting write: " + string.Join(", ", violations));
                    SafeDelete(TempPath);
                    return false;
                }
                var verify = JsonUtility.FromJson<SaveRoot>(readBack);
                var post = SaveValidator.Sanitize(verify);
                if (!post.IsValid)
                {
                    Debug.LogError("[SaveManager] Round-trip validation failed; keeping existing save. " + string.Join("; ", post.Messages));
                    SafeDelete(TempPath);
                    return false;
                }

                // 4) Back up existing, then atomically swap temp into place.
                if (File.Exists(SavePath))
                {
                    SafeDelete(BackupPath);
                    File.Copy(SavePath, BackupPath);
                }
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(TempPath, SavePath);

                Debug.Log("[SaveManager] Save written to " + SavePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveManager] Save failed: " + e);
                SafeDelete(TempPath);
                return false;
            }
        }

        /// <summary>
        /// Loads the save. On corruption: quarantines the bad file, tries the backup,
        /// and finally falls back to a fresh default save. Always returns a valid,
        /// sanitized SaveRoot (never null).
        /// </summary>
        public static SaveRoot Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var loaded = TryLoadFile(SavePath, out bool corrupted);
                    if (loaded != null) return loaded;
                    if (corrupted) Quarantine(SavePath, "primary");
                }

                if (File.Exists(BackupPath))
                {
                    Debug.LogWarning("[SaveManager] Primary save unusable; attempting backup.");
                    var backup = TryLoadFile(BackupPath, out bool backupCorrupted);
                    if (backup != null) return backup;
                    if (backupCorrupted) Quarantine(BackupPath, "backup");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveManager] Load failed unexpectedly: " + e);
            }

            Debug.LogWarning("[SaveManager] No usable save found; creating default.");
            return SaveRoot.CreateDefault();
        }

        /// <summary>
        /// Attempts to parse and validate a single file. Returns a sanitized SaveRoot,
        /// or null if the file is missing/corrupt. Sets <paramref name="corrupted"/>.
        /// </summary>
        private static SaveRoot TryLoadFile(string path, out bool corrupted)
        {
            corrupted = false;
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    corrupted = true;
                    return null;
                }

                // Forbidden-key scan on load (defense against tampered files).
                if (!SaveValidator.ScanRawJson(json, out var violations))
                {
                    Debug.LogError("[SaveManager] Loaded file contains forbidden keys: " + string.Join(", ", violations));
                    corrupted = true;
                    return null;
                }

                var root = JsonUtility.FromJson<SaveRoot>(json);
                if (root == null)
                {
                    corrupted = true;
                    return null;
                }

                var result = SaveValidator.Sanitize(root);
                if (!result.IsValid)
                {
                    Debug.LogError("[SaveManager] Post-load validation rejected file: " + string.Join("; ", result.Messages));
                    corrupted = true;
                    return null;
                }

                if (result.WasModified)
                    Debug.LogWarning("[SaveManager] Loaded save was sanitized: " + string.Join("; ", result.Messages));

                return root;
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveManager] Could not parse '" + path + "': " + e.Message);
                corrupted = true;
                return null;
            }
        }

        private static void Quarantine(string path, string label)
        {
            try
            {
                Directory.CreateDirectory(QuarantinePath);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string dest = Path.Combine(QuarantinePath, $"{label}_{stamp}.json");
                File.Move(path, dest);
                Debug.LogWarning("[SaveManager] Quarantined corrupted save -> " + dest);
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveManager] Failed to quarantine '" + path + "': " + e.Message);
                SafeDelete(path);
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best effort */ }
        }

        /// <summary>Deletes all save artifacts (used by Data Management "delete all").</summary>
        public static void DeleteAll()
        {
            SafeDelete(SavePath);
            SafeDelete(BackupPath);
            SafeDelete(TempPath);
            Debug.Log("[SaveManager] All primary save files deleted.");
        }
    }
}
