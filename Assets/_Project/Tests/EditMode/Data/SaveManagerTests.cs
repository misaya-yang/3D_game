using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Data;
using Object = UnityEngine.Object;

namespace Wendao.Tests.EditMode.Data
{
    public sealed class SaveManagerTests
    {
        [Serializable]
        public sealed class InventoryModuleState
        {
            public int PotionCount;
        }

        private string _storageRoot;
        private SaveManager _manager;

        [SetUp]
        public void SetUp()
        {
            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoSaveTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_storageRoot);
            _manager = CreateManager();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyManager();
            if (Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void SaveAndLoad_RoundTripProfileWorldAndMetadataAcrossSlot()
        {
            _manager.Profile.DisplayName = "云岚";
            _manager.Profile.Realm = 2;
            _manager.Profile.SubStage = 3;
            _manager.Profile.CultivationXp = 8123.5f;
            _manager.Profile.SpiritRoot = "Water";
            _manager.Profile.BodyLevel = (int)BodyLevel.Diamond;
            _manager.Profile.BodyXp = 4875.5f;
            _manager.Profile.PlayTimeSeconds = 456.25f;
            _manager.Profile.MainQuestIndex = "quest_main_02_03";
            _manager.Profile.SpiritStones = 789;
            _manager.World.UnlockedMaps.Add("map_qingshi");
            _manager.World.TutorialsCompleted.Add("tut_move");
            _manager.World.SerendipityFlags.Add("serendipity_qingshi_01");
            _manager.World.DungeonCheckpoint["map_blackwind"] = 2;
            _manager.World.QuestFlags["foundation_pill_granted"] = true;

            Assert.That(_manager.SaveGame(1), Is.True, _manager.LastError);

            string slotDirectory = Path.Combine(_storageRoot, "SaveSlot_1");
            Assert.That(File.Exists(Path.Combine(slotDirectory, "meta.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(slotDirectory, "profile.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(slotDirectory, "world.json")), Is.True);

            string worldJson = File.ReadAllText(Path.Combine(slotDirectory, "world.json"));
            StringAssert.Contains("\"tutorialsCompleted\"", worldJson);
            StringAssert.Contains("\"map_blackwind\": 2", worldJson);

            DestroyManager();
            _manager = CreateManager();

            Assert.That(_manager.LoadGame(1), Is.True, _manager.LastError);
            Assert.That(_manager.Profile.DisplayName, Is.EqualTo("云岚"));
            Assert.That(_manager.Profile.CultivationXp, Is.EqualTo(8123.5f));
            Assert.That(_manager.Profile.SpiritRoot, Is.EqualTo("Water"));
            Assert.That(
                _manager.Profile.BodyLevel,
                Is.EqualTo((int)BodyLevel.Diamond));
            Assert.That(_manager.Profile.BodyXp, Is.EqualTo(4875.5f));
            Assert.That(_manager.World.TutorialsCompleted, Contains.Item("tut_move"));
            Assert.That(_manager.World.DungeonCheckpoint["map_blackwind"], Is.EqualTo(2));
            Assert.That(_manager.World.QuestFlags["foundation_pill_granted"], Is.True);

            SaveMetadata metadata = _manager.GetMetadata(1);
            Assert.That(metadata.Exists, Is.True);
            Assert.That(metadata.IsCorrupted, Is.False);
            Assert.That(metadata.DisplayName, Is.EqualTo("云岚"));
            Assert.That(metadata.Realm, Is.EqualTo(2));
            Assert.That(metadata.SpiritStones, Is.EqualTo(789));
            Assert.That(metadata.SavedAt, Is.Not.Empty);
            Assert.That(_manager.GetAllSaves(), Has.Length.EqualTo(SaveManager.SlotCount));
        }

        [Test]
        public void RegisteredModule_RoundTripsThroughItsOwnJsonFile()
        {
            var captured = new InventoryModuleState { PotionCount = 17 };
            Assert.That(
                _manager.RegisterModule("inventory", () => captured, _ => { }),
                Is.True);
            Assert.That(
                _manager.RegisterModule("Inventory", () => captured, _ => { }),
                Is.False);
            Assert.That(
                _manager.RegisterModule("Profile", () => captured, _ => { }),
                Is.False);
            Assert.That(_manager.SaveGame(0), Is.True, _manager.LastError);
            Assert.That(
                File.Exists(Path.Combine(_storageRoot, "SaveSlot_0", "inventory.json")),
                Is.True);

            DestroyManager();
            _manager = CreateManager();
            InventoryModuleState restored = null;
            Assert.That(
                _manager.RegisterModule("inventory", () => new InventoryModuleState(), value => restored = value),
                Is.True);

            Assert.That(_manager.LoadGame(0), Is.True, _manager.LastError);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.PotionCount, Is.EqualTo(17));
        }

        [Test]
        public void MissingRegisteredModuleFile_ResetsRuntimeStateForOlderSave()
        {
            var state = new InventoryModuleState { PotionCount = 17 };
            var resetCalls = 0;
            Assert.That(
                _manager.RegisterModule(
                    "inventory",
                    () => state,
                    value => state = value,
                    () =>
                    {
                        state = new InventoryModuleState();
                        resetCalls++;
                    }),
                Is.True);
            Assert.That(_manager.SaveGame(0), Is.True, _manager.LastError);
            File.Delete(Path.Combine(_storageRoot, "SaveSlot_0", "inventory.json"));
            state.PotionCount = 99;

            Assert.That(_manager.LoadGame(0), Is.True, _manager.LastError);
            Assert.That(resetCalls, Is.EqualTo(1));
            Assert.That(state.PotionCount, Is.Zero);
        }

        [Test]
        public void DamagedRequiredFile_MarksSlotCorrupted()
        {
            Assert.That(_manager.SaveGame(2), Is.True, _manager.LastError);
            File.WriteAllText(
                Path.Combine(_storageRoot, "SaveSlot_2", "profile.json"),
                "{ invalid json");

            SaveMetadata metadata = _manager.GetMetadata(2);

            Assert.That(metadata.Exists, Is.True);
            Assert.That(metadata.IsCorrupted, Is.True);
        }

        [Test]
        public void BlackwindCheckpoint_RejectsValuesOutsideZeroThroughFour()
        {
            _manager.World.DungeonCheckpoint["map_blackwind"] = 5;
            LogAssert.Expect(LogType.Error, new Regex("map_blackwind checkpoint must be in the range 0..4"));

            Assert.That(_manager.SaveGame(0), Is.False);
            Assert.That(_manager.LastError, Does.Contain("0..4"));
            Assert.That(Directory.Exists(Path.Combine(_storageRoot, "SaveSlot_0")), Is.False);
        }

        [Test]
        public void ProfileRejectsInvalidBodyProgress()
        {
            _manager.Profile.BodyLevel = (int)BodyLevel.Eternal + 1;
            LogAssert.Expect(
                LogType.Error,
                new Regex("Profile body level is outside the supported domain"));
            Assert.That(_manager.SaveGame(0), Is.False);

            _manager.Profile.BodyLevel = (int)BodyLevel.Mortal;
            _manager.Profile.BodyXp = float.NaN;
            LogAssert.Expect(
                LogType.Error,
                new Regex("Profile body XP must be a finite non-negative value"));
            Assert.That(_manager.SaveGame(0), Is.False);
        }

        [Test]
        public void DeleteSave_IsIdempotentAndClearsActiveRuntimeSlot()
        {
            Assert.That(_manager.SaveGame(0), Is.True, _manager.LastError);
            Assert.That(_manager.ActiveSlot, Is.EqualTo(0));

            Assert.That(_manager.DeleteSave(0), Is.True, _manager.LastError);
            Assert.That(_manager.DeleteSave(0), Is.True, _manager.LastError);
            Assert.That(_manager.ActiveSlot, Is.EqualTo(-1));
            Assert.That(_manager.GetMetadata(0).Exists, Is.False);
        }

        private SaveManager CreateManager()
        {
            var gameObject = new GameObject("SaveManager Test");
            var manager = gameObject.AddComponent<SaveManager>();
            manager.ConfigureStorageRoot(_storageRoot);
            return manager;
        }

        private void DestroyManager()
        {
            if (_manager != null)
            {
                Object.DestroyImmediate(_manager.gameObject);
                _manager = null;
            }
        }
    }
}
