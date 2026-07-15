using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.World;
using Wendao.Systems.Tutorial;
using Wendao.UI.SceneFlow;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.SceneFlow
{
    public sealed class SceneLoaderPlayModeTests
    {
        private string _storageRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EventBus.Clear();
            DestroyRuntimeServices();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            SceneUiBootstrap.Install();
            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoSceneFlowTests_" + System.Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);

            AsyncOperation bootLoad = SceneManager.LoadSceneAsync(
                SceneLoader.BootSceneName,
                LoadSceneMode.Single);
            Assert.That(bootLoad, Is.Not.Null);
            yield return bootLoad;

            float deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != SceneLoader.MainMenuSceneName
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneLoader.MainMenuSceneName));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EventBus.Clear();
            DestroyRuntimeServices();
            yield return null;
            ServiceLocator.Clear();
            if (!string.IsNullOrEmpty(_storageRoot) && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [UnityTest]
        public IEnumerator MainMenuButtonLoadsQingshiThroughLoadingSceneAndPublishesMapInfo()
        {
            MainMenuView mainMenu = Object.FindAnyObjectByType<MainMenuView>();
            Assert.That(mainMenu, Is.Not.Null);
            Assert.That(mainMenu.StartButton, Is.Not.Null);
            Assert.That(
                FindText(mainMenu, "Title")?.text,
                Is.EqualTo(MainMenuView.TitleDefaultValue));
            Assert.That(
                FindText(mainMenu, "Label")?.text,
                Is.EqualTo(MainMenuView.StartGameDefaultValue));
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(
                EventSystem.current.GetComponent<InputSystemUIInputModule>(),
                Is.Not.Null);

            SceneLoader loader = SceneLoader.Instance;
            Assert.That(loader, Is.Not.Null);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.MainMenu));

            var observedProgress = new List<float>();
            MapInfo observedMap = default;
            var mapEventCalls = 0;
            var loadingSceneObserved = false;
            var loadingViewObserved = false;
            var loadingTextObserved = false;
            var loadingProgressTextObserved = false;
            Action<float> progressHandler = value => observedProgress.Add(value);
            Action<MapInfo> mapHandler = info =>
            {
                observedMap = info;
                mapEventCalls++;
            };
            loader.ProgressChanged += progressHandler;
            EventBus.Subscribe(SceneLoader.MapLoadedEvent, mapHandler);

            try
            {
                mainMenu.StartButton.onClick.Invoke();
                Assert.That(loader.IsLoading, Is.True);

                float deadline = Time.realtimeSinceStartup + 15f;
                while ((loader.IsLoading
                        || SceneManager.GetActiveScene().name != SceneLoader.DefaultMapSceneName)
                    && Time.realtimeSinceStartup < deadline)
                {
                    if (SceneManager.GetActiveScene().name == SceneLoader.LoadingSceneName)
                    {
                        loadingSceneObserved = true;
                        LoadingView loadingView = Object.FindAnyObjectByType<LoadingView>();
                        loadingViewObserved |= loadingView != null;
                        loadingTextObserved |= loadingView != null
                            && FindText(loadingView, "LoadingLabel")?.text
                                == LoadingView.LoadingDefaultValue;
                        loadingProgressTextObserved |= loadingView != null
                            && loadingView.ProgressText != null
                            && loadingView.ProgressText.text.StartsWith("进度 ")
                            && loadingView.ProgressText.text.EndsWith("%");
                    }

                    yield return null;
                }
            }
            finally
            {
                loader.ProgressChanged -= progressHandler;
                EventBus.Unsubscribe(SceneLoader.MapLoadedEvent, mapHandler);
            }

            Assert.That(loadingSceneObserved, Is.True);
            Assert.That(loadingViewObserved, Is.True);
            Assert.That(loadingTextObserved, Is.True);
            Assert.That(loadingProgressTextObserved, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneLoader.DefaultMapSceneName));
            Assert.That(loader.IsLoading, Is.False);
            Assert.That(loader.Progress, Is.EqualTo(1f));
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
            Assert.That(mapEventCalls, Is.EqualTo(1));
            Assert.That(observedMap.MapId, Is.EqualTo(SceneLoader.DefaultMapId));
            Assert.That(observedMap.SpawnId, Is.Empty);
            Assert.That(observedProgress.Count, Is.GreaterThan(1));
            Assert.That(observedProgress[observedProgress.Count - 1], Is.EqualTo(1f));
            for (int i = 1; i < observedProgress.Count; i++)
            {
                Assert.That(
                    observedProgress[i],
                    Is.GreaterThanOrEqualTo(observedProgress[i - 1]));
            }

            Assert.That(GameObject.Find(QingshiGreyboxFactory.GroundName), Is.Not.Null);
            Assert.That(GameObject.Find(QingshiGreyboxFactory.DefaultSpawnName), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
        }

        private static void DestroyRuntimeServices()
        {
            GameObject player = GameObject.Find("Player_Greybox");
            if (player != null)
            {
                Object.Destroy(player);
            }

            DestroyAll<SceneLoader>();
            DestroyAll<BlackwindDungeonSystem>();
            DestroyAll<MapTravelSystem>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
            DestroyAll<TutorialManager>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.Entities.Enemy.EnemyBrain>();
            DestroyAll<Wendao.Entities.Enemy.EnemySpawner>();
        }

        private static Text FindText(Component root, string objectName)
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.name == objectName)
                {
                    return text;
                }
            }

            return null;
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
