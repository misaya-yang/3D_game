using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.NPC;
using Wendao.Entities.Player;
using Wendao.Systems;
using Wendao.Systems.Achievement;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Diagnostics;
using Wendao.Systems.Equipment;
using Wendao.Systems.Faction;
using Wendao.Systems.Feedback;
using Wendao.Systems.Inventory;
using Wendao.Systems.Loot;
using Wendao.Systems.Mount;
using Wendao.Systems.NPC;
using Wendao.Systems.Quest;
using Wendao.Systems.Shop;
using Wendao.Systems.Skill;
using Wendao.Systems.Title;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0703PlayModeTests
    {
        private string _storageRoot;
        private float _sceneLoadSeconds;
        private int _previousQualityLevel;
        private int _previousVsyncCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            _previousQualityLevel = QualitySettings.GetQualityLevel();
            _previousVsyncCount = QualitySettings.vSyncCount;
            SceneFlowBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0703Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            float loadStarted = Time.realtimeSinceStartup;
            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            _sceneLoadSeconds = Time.realtimeSinceStartup - loadStarted;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            EnemySpawner spawner = WolfRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            Assert.That(spawner, Is.Not.Null);
            spawner.SpawnAllNow();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            QualitySettings.SetQualityLevel(_previousQualityLevel, true);
            QualitySettings.vSyncCount = _previousVsyncCount;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator BoundaryMatrixRejectsUnsafeOperationsWithoutCrashing()
        {
            Assert.That(MvpBoundaryCatalog.All, Has.Length.EqualTo(8));
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            SkillManager skills = Object.FindAnyObjectByType<SkillManager>();
            EquipmentManager equipment =
                Object.FindAnyObjectByType<EquipmentManager>();
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            DialogueManager dialogue = Object.FindAnyObjectByType<DialogueManager>();
            PlayerController player = Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            Assert.That(skills, Is.Not.Null);
            Assert.That(equipment, Is.Not.Null);
            Assert.That(dialogue, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(stats, Is.Not.Null);

            var toastKeys = new List<string>();
            Action<ToastInfo> toastHandler = info =>
                toastKeys.Add(info.LocalizationKey);
            EventBus.Subscribe(UiEvents.ToastRequested, toastHandler);
            try
            {
                int safety = InventoryManager.Capacity + 1;
                while (safety-- > 0
                    && inventory.AddItem(
                        InventoryContentIds.IronSword,
                        1,
                        AcquireSource.Cheat))
                {
                }

                Assert.That(inventory.Slots, Has.Count.EqualTo(InventoryManager.Capacity));
                Assert.That(
                    inventory.CanAdd(InventoryContentIds.HealPotion01, 1),
                    Is.False);
                Assert.That(
                    inventory.AddItem(
                        InventoryContentIds.HealPotion01,
                        1,
                        AcquireSource.Loot),
                    Is.False);

                player.ForceState(PlayerState.Idle);
                stats.SetMana(0f);
                Assert.That(skills.CanCast(0), Is.False);
                Assert.That(
                    skills.TryCast(0, player.transform.position + Vector3.forward, null),
                    Is.False);
                Assert.That(player.State, Is.Not.EqualTo(PlayerState.SkillCast));
                Assert.That(toastKeys, Does.Contain(SkillManager.ManaInsufficientToastKey));

                ItemData ironItem = ConfigDatabase.Instance.GetItem(
                    InventoryContentIds.IronSword);
                EquipmentData ironEquipment = ConfigDatabase.Instance.GetEquipment(
                    InventoryContentIds.IronSword);
                int oldItemRealm = ironItem.RequiredRealm;
                int oldEquipmentRealm = ironEquipment.RequiredRealm;
                ironItem.RequiredRealm = (int)RealmType.Foundation;
                ironEquipment.RequiredRealm = (int)RealmType.Foundation;
                try
                {
                    int ironSlot = FindSlot(
                        inventory,
                        InventoryContentIds.IronSword);
                    Assert.That(ironSlot, Is.GreaterThanOrEqualTo(0));
                    Assert.That(equipment.EquipFromInventory(ironSlot), Is.False);
                    Assert.That(
                        toastKeys,
                        Does.Contain(EquipmentManager.RealmRequiredToastKey));
                }
                finally
                {
                    ironItem.RequiredRealm = oldItemRealm;
                    ironEquipment.RequiredRealm = oldEquipmentRealm;
                }

                Assert.That(
                    quests.Accept(QuestContentIds.MainRootAwakening),
                    Is.True);
                Assert.That(
                    quests.Accept(QuestContentIds.MainRootAwakening),
                    Is.False);

                Assert.That(
                    dialogue.TryStartDialogue("dlg_shop_zhanggui", "npc_zhanggui"),
                    Is.True);
                Scene previous = SceneManager.GetActiveScene();
                Scene temporary = SceneManager.CreateScene(
                    "G0703_DialogueBoundary_" + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(temporary);
                Assert.That(dialogue.IsOpen, Is.False);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
                SceneManager.SetActiveScene(previous);
                yield return SceneManager.UnloadSceneAsync(temporary);
            }
            finally
            {
                EventBus.Unsubscribe(UiEvents.ToastRequested, toastHandler);
            }
        }

        [Test]
        public void RepeatedGreyboxEnsureReusesExistingMaterials()
        {
            Scene scene = SceneManager.GetActiveScene();
            QingshiGreyboxFactory.EnsureCreated(scene);
            Renderer ground = GameObject.Find(QingshiGreyboxFactory.GroundName)
                .GetComponent<Renderer>();
            Renderer gatherable = GameObject.Find(
                    Wendao.Systems.Crafting.GatheringContentIds.QingshiQingxin01)
                .GetComponent<Renderer>();
            WorldAreaMarker area = Object.FindAnyObjectByType<WorldAreaMarker>();
            Renderer areaPlate = area.transform.Find(WorldAreaMarker.PlateName)
                .GetComponent<Renderer>();
            Material groundMaterial = ground.sharedMaterial;
            Material gatherableMaterial = gatherable.sharedMaterial;
            Material areaMaterial = areaPlate.sharedMaterial;

            for (int index = 0; index < 20; index++)
            {
                QingshiGreyboxFactory.EnsureCreated(scene);
            }

            Assert.That(ground.sharedMaterial, Is.SameAs(groundMaterial));
            Assert.That(gatherable.sharedMaterial, Is.SameAs(gatherableMaterial));
            Assert.That(areaPlate.sharedMaterial, Is.SameAs(areaMaterial));
        }

        [UnityTest]
        public IEnumerator Qingshi1080pMediumProxyMeetsPerformanceBudget()
        {
            int medium = Array.IndexOf(
                QualitySettings.names,
                MvpRuntimeDiagnostics.TargetQualityName);
            Assert.That(medium, Is.GreaterThanOrEqualTo(0));
            QualitySettings.SetQualityLevel(medium, true);
            QualitySettings.vSyncCount = 0;
            Screen.SetResolution(
                MvpRuntimeDiagnostics.TargetWidth,
                MvpRuntimeDiagnostics.TargetHeight,
                false);

            for (int warmup = 0; warmup < 30; warmup++)
            {
                yield return null;
            }

            using (var diagnostics = new MvpRuntimeDiagnostics())
            {
                diagnostics.Begin(0f);
                float previous = Time.realtimeSinceStartup;
                for (int frame = 0; frame < 120; frame++)
                {
                    yield return null;
                    float now = Time.realtimeSinceStartup;
                    diagnostics.SampleFrame(Mathf.Max(0.000001f, now - previous));
                    previous = now;
                }

                MvpRuntimeDiagnosticsReport report = diagnostics.Report;
                TestContext.WriteLine(
                    $"G07-03 performance proxy: 1920x1080 Medium; "
                    + $"load={_sceneLoadSeconds:0.000}s; "
                    + $"avg={report.AverageFramesPerSecond:0.0}fps; "
                    + $"worst={report.WorstFrameMilliseconds:0.00}ms; "
                    + $"memory={report.PeakAllocatedMemoryBytes / (1024f * 1024f):0.0}MiB.");
                Assert.That(QualitySettings.names[QualitySettings.GetQualityLevel()],
                    Is.EqualTo(MvpRuntimeDiagnostics.TargetQualityName));
                Assert.That(_sceneLoadSeconds, Is.LessThan(15f));
                Assert.That(report.AverageFramesPerSecond,
                    Is.GreaterThanOrEqualTo(MvpRuntimeDiagnostics.MinimumFramesPerSecond));
                Assert.That(report.MeetsMemoryBudget, Is.True);
                Assert.That(report.ErrorCount, Is.Zero);
                Assert.That(report.ExceptionCount, Is.Zero);
            }

            int aliveEnemies = 0;
            foreach (EnemyBrain enemy in Object.FindObjectsByType<EnemyBrain>())
            {
                if (enemy != null && !enemy.IsDead)
                {
                    aliveEnemies++;
                }
            }

            Assert.That(aliveEnemies, Is.LessThanOrEqualTo(15));
        }

        [Test]
        public void AcceleratedSixtyMinuteSystemsSoakHasNoErrors()
        {
            DayNightSystem dayNight = Object.FindAnyObjectByType<DayNightSystem>();
            WeatherSystem weather = Object.FindAnyObjectByType<WeatherSystem>();
            SkillManager skills = Object.FindAnyObjectByType<SkillManager>();
            IInventoryService inventory = ServiceLocator.Get<IInventoryService>();
            IQuestService quests = ServiceLocator.Get<IQuestService>();
            Assert.That(dayNight, Is.Not.Null);
            Assert.That(weather, Is.Not.Null);
            Assert.That(skills, Is.Not.Null);
            Assert.That(
                quests.Accept(QuestContentIds.MainRootAwakening),
                Is.True);

            using (var diagnostics = new MvpRuntimeDiagnostics())
            {
                diagnostics.Begin(MvpRuntimeDiagnostics.StabilityDurationSeconds);
                for (int second = 1;
                     second <= (int)MvpRuntimeDiagnostics.StabilityDurationSeconds;
                     second++)
                {
                    dayNight.TickTime(1f);
                    weather.TickWeather(1f);
                    skills.TickCooldowns(1f);
                    diagnostics.AdvanceSimulation(1f);
                    diagnostics.SampleFrame(1f / 60f);

                    if (second % 60 == 0)
                    {
                        Assert.That(
                            quests.Accept(QuestContentIds.MainRootAwakening),
                            Is.False);
                        Assert.That(
                            inventory.AddItem(
                                InventoryContentIds.HealPotion01,
                                1,
                                AcquireSource.Cheat),
                            Is.True);
                        Assert.That(
                            inventory.RemoveItem(
                                InventoryContentIds.HealPotion01,
                                1),
                            Is.True);
                        Assert.That(SaveManager.Instance.SaveGame(0),
                            Is.True,
                            SaveManager.Instance.LastError);
                    }

                    if (second % 600 == 0)
                    {
                        Assert.That(SaveManager.Instance.LoadGame(0),
                            Is.True,
                            SaveManager.Instance.LastError);
                    }
                }

                MvpRuntimeDiagnosticsReport report = diagnostics.Report;
                TestContext.WriteLine(
                    $"G07-03 accelerated soak: simulated={report.SimulatedSeconds:0}s; "
                    + $"saveCycles=60; loadCycles=6; errors={report.ErrorCount}; "
                    + $"exceptions={report.ExceptionCount}; "
                    + $"memory={report.PeakAllocatedMemoryBytes / (1024f * 1024f):0.0}MiB.");
                Assert.That(report.SimulatedSeconds,
                    Is.EqualTo(MvpRuntimeDiagnostics.StabilityDurationSeconds));
                Assert.That(report.DurationComplete, Is.True);
                Assert.That(report.AverageFramesPerSecond,
                    Is.EqualTo(60f).Within(0.01f));
                Assert.That(report.MeetsMemoryBudget, Is.True);
                Assert.That(report.ErrorCount, Is.Zero);
                Assert.That(report.ExceptionCount, Is.Zero);
                Assert.That(report.IsStable, Is.True);
            }

            Assert.That(float.IsNaN(dayNight.TimeOfDay), Is.False);
            Assert.That(float.IsInfinity(dayNight.TimeOfDay), Is.False);
            Assert.That(
                weather.TransitionRemaining,
                Is.InRange(0f, WeatherSystem.TransitionDurationSeconds));
        }

        private static int FindSlot(IInventoryService inventory, string itemId)
        {
            for (int index = 0; index < inventory.Slots.Count; index++)
            {
                if (inventory.Slots[index] != null
                    && inventory.Slots[index].ItemId == itemId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static void EnterPlayingState()
        {
            GameManager manager = GameManager.Instance;
            if (manager.State == GameState.Boot)
            {
                Assert.That(manager.TrySetState(GameState.MainMenu), Is.True);
            }

            if (manager.State == GameState.MainMenu)
            {
                Assert.That(manager.TrySetState(GameState.Loading), Is.True);
            }

            if (manager.State == GameState.Loading)
            {
                Assert.That(manager.TrySetState(GameState.Playing), Is.True);
            }
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<DayNightSystem>();
            DestroyAll<WeatherSystem>();
            DestroyAll<AudioStateController>();
            DestroyAll<CombatFeedbackController>();
            DestroyAll<AudioManager>();
            DestroyAll<VFXManager>();
            DestroyAll<TutorialManager>();
            DestroyAll<SkillProjectile>();
            DestroyAll<SkillManager>();
            DestroyAll<DialogueManager>();
            DestroyAll<DailyQuestManager>();
            DestroyAll<QuestManager>();
            DestroyAll<SerendipitySystem>();
            DestroyAll<AchievementManager>();
            DestroyAll<TitleManager>();
            DestroyAll<FactionManager>();
            DestroyAll<MountManager>();
            DestroyAll<LootSystem>();
            DestroyAll<CultivationManager>();
            DestroyAll<BodyRefinementManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<RefineSystem>();
            DestroyAll<EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<ShopSystem>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatFeelController>();
            DestroyAll<CombatSystem>();
            DestroyAll<BlackwindDungeonSystem>();
            DestroyAll<MapTravelSystem>();
            DestroyAll<SafeZoneSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            foreach (T instance in instances)
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }
        }
    }
}
