using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Wendao.Core;
using UnityEngine;

namespace Wendao.Data
{
    public sealed class SaveManager : Singleton<SaveManager>
    {
        public const int SlotCount = 3;
        public const double MinimumAutoSaveIntervalSeconds = 60d;

        private const string MetadataModule = "meta";
        private const string ProfileModule = "profile";
        private const string WorldModule = "world";

        private readonly SortedDictionary<string, IRegisteredSaveModule> _modules =
            new SortedDictionary<string, IRegisteredSaveModule>(StringComparer.OrdinalIgnoreCase);

        private DateTime _lastAutoSaveUtc = DateTime.MinValue;

        public int ActiveSlot { get; private set; } = -1;
        public string StorageRootPath { get; private set; }
        public string LastError { get; private set; }
        public SaveMetadata CurrentMetadata { get; private set; } = new SaveMetadata();
        public SaveProfileData Profile { get; private set; } = new SaveProfileData();
        public SaveWorldData World { get; private set; } = new SaveWorldData();

        public bool SaveGame(int slot)
        {
            LastError = null;
            if (!IsValidSlot(slot))
            {
                return Fail($"Save slot must be in the range 0..{SlotCount - 1}.");
            }

            if (!ValidateProfile(Profile, out string validationError)
                || !ValidateWorld(World, out validationError))
            {
                return Fail(validationError);
            }

            ActiveSlot = slot;
            SynchronizeMetadata(slot);
            string slotDirectory = GetSlotDirectory(slot);

            if (!JsonStorage.TryWriteAtomic(
                    Path.Combine(slotDirectory, ProfileModule + ".json"),
                    Profile,
                    out string error)
                || !JsonStorage.TryWriteAtomic(
                    Path.Combine(slotDirectory, WorldModule + ".json"),
                    World,
                    out error))
            {
                return Fail(error);
            }

            foreach (IRegisteredSaveModule module in _modules.Values)
            {
                if (!module.TrySave(Path.Combine(slotDirectory, module.Name + ".json"), out error))
                {
                    return Fail(error);
                }
            }

            // Metadata is written last so its existence remains the slot commit marker.
            if (!JsonStorage.TryWriteAtomic(
                    Path.Combine(slotDirectory, MetadataModule + ".json"),
                    CurrentMetadata,
                    out error))
            {
                return Fail(error);
            }

            CurrentMetadata.Exists = true;
            CurrentMetadata.IsCorrupted = false;
            return true;
        }

        public bool LoadGame(int slot)
        {
            LastError = null;
            if (!TryReadRequiredFiles(
                    slot,
                    out SaveMetadata metadata,
                    out SaveProfileData profile,
                    out SaveWorldData world,
                    out string error))
            {
                return Fail(error);
            }

            var loadedModules = new List<LoadedModuleState>();
            var modulesWithFiles = new HashSet<IRegisteredSaveModule>();
            string slotDirectory = GetSlotDirectory(slot);
            foreach (IRegisteredSaveModule module in _modules.Values)
            {
                string path = Path.Combine(slotDirectory, module.Name + ".json");
                if (!File.Exists(path))
                {
                    continue;
                }

                if (!module.TryRead(path, out object state, out error))
                {
                    if (!module.IsOptional)
                    {
                        return Fail(error);
                    }

                    Debug.LogWarning(
                        $"Optional save module '{module.Name}' was reset: {error}",
                        this);
                    if (!module.TryReset(out error))
                    {
                        return Fail(error);
                    }

                    modulesWithFiles.Add(module);
                    continue;
                }

                loadedModules.Add(new LoadedModuleState(module, state));
                modulesWithFiles.Add(module);
            }

            Profile = profile;
            World = world;
            CurrentMetadata = metadata;
            ActiveSlot = slot;

            foreach (IRegisteredSaveModule module in _modules.Values)
            {
                if (modulesWithFiles.Contains(module))
                {
                    continue;
                }

                if (!module.TryReset(out error))
                {
                    return Fail(error);
                }
            }

            foreach (LoadedModuleState loaded in loadedModules)
            {
                if (!loaded.Module.TryRestore(loaded.State, out error))
                {
                    return Fail(error);
                }
            }

            return true;
        }

