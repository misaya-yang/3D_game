using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0502PlayModeTests
    {
        private string _storageRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            PlayerRuntimeBootstrap.Install();
            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0502Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.CangwuMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            Scene scene = SceneManager.GetActiveScene();
            CangwuGreyboxFactory.EnsureCreated(scene);
            PlayerRuntimeBootstrap.EnsureForScene(scene);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
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

        [Test]
        public void CangwuSceneIsRegisteredAndContainsFiveLocalizedAreas()
        {
            SceneLoader loader = SceneLoader.Instance;
            Assert.That(loader.TryGetSceneName(
                MapContentIds.CangwuMap,
                out string sceneName), Is.True);
            Assert.That(sceneName, Is.EqualTo(SceneLoader.CangwuMapSceneName));
            Assert.That(Application.CanStreamedLevelBeLoaded(sceneName), Is.True);

            WorldAreaMarker[] markers = Object.FindObjectsByType<WorldAreaMarker>();
            Assert.That(markers,
                Has.Length.EqualTo(CangwuGreyboxFactory.RequiredAreaCount));
            var areaIds = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldAreaMarker marker in markers)
            {
                Assert.That(areaIds.Add(marker.AreaId), Is.True, marker.AreaId);
                Assert.That(keys.Add(marker.NameLocalizationKey),
                    Is.True,
                    marker.NameLocalizationKey);
                Assert.That(marker.DefaultName, Is.Not.Empty);
            }

            Assert.That(areaIds, Is.EquivalentTo(new[]
            {
                "area_cangwu_gate_platform",
                "area_cangwu_mountain_road",
                "area_cangwu_mist_valley",
                "area_cangwu_cave",
                "area_cangwu_thunder_terrace"
            }));
            Assert.That(GameObject.Find(
                CangwuGreyboxFactory.DefaultSpawnName), Is.Not.Null);
            Assert.That(GameObject.Find(
                "Cangwu_AlchemyFurnace_Greybox"), Is.Not.Null);
            Assert.That(GameObject.Find(
                "Cangwu_QuestBoard_Greybox"), Is.Not.Null);
        }

        [Test]
        public void CangwuPlayerSpawnsAtRequestedTeleportPoint()
        {
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            TeleportPoint point = Object.FindAnyObjectByType<TeleportPoint>();
            Assert.That(player, Is.Not.Null);
            Assert.That(point, Is.Not.Null);
            Assert.That(point.TeleportId,
                Is.EqualTo(MapContentIds.CangwuGateTeleport));
            Assert.That(Vector3.Distance(
                player.transform.position,
                point.transform.position), Is.LessThan(0.1f));
        }

        [Test]
        public void FirstStepOnTeleportUnlocksAndPersistsMapAndPoint()
        {
            SaveManager save = SaveManager.Instance;
            IMapTravelService travel = ServiceLocator.Get<IMapTravelService>();
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            TeleportPoint point = Object.FindAnyObjectByType<TeleportPoint>();
            save.World.UnlockedMaps.Remove(MapContentIds.CangwuMap);
            save.World.UnlockedTeleports.Remove(
                MapContentIds.CangwuGateTeleport);

            Assert.That(point.TryDiscover(player.gameObject), Is.True);
            Assert.That(travel.IsMapUnlocked(MapContentIds.CangwuMap), Is.True);
            Assert.That(travel.IsTeleportUnlocked(
                MapContentIds.CangwuGateTeleport), Is.True);
            Assert.That(save.SaveGame(0), Is.True, save.LastError);

            save.World.UnlockedMaps.Remove(MapContentIds.CangwuMap);
            save.World.UnlockedTeleports.Remove(
                MapContentIds.CangwuGateTeleport);
            Assert.That(save.LoadGame(0), Is.True, save.LastError);
            Assert.That(save.World.UnlockedMaps,
                Does.Contain(MapContentIds.CangwuMap));
            Assert.That(save.World.UnlockedTeleports,
                Does.Contain(MapContentIds.CangwuGateTeleport));
        }

        [UnityTest]
        public IEnumerator MainQuestFlagControlsSecretPathAndOpenGateLoadsCangwu()
        {
            AsyncOperation qingshiLoad = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(qingshiLoad, Is.Not.Null);
            yield return qingshiLoad;
            QingshiGreyboxFactory.EnsureCreated(SceneManager.GetActiveScene());
            PlayerController player = PlayerRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            CangwuPathGate gate = Object.FindAnyObjectByType<CangwuPathGate>();
            Assert.That(gate, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            ToastInfo observedToast = default;
            var toastCalls = 0;
            Action<ToastInfo> toastHandler = info =>
            {
                observedToast = info;
                toastCalls++;
            };
            EventBus.Subscribe(Wendao.Systems.UiEvents.ToastRequested, toastHandler);
            try
            {
                SaveManager.Instance.World.QuestFlags[
                    MapContentIds.CangwuPathOpenFlag] = false;
                Assert.That(gate.TryEnter(player.gameObject), Is.False);
                Assert.That(toastCalls, Is.EqualTo(1));
                Assert.That(observedToast.LocalizationKey,
                    Is.EqualTo(CangwuPathGate.LockedLocalizationKey));
                Assert.That(SceneManager.GetActiveScene().name,
                    Is.EqualTo(SceneLoader.DefaultMapSceneName));

                SaveManager.Instance.World.QuestFlags[
                    MapContentIds.CangwuPathOpenFlag] = true;
                Assert.That(gate.TryEnter(player.gameObject), Is.True);

                float deadline = Time.realtimeSinceStartup + 15f;
                while ((SceneLoader.Instance.IsLoading
                        || SceneManager.GetActiveScene().name
                            != SceneLoader.CangwuMapSceneName)
                    && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }
            finally
            {
                EventBus.Unsubscribe(
                    Wendao.Systems.UiEvents.ToastRequested,
                    toastHandler);
            }

            Assert.That(SceneManager.GetActiveScene().name,
                Is.EqualTo(SceneLoader.CangwuMapSceneName));
            Assert.That(SceneLoader.Instance.LastLoadedMapId,
                Is.EqualTo(MapContentIds.CangwuMap));
            Assert.That(SaveManager.Instance.World.UnlockedMaps,
                Does.Contain(MapContentIds.CangwuMap));
            PlayerController cangwuPlayer =
                Object.FindAnyObjectByType<PlayerController>();
            TeleportPoint cangwuPoint =
                Object.FindAnyObjectByType<TeleportPoint>();
            Assert.That(Vector3.Distance(
                cangwuPlayer.transform.position,
                cangwuPoint.transform.position), Is.LessThan(0.1f));
        }

        private static void EnterPlayingState()
        {
            GameManager manager = GameManager.Instance;
            Assert.That(manager, Is.Not.Null);
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
            DestroyAll<BlackwindDungeonSystem>();
            DestroyAll<MapTravelSystem>();
            DestroyAll<Wendao.UI.Combat.DeathView>();
            DestroyAll<SafeZoneSystem>();
            DestroyAll<Wendao.Entities.Enemy.BossArenaController>();
            DestroyAll<Wendao.Entities.Enemy.EnemyBrain>();
            DestroyAll<Wendao.Entities.Enemy.EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<Wendao.Entities.Enemy.TrainingDummy>();
            DestroyAll<Wendao.CameraSystem.ThirdPersonCamera>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<Wendao.Systems.Cultivation.BodyRefinementManager>();
            DestroyAll<Wendao.Systems.Cultivation.CultivationManager>();
            DestroyAll<Wendao.Systems.Cultivation.SpiritRootSystem>();
            DestroyAll<Wendao.Systems.Equipment.RefineSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<Wendao.Systems.Inventory.ItemUseSystem>();
            DestroyAll<Wendao.Systems.Inventory.InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<Wendao.Systems.Combat.StatusEffectManager>();
            DestroyAll<Wendao.Systems.Combat.CombatSystem>();
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
