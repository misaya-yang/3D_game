using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Wendao.Data
{
    [Serializable]
    public sealed class GameSettingsData
    {
        [JsonProperty(Required = Required.Always)]
        public int SchemaVersion = SaveSchema.CurrentVersion;
        public float MasterVolume = 1f;
        public float BgmVolume = 0.65f;
        public float SfxVolume = 0.8f;
        public bool Fullscreen;

        public GameSettingsData Clone()
        {
            return new GameSettingsData
            {
                SchemaVersion = SchemaVersion,
                MasterVolume = MasterVolume,
                BgmVolume = BgmVolume,
                SfxVolume = SfxVolume,
                Fullscreen = Fullscreen
            };
        }

        public void Sanitize()
        {
            SchemaVersion = SaveSchema.CurrentVersion;
            MasterVolume = Mathf.Clamp01(MasterVolume);
            BgmVolume = Mathf.Clamp01(BgmVolume);
            SfxVolume = Mathf.Clamp01(SfxVolume);
        }
    }

    public static class GameSettingsStore
    {
        private static string _storageRootOverride;

        public static string SettingsPath => Path.Combine(
            string.IsNullOrWhiteSpace(_storageRootOverride)
                ? Application.persistentDataPath
                : _storageRootOverride,
            "Settings",
            "settings.json");

        public static void ConfigureStorageRoot(string storageRoot)
        {
            _storageRootOverride = storageRoot ?? string.Empty;
        }

        public static GameSettingsData LoadOrDefault()
        {
            if (JsonStorage.TryRead(
                    SettingsPath,
                    out GameSettingsData settings,
                    out _)
                && settings.SchemaVersion == SaveSchema.CurrentVersion)
            {
                settings.Sanitize();
                return settings;
            }

            return new GameSettingsData();
        }

        public static bool TrySave(GameSettingsData settings, out string error)
        {
            if (settings == null)
            {
                error = "Settings payload is null.";
                return false;
            }

            settings.Sanitize();
            return JsonStorage.TryWriteAtomic(SettingsPath, settings, out error);
        }
    }
}
