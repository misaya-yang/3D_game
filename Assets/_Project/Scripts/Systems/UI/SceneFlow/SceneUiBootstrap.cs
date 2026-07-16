using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Crafting;
using Wendao.UI.Cultivation;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using Wendao.UI.Debugging;
#endif
using Wendao.UI.Inventory;
using Wendao.UI.NPC;
using Wendao.UI.Quest;
using Wendao.UI.Shop;
using Wendao.UI.Skill;
using Wendao.UI.Tutorial;

namespace Wendao.UI.SceneFlow
{
    public static class SceneUiBootstrap
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
            EnsureForScene(SceneManager.GetActiveScene());
        }

        public static void EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            bool isGameplayScene = IsGameplayScene(scene);

            if (scene.name == SceneLoader.MainMenuSceneName
                && Object.FindAnyObjectByType<MainMenuView>(FindObjectsInactive.Include) == null)
            {
                new GameObject("MainMenuView").AddComponent<MainMenuView>();
            }
            else if (scene.name == SceneLoader.LoadingSceneName
                && Object.FindAnyObjectByType<LoadingView>(FindObjectsInactive.Include) == null)
            {
                new GameObject("LoadingView").AddComponent<LoadingView>();
            }

            bool createdTutorialToast = false;
            if (isGameplayScene
                && Object.FindAnyObjectByType<TutorialToastView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("TutorialToastView").AddComponent<TutorialToastView>();
                createdTutorialToast = true;
            }

            if (createdTutorialToast
                && ServiceLocator.TryGet<ITutorialService>(
                    out ITutorialService tutorialService))
            {
                tutorialService.RepublishActivePrompt();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<UIManager>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("UIManager").AddComponent<UIManager>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<GameplayMenuBarView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("GameplayMenuBarView")
                    .AddComponent<GameplayMenuBarView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<DamageFloatingTextView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("DamageFloatingTextView")
                    .AddComponent<DamageFloatingTextView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<BossHealthBarView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("BossHealthBarView")
                    .AddComponent<BossHealthBarView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<LockOnMarkerView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("LockOnMarkerView")
                    .AddComponent<LockOnMarkerView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<DeathView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("DeathView").AddComponent<DeathView>();
            }

            GameToastView gameToast = null;
            if (isGameplayScene)
            {
                gameToast = Object.FindAnyObjectByType<GameToastView>(
                    FindObjectsInactive.Include);
                if (gameToast == null)
                {
                    gameToast = new GameObject("GameToastView")
                        .AddComponent<GameToastView>();
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (gameToast.GetComponent<DebugConsoleView>() == null)
                {
                    gameToast.gameObject.AddComponent<DebugConsoleView>();
                }
#endif
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<InventoryPanelView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("InventoryPanelView").AddComponent<InventoryPanelView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<SkillQuickbarView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("SkillQuickbarView").AddComponent<SkillQuickbarView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<SkillPanelView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("SkillPanelView").AddComponent<SkillPanelView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<AlchemyPanelView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("AlchemyPanelView").AddComponent<AlchemyPanelView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<ShopPanelView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("ShopPanelView").AddComponent<ShopPanelView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<CombatStatusHudView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("CombatStatusHudView")
                    .AddComponent<CombatStatusHudView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<CultivationHudView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("CultivationHudView").AddComponent<CultivationHudView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<CharacterPanelView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("CharacterPanelView")
                    .AddComponent<CharacterPanelView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<BreakthroughCeremonyView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("BreakthroughCeremonyView")
                    .AddComponent<BreakthroughCeremonyView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<SpiritRootSelectionView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("SpiritRootSelectionView")
                    .AddComponent<SpiritRootSelectionView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<QuestTrackerView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("QuestTrackerView").AddComponent<QuestTrackerView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<QuestWorldMarkerView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("QuestWorldMarkerView")
                    .AddComponent<QuestWorldMarkerView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<QuestPanelView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("QuestPanelView").AddComponent<QuestPanelView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<MapPanelView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("MapPanelView").AddComponent<MapPanelView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<PausePanelView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("PausePanelView").AddComponent<PausePanelView>();
            }

            if (isGameplayScene
                && Object.FindAnyObjectByType<DialogueView>(
                    FindObjectsInactive.Include) == null)
            {
                new GameObject("DialogueView").AddComponent<DialogueView>();
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static bool IsGameplayScene(Scene scene)
        {
            return scene.name == SceneLoader.DefaultMapSceneName
                || scene.name == SceneLoader.CangwuMapSceneName
                || scene.name == SceneLoader.BlackwindDungeonSceneName;
        }
    }
}
