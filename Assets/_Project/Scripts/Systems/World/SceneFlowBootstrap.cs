using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Crafting;
using Wendao.Systems.Achievement;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using Wendao.Systems.Debugging;
#endif
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
using Wendao.Systems.Tutorial;
using Wendao.Systems.Title;

namespace Wendao.Systems.World
{
    public static class SceneFlowBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtRuntime()
        {
            Install();
        }

        public static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureServices();
            SerendipityRuntimeBootstrap.Install();
        }

        public static void EnsureServices()
        {
            EnsureGameManager();
            EnsureSaveManager();
            EnsureConfigDatabase();
            EnsureVfxManager();
            EnsureAudioManager();
            EnsureGameSettingsRuntime();
            EnsureAudioStateController();
            EnsureCombatFeedbackController();
            EnsureDayNightSystem();
            EnsureWeatherSystem();
            EnsureSafeZoneSystem();
            EnsureMapTravelSystem();
            EnsureBlackwindDungeonSystem();
            EnsureSpiritRootSystem();
            EnsureBodyRefinementManager();
            EnsureCultivationManager();
            EnsureMountManager();
            EnsureFactionManager();
            EnsureTitleManager();
            EnsureInventoryManager();
            EnsureShopSystem();
            EnsureGatheringSystem();
            EnsureAlchemySystem();
            EnsureLootSystem();
            EnsureItemUseSystem();
            EnsureEquipmentManager();
            EnsureRefineSystem();
            EnsureQuestManager();
            EnsureDailyQuestManager();
            EnsureAchievementManager();
            EnsureSerendipitySystem();
            EnsureDialogueManager();
            EnsureSceneLoader();
            EnsureStatusEffectManager();
            EnsureCombatFeelController();
            EnsureCombatSystem();
            EnsureSkillManager();
            EnsureTutorialManager();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            EnsureDebugConsoleService();
#endif
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureServices();

            if (scene.name == SceneLoader.BootSceneName)
            {
                SceneLoader loader = SceneLoader.Instance;
                if (loader != null && !loader.IsLoading)
                {
                    loader.LoadMainMenu();
                }

                return;
            }

            if (scene.name == SceneLoader.MainMenuSceneName)
            {
                GameManager gameManager = GameManager.Instance;
                if (gameManager != null && gameManager.State != GameState.MainMenu)
                {
                    gameManager.TrySetState(GameState.MainMenu);
                }

                return;
            }

            if (scene.name == SceneLoader.DefaultMapSceneName)
            {
                QingshiGreyboxFactory.EnsureCreated(scene);
            }
            else if (scene.name == SceneLoader.CangwuMapSceneName)
            {
                CangwuGreyboxFactory.EnsureCreated(scene);
            }
            else if (scene.name == SceneLoader.BlackwindDungeonSceneName)
            {
                BlackwindDungeonFactory.EnsureCreated(scene);
            }
        }

        private static void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                new GameObject("[GameManager]").AddComponent<GameManager>();
            }
        }

        private static void EnsureSaveManager()
        {
            if (SaveManager.Instance == null)
            {
                new GameObject("[SaveManager]").AddComponent<SaveManager>();
            }
        }

        private static void EnsureConfigDatabase()
        {
            if (ConfigDatabase.Instance != null)
            {
                return;
            }

            var database = new GameObject("[ConfigDatabase]").AddComponent<ConfigDatabase>();
            database.LoadAll();
        }

        private static void EnsureVfxManager()
        {
            VFXManager manager = VFXManager.Instance;
            if (manager == null)
            {
                manager = new GameObject("[VFXManager]")
                    .AddComponent<VFXManager>();
            }

            manager.EnsureRegistered();
        }

        private static void EnsureAudioManager()
        {
            AudioManager manager = AudioManager.Instance;
            if (manager == null)
            {
                manager = new GameObject("[AudioManager]")
                    .AddComponent<AudioManager>();
            }

            manager.EnsureRegistered();
        }

        private static void EnsureGameSettingsRuntime()
        {
            if (Object.FindAnyObjectByType<GameSettingsRuntime>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[GameSettingsRuntime]")
                    .AddComponent<GameSettingsRuntime>();
            }
        }

        private static void EnsureAudioStateController()
        {
            AudioStateController controller =
                Object.FindAnyObjectByType<AudioStateController>(
                    FindObjectsInactive.Include);
            if (controller == null)
            {
                controller = new GameObject("[AudioStateController]")
                    .AddComponent<AudioStateController>();
            }

            controller.EnsureRegistered();
        }

        private static void EnsureCombatFeedbackController()
        {
            if (Object.FindAnyObjectByType<CombatFeedbackController>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[CombatFeedbackController]")
                    .AddComponent<CombatFeedbackController>();
            }
        }

        private static void EnsureDayNightSystem()
        {
            DayNightSystem system = Object.FindAnyObjectByType<DayNightSystem>(
                FindObjectsInactive.Include);
            if (system == null)
            {
                system = new GameObject("[DayNightSystem]")
                    .AddComponent<DayNightSystem>();
            }

            system.EnsureRegistered();
        }

        private static void EnsureWeatherSystem()
        {
            WeatherSystem system = Object.FindAnyObjectByType<WeatherSystem>(
                FindObjectsInactive.Include);
            if (system == null)
            {
                system = new GameObject("[WeatherSystem]")
                    .AddComponent<WeatherSystem>();
            }

            system.EnsureRegistered();
        }

        private static void EnsureSceneLoader()
        {
            if (SceneLoader.Instance == null)
            {
                new GameObject("[SceneLoader]").AddComponent<SceneLoader>();
            }
        }

        private static void EnsureInventoryManager()
        {
            if (Object.FindAnyObjectByType<InventoryManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[InventoryManager]").AddComponent<InventoryManager>();
            }
        }

        private static void EnsureSafeZoneSystem()
        {
            if (Object.FindAnyObjectByType<SafeZoneSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[SafeZoneSystem]")
                    .AddComponent<SafeZoneSystem>();
            }
        }

        private static void EnsureMapTravelSystem()
        {
            if (Object.FindAnyObjectByType<MapTravelSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[MapTravelSystem]")
                    .AddComponent<MapTravelSystem>();
            }
        }

        private static void EnsureBlackwindDungeonSystem()
        {
            if (Object.FindAnyObjectByType<BlackwindDungeonSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[BlackwindDungeonSystem]")
                    .AddComponent<BlackwindDungeonSystem>();
            }
        }

        private static void EnsureShopSystem()
        {
            if (Object.FindAnyObjectByType<ShopSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[ShopSystem]").AddComponent<ShopSystem>();
            }
        }

        private static void EnsureAlchemySystem()
        {
            if (Object.FindAnyObjectByType<AlchemySystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[AlchemySystem]").AddComponent<AlchemySystem>();
            }
        }

        private static void EnsureGatheringSystem()
        {
            if (Object.FindAnyObjectByType<GatheringSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[GatheringSystem]").AddComponent<GatheringSystem>();
            }
        }

        private static void EnsureLootSystem()
        {
            if (Object.FindAnyObjectByType<LootSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[LootSystem]").AddComponent<LootSystem>();
            }
        }

        private static void EnsureSpiritRootSystem()
        {
            if (Object.FindAnyObjectByType<SpiritRootSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[SpiritRootSystem]").AddComponent<SpiritRootSystem>();
            }
        }

        private static void EnsureCultivationManager()
        {
            if (Object.FindAnyObjectByType<CultivationManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[CultivationManager]")
                    .AddComponent<CultivationManager>();
            }
        }

        private static void EnsureMountManager()
        {
            if (Object.FindAnyObjectByType<MountManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[MountManager]").AddComponent<MountManager>();
            }
        }

        private static void EnsureFactionManager()
        {
            if (Object.FindAnyObjectByType<FactionManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[FactionManager]").AddComponent<FactionManager>();
            }
        }

        private static void EnsureTitleManager()
        {
            if (Object.FindAnyObjectByType<TitleManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[TitleManager]").AddComponent<TitleManager>();
            }
        }

        private static void EnsureAchievementManager()
        {
            if (Object.FindAnyObjectByType<AchievementManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[AchievementManager]")
                    .AddComponent<AchievementManager>();
            }
        }

        private static void EnsureBodyRefinementManager()
        {
            if (Object.FindAnyObjectByType<BodyRefinementManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[BodyRefinementManager]")
                    .AddComponent<BodyRefinementManager>();
            }
        }

        private static void EnsureItemUseSystem()
        {
            if (Object.FindAnyObjectByType<ItemUseSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[ItemUseSystem]").AddComponent<ItemUseSystem>();
            }
        }

        private static void EnsureEquipmentManager()
        {
            if (Object.FindAnyObjectByType<EquipmentManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[EquipmentManager]").AddComponent<EquipmentManager>();
            }
        }

        private static void EnsureRefineSystem()
        {
            if (Object.FindAnyObjectByType<RefineSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[RefineSystem]").AddComponent<RefineSystem>();
            }
        }

        private static void EnsureQuestManager()
        {
            if (Object.FindAnyObjectByType<QuestManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[QuestManager]").AddComponent<QuestManager>();
            }
        }

        private static void EnsureDailyQuestManager()
        {
            if (Object.FindAnyObjectByType<DailyQuestManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[DailyQuestManager]")
                    .AddComponent<DailyQuestManager>();
            }
        }

        private static void EnsureSerendipitySystem()
        {
            if (Object.FindAnyObjectByType<SerendipitySystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[SerendipitySystem]")
                    .AddComponent<SerendipitySystem>();
            }
        }

        private static void EnsureDialogueManager()
        {
            if (Object.FindAnyObjectByType<DialogueManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[DialogueManager]")
                    .AddComponent<DialogueManager>();
            }
        }

        private static void EnsureTutorialManager()
        {
            if (Object.FindAnyObjectByType<TutorialManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[TutorialManager]").AddComponent<TutorialManager>();
            }
        }

        private static void EnsureCombatSystem()
        {
            if (Object.FindAnyObjectByType<CombatSystem>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[CombatSystem]").AddComponent<CombatSystem>();
            }
        }

        private static void EnsureCombatFeelController()
        {
            if (Object.FindAnyObjectByType<CombatFeelController>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[CombatFeelController]")
                    .AddComponent<CombatFeelController>();
            }
        }

        private static void EnsureStatusEffectManager()
        {
            if (Object.FindAnyObjectByType<StatusEffectManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[StatusEffectManager]")
                    .AddComponent<StatusEffectManager>();
            }
        }

        private static void EnsureSkillManager()
        {
            if (Object.FindAnyObjectByType<SkillManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("[SkillManager]").AddComponent<SkillManager>();
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static void EnsureDebugConsoleService()
        {
            if (ServiceLocator.TryGet<IDebugConsoleService>(out _))
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return;
            }

            DebugConsoleService service =
                gameManager.GetComponent<DebugConsoleService>();
            if (service == null)
            {
                service = gameManager.gameObject.AddComponent<DebugConsoleService>();
            }

            service.EnsureRegistered();
        }
#endif
    }
}