        public bool DeleteSave(int slot)
        {
            LastError = null;
            if (!IsValidSlot(slot))
            {
                return Fail($"Save slot must be in the range 0..{SlotCount - 1}.");
            }

            try
            {
                string slotDirectory = GetSlotDirectory(slot);
                if (Directory.Exists(slotDirectory))
                {
                    Directory.Delete(slotDirectory, true);
                }

                if (ActiveSlot == slot)
                {
                    ResetRuntimeData();
                }

                return true;
            }
            catch (Exception exception)
            {
                return Fail($"Failed to delete save slot {slot}: {exception.Message}");
            }
        }

        public SaveMetadata GetMetadata(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return new SaveMetadata
                {
                    Slot = slot,
                    Exists = false,
                    IsCorrupted = true
                };
            }

            string slotDirectory = GetSlotDirectory(slot);
            if (!Directory.Exists(slotDirectory))
            {
                return new SaveMetadata
                {
                    Slot = slot,
                    Exists = false,
                    IsCorrupted = false
                };
            }

            if (!TryReadRequiredFiles(
                    slot,
                    out SaveMetadata metadata,
                    out _,
                    out _,
                    out _))
            {
                return new SaveMetadata
                {
                    Slot = slot,
                    Exists = true,
                    IsCorrupted = true
                };
            }

            foreach (IRegisteredSaveModule module in _modules.Values)
            {
                string path = Path.Combine(slotDirectory, module.Name + ".json");
                if (File.Exists(path)
                    && !module.TryRead(path, out _, out _)
                    && !module.IsOptional)
                {
                    metadata.IsCorrupted = true;
                    break;
                }
            }

            return metadata;
        }

        public SaveMetadata[] GetAllSaves()
        {
            var saves = new SaveMetadata[SlotCount];
            for (int slot = 0; slot < SlotCount; slot++)
            {
                saves[slot] = GetMetadata(slot);
            }

            return saves;
        }

        public void AutoSave()
        {
            if (!IsValidSlot(ActiveSlot))
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            if ((now - _lastAutoSaveUtc).TotalSeconds < MinimumAutoSaveIntervalSeconds)
            {
                return;
            }

            if (SaveGame(ActiveSlot))
            {
                _lastAutoSaveUtc = now;
            }
        }

        public void SaveModule(string moduleName)
        {
            TrySaveModule(moduleName);
        }

        public bool TrySaveModule(string moduleName)
        {
            LastError = null;
            if (!IsValidSlot(ActiveSlot))
            {
                return Fail("No active save slot is loaded.");
            }

            string slotDirectory = GetSlotDirectory(ActiveSlot);
            string error;
            switch (moduleName)
            {
                case ProfileModule:
                    if (!ValidateProfile(Profile, out error))
                    {
                        return Fail(error);
                    }

                    return WriteModule(ProfileModule, Profile);
                case WorldModule:
                    if (!ValidateWorld(World, out error))
                    {
                        return Fail(error);
                    }

                    return WriteModule(WorldModule, World);
                case MetadataModule:
                    SynchronizeMetadata(ActiveSlot);
                    return WriteModule(MetadataModule, CurrentMetadata);
                default:
                    if (!_modules.TryGetValue(moduleName ?? string.Empty, out IRegisteredSaveModule module))
                    {
                        return Fail($"Save module is not registered: {moduleName}");
                    }

                    if (!module.TrySave(
                            Path.Combine(slotDirectory, module.Name + ".json"),
                            out error))
                    {
                        return Fail(error);
                    }

                    return true;
            }
        }

        public bool RegisterModule<T>(
            string moduleName,
            Func<T> capture,
            Action<T> restore,
            Action reset = null,
            bool optional = false)
            where T : class
        {
            if (!IsValidCustomModuleName(moduleName) || capture == null || restore == null)
            {
                return false;
            }

            if (_modules.ContainsKey(moduleName))
            {
                return false;
            }

            _modules.Add(
                moduleName,
                new RegisteredSaveModule<T>(
                    moduleName,
                    capture,
                    restore,
                    reset,
                    optional));
            return true;
        }

        public bool UnregisterModule(string moduleName)
        {
            return !string.IsNullOrEmpty(moduleName) && _modules.Remove(moduleName);
        }

        public void ConfigureStorageRoot(string storageRootPath)
        {
            if (string.IsNullOrWhiteSpace(storageRootPath))
            {
                throw new ArgumentException("Storage root path is required.", nameof(storageRootPath));
            }

            StorageRootPath = Path.GetFullPath(storageRootPath);
            ResetRuntimeData();
        }

        public void ResetRuntimeData()
        {
            ActiveSlot = -1;
            CurrentMetadata = new SaveMetadata();
            Profile = new SaveProfileData();
            World = new SaveWorldData();
            LastError = null;
            _lastAutoSaveUtc = DateTime.MinValue;
        }

