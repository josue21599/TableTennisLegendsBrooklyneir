using System;
using System.IO;
using UnityEngine;

namespace Brooklyneir.SaveSystem
{
    /// <summary>
    /// Local persistent save (JSON in Application.persistentDataPath). Writes go to a temporary
    /// file first and then replace the main file, keeping the previous version as a backup, so a
    /// crash or a phone shutdown in the middle of a write cannot destroy the progress.
    /// Loading tries the main file, then the backup, then starts fresh; whatever loads is
    /// sanitised. No network, no accounts.
    /// </summary>
    public class SaveService
    {
        const string FileName = "ttlb_save.json";
        const string BackupName = "ttlb_save.bak";
        const string TempName = "ttlb_save.tmp";

        readonly string _dir;
        readonly int _startingCoins;
        readonly int _maxCoins;
        bool _dirty;

        public SaveData Data { get; private set; }
        public string LastError { get; private set; }

        public SaveService(int startingCoins, int maxCoins) : this(Application.persistentDataPath, startingCoins, maxCoins) { }

        public SaveService(string directory, int startingCoins, int maxCoins)
        {
            _dir = directory;
            _startingCoins = startingCoins;
            _maxCoins = maxCoins;
        }

        string PathOf(string name) { return Path.Combine(_dir, name); }

        public void Load()
        {
            SaveData d = TryRead(PathOf(FileName));
            if (d == null)
            {
                d = TryRead(PathOf(BackupName));
                if (d != null) Debug.LogWarning("[TTLB] Main save unreadable, restored from backup.");
            }
            if (d == null)
            {
                d = SaveData.CreateNew(_startingCoins);
                _dirty = true;
            }
            if (d.Sanitize(_maxCoins)) _dirty = true;
            Data = d;
            if (_dirty) Save();
        }

        SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json) || json.Trim().Length < 2) return null;
                // Overwrite a default instance so fields missing from older files keep their defaults.
                SaveData d = new SaveData();
                JsonUtility.FromJsonOverwrite(json, d);
                return d;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning("[TTLB] Could not read save '" + path + "': " + e.Message);
                return null;
            }
        }

        public void MarkDirty() { _dirty = true; }

        /// <summary>Writes only if something changed since the last save.</summary>
        public void SaveIfDirty()
        {
            if (_dirty) Save();
        }

        public bool Save()
        {
            if (Data == null) return false;
            try
            {
                if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
                string json = JsonUtility.ToJson(Data, false);
                string tmp = PathOf(TempName), main = PathOf(FileName), bak = PathOf(BackupName);
                File.WriteAllText(tmp, json);
                if (File.Exists(main))
                {
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(main, bak);
                }
                File.Move(tmp, main);
                _dirty = false;
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogError("[TTLB] Save failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Erases all progress (settings are kept).</summary>
        public void ResetProgress()
        {
            SettingsData keep = Data != null ? Data.settings : new SettingsData();
            Data = SaveData.CreateNew(_startingCoins);
            Data.settings = keep;
            Data.Sanitize(_maxCoins);
            Save();
        }
    }
}