        protected override void OnSingletonAwake()
        {
            if (string.IsNullOrEmpty(StorageRootPath))
            {
                StorageRootPath = Path.Combine(Application.persistentDataPath, "Saves");
            }

            ResetRuntimeData();
        }

        private bool TryReadRequiredFiles(
            int slot,
            out SaveMetadata metadata,
            out SaveProfileData profile,
            out SaveWorldData world,
            out string error)
        {
            metadata = null;
            profile = null;
            world = null;
            error = null;

            if (!IsValidSlot(slot))
            {
                error = $"Save slot must be in the range 0..{SlotCount - 1}.";
                return false;
            }

            string slotDirectory = GetSlotDirectory(slot);
            if (!Directory.Exists(slotDirectory))
            {
                error = $"Save slot does not exist: {slot}";
                return false;
            }

            if (!JsonStorage.TryRead(
                    Path.Combine(slotDirectory, MetadataModule + ".json"),
                    out metadata,
                    out error)
                || !JsonStorage.TryRead(
                    Path.Combine(slotDirectory, ProfileModule + ".json"),
                    out profile,
                    out error)
                || !JsonStorage.TryRead(
                    Path.Combine(slotDirectory, WorldModule + ".json"),
                    out world,
                    out error))
            {
                return false;
            }

            if (!ValidateMetadata(metadata, out error)
                || !ValidateProfile(profile, out error)
                || !ValidateWorld(world, out error))
            {
                return false;
            }

            metadata.Slot = slot;
            metadata.Exists = true;
            metadata.IsCorrupted = false;
            return true;
        }

        private bool WriteModule<T>(string moduleName, T value) where T : class
        {
            if (!JsonStorage.TryWriteAtomic(
                    Path.Combine(GetSlotDirectory(ActiveSlot), moduleName + ".json"),
                    value,
                    out string error))
            {
                return Fail(error);
            }

            return true;
        }

        private void SynchronizeMetadata(int slot)
        {
            CurrentMetadata.SchemaVersion = SaveSchema.CurrentVersion;
            CurrentMetadata.DisplayName = Profile.DisplayName;
            CurrentMetadata.Realm = Profile.Realm;
            CurrentMetadata.SubStage = Profile.SubStage;
            CurrentMetadata.PlayTimeSeconds = Profile.PlayTimeSeconds;
            CurrentMetadata.MainQuestIndex = Profile.MainQuestIndex;
            CurrentMetadata.SpiritStones = Profile.SpiritStones;
            CurrentMetadata.SavedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            CurrentMetadata.Slot = slot;
            CurrentMetadata.Exists = true;
            CurrentMetadata.IsCorrupted = false;
        }

        private string GetSlotDirectory(int slot)
        {
            string root = StorageRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(Application.persistentDataPath, "Saves");
                StorageRootPath = root;
            }

            return Path.Combine(root, $"SaveSlot_{slot}");
        }

        private bool Fail(string error)
        {
            LastError = string.IsNullOrEmpty(error) ? "Unknown save error." : error;
            Debug.LogError(LastError, this);
            return false;
        }

        private static bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < SlotCount;
        }

        private static bool IsValidCustomModuleName(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName)
                || string.Equals(moduleName, MetadataModule, StringComparison.OrdinalIgnoreCase)
                || string.Equals(moduleName, ProfileModule, StringComparison.OrdinalIgnoreCase)
                || string.Equals(moduleName, WorldModule, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = 0; i < moduleName.Length; i++)
            {
                char value = moduleName[i];
                if (!char.IsLetterOrDigit(value) && value != '_' && value != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateMetadata(SaveMetadata value, out string error)
        {
            if (value == null || value.SchemaVersion != SaveSchema.CurrentVersion)
            {
                error = "Unsupported or missing metadata schema version.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateProfile(SaveProfileData value, out string error)
        {
            if (value == null || value.SchemaVersion != SaveSchema.CurrentVersion)
            {
                error = "Unsupported or missing profile schema version.";
                return false;
            }

            if (value.Realm < 0 || value.Realm > 4 || value.SubStage < 0)
            {
                error = "Profile realm or sub-stage is outside the supported domain.";
                return false;
            }

            if (float.IsNaN(value.CultivationXp)
                || float.IsInfinity(value.CultivationXp)
                || value.CultivationXp < 0f)
            {
                error = "Profile cultivation XP must be a finite non-negative value.";
                return false;
            }

            if (!string.IsNullOrEmpty(value.SpiritRoot)
                && (!Enum.TryParse(
                        value.SpiritRoot,
                        false,
                        out SpiritRootType spiritRoot)
                    || spiritRoot == SpiritRootType.None
                    || !Enum.IsDefined(typeof(SpiritRootType), spiritRoot)))
            {
                error = "Profile spirit root is outside the supported domain.";
                return false;
            }

            if (value.BodyLevel < (int)BodyLevel.Mortal
                || value.BodyLevel > (int)BodyLevel.Eternal)
            {
                error = "Profile body level is outside the supported domain.";
                return false;
            }

            if (float.IsNaN(value.BodyXp)
                || float.IsInfinity(value.BodyXp)
                || value.BodyXp < 0f)
            {
                error = "Profile body XP must be a finite non-negative value.";
                return false;
            }

            if (value.FactionReputation == null)
            {
                error = "Profile faction reputation collection is null.";
                return false;
            }

            foreach (KeyValuePair<string, int> entry in value.FactionReputation)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0)
                {
                    error = "Profile faction reputation entry is invalid.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateWorld(SaveWorldData value, out string error)
        {
            if (value == null || value.SchemaVersion != SaveSchema.CurrentVersion)
            {
                error = "Unsupported or missing world schema version.";
                return false;
            }

            if (value.UnlockedMaps == null
                || value.UnlockedTeleports == null
                || value.TutorialsCompleted == null
                || value.SerendipityFlags == null
                || value.DungeonCheckpoint == null
                || value.QuestFlags == null)
            {
                error = "World save contains a null collection.";
                return false;
            }

            if (value.DungeonCheckpoint.TryGetValue("map_blackwind", out int checkpoint)
                && (checkpoint < 0 || checkpoint > 4))
            {
                error = "map_blackwind checkpoint must be in the range 0..4.";
                return false;
            }

            if (float.IsNaN(value.TimeOfDay)
                || float.IsInfinity(value.TimeOfDay)
                || value.TimeOfDay < 0f
                || value.TimeOfDay >= 24f)
            {
                error = "World time of day must be in the range [0, 24).";
                return false;
            }

            error = null;
            return true;
        }

        private interface IRegisteredSaveModule
        {
            string Name { get; }
            bool IsOptional { get; }
            bool TrySave(string path, out string error);
            bool TryRead(string path, out object state, out string error);
            bool TryReset(out string error);
            bool TryRestore(object state, out string error);
        }

        private sealed class RegisteredSaveModule<T> : IRegisteredSaveModule where T : class
        {
            private readonly Func<T> _capture;
            private readonly Action<T> _restore;
            private readonly Action _reset;

            public RegisteredSaveModule(
                string name,
                Func<T> capture,
                Action<T> restore,
                Action reset,
                bool optional)
            {
                Name = name;
                _capture = capture;
                _restore = restore;
                _reset = reset;
                IsOptional = optional;
            }

            public string Name { get; }
            public bool IsOptional { get; }

            public bool TrySave(string path, out string error)
            {
                try
                {
                    T state = _capture();
                    if (state == null)
                    {
                        error = $"Save module '{Name}' captured a null state.";
                        return false;
                    }

                    return JsonStorage.TryWriteAtomic(path, state, out error);
                }
                catch (Exception exception)
                {
                    error = $"Save module '{Name}' capture failed: {exception.Message}";
                    return false;
                }
            }

            public bool TryRead(string path, out object state, out string error)
            {
                bool success = JsonStorage.TryRead(path, out T typedState, out error);
                state = typedState;
                return success;
            }

            public bool TryRestore(object state, out string error)
            {
                try
                {
                    _restore((T)state);
                    error = null;
                    return true;
                }
                catch (Exception exception)
                {
                    error = $"Save module '{Name}' restore failed: {exception.Message}";
                    return false;
                }
            }

            public bool TryReset(out string error)
            {
                try
                {
                    _reset?.Invoke();
                    error = null;
                    return true;
                }
                catch (Exception exception)
                {
                    error = $"Save module '{Name}' reset failed: {exception.Message}";
                    return false;
                }
            }
        }

        private sealed class LoadedModuleState
        {
            public LoadedModuleState(IRegisteredSaveModule module, object state)
            {
                Module = module;
                State = state;
            }

            public IRegisteredSaveModule Module { get; }
            public object State { get; }
        }
    }
}
