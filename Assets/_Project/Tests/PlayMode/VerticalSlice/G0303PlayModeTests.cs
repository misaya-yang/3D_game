using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Player;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.World;
using Wendao.UI.Crafting;
using Wendao.UI.SceneFlow;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0303PlayModeTests
    {
        private string _storageRoot;
        private SaveManager _save;
        private AlchemySystem _alchemy;
        private IInventoryService _inventory;
        private AlchemyPanelView _panel;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();

            SceneFlowBootstrap.Install();
            SceneUiBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0303Tests_" + Guid.NewGuid().ToString("N"));
            _save = SaveManager.Instance;
            _save.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
            yield return null;

            _alchemy = Object.FindAnyObjectByType<AlchemySystem>();
            _inventory = ServiceLocator.Get<IInventoryService>();
            _panel = Object.FindAnyObjectByType<AlchemyPanelView>();
            Assert.That(_alchemy, Is.Not.Null);
            Assert.That(_inventory, Is.Not.Null);
            Assert.That(_panel, Is.Not.Null);
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
        public void FourRecipesAndTheirMaterialsMatchTheContentTable()
        {
            Assert.That(AlchemyContentIds.Recipes, Has.Length.EqualTo(4));
            AssertRecipe(
                AlchemyContentIds.HealRecipe,
                1,
                0.9f,
                InventoryContentIds.HealPotion01,
                InventoryContentIds.QingxinGrass,
                2,
                InventoryContentIds.SpiritDust,
                1);
            AssertRecipe(
                AlchemyContentIds.ManaRecipe,
                1,
                0.9f,
                InventoryContentIds.ManaPotion01,
                InventoryContentIds.QingxinGrass,
                1,
                InventoryContentIds.SpiritDust,
                2);
            AssertRecipe(
                AlchemyContentIds.BodyRecipe,
                2,
                0.75f,
                InventoryContentIds.BodyPotion01,
                InventoryContentIds.WolfHair,
                3,
                InventoryContentIds.SpiritDust,
                2);
            AssertRecipe(
                AlchemyContentIds.CultivationRecipe,
                3,
                0.7f,
                InventoryContentIds.CultivationPotion01,
                InventoryContentIds.BeastCore01,
                1,
                InventoryContentIds.SpiritDust,
                3);

            ItemData manaPotion = ConfigDatabase.Instance.GetItem(
                InventoryContentIds.ManaPotion01);
            ItemData cultivationPotion = ConfigDatabase.Instance.GetItem(
                InventoryContentIds.CultivationPotion01);
            Assert.That(
                manaPotion.UseEffects[0].EffectType,
                Is.EqualTo(UseEffectType.RestoreMana));
            Assert.That(manaPotion.UseEffects[0].Value, Is.EqualTo(50f));
            Assert.That(
                cultivationPotion.UseEffects[0].EffectType,
                Is.EqualTo(UseEffectType.AddCultivationXp));
            Assert.That(cultivationPotion.UseEffects[0].Value, Is.EqualTo(300f));
        }

        [Test]
        public void SuccessfulCraftConsumesMaterialsAwardsProductXpAndEvent()
        {
            AddHealIngredients(1);
            _alchemy.SetRandomValueProvider(() => 0f);
            CraftResultInfo observed = default;
            var completed = 0;
            var failed = 0;
            Action<CraftResultInfo> completedHandler = info =>
            {
                observed = info;
                completed++;
            };
            Action<CraftResultInfo> failedHandler = _ => failed++;
            EventBus.Subscribe(AlchemyEvents.CraftCompleted, completedHandler);
            EventBus.Subscribe(AlchemyEvents.CraftFailed, failedHandler);
            try
            {
                Assert.That(_alchemy.CanCraft(AlchemyContentIds.HealRecipe), Is.True);
                Assert.That(_alchemy.Craft(AlchemyContentIds.HealRecipe), Is.True);
            }
            finally
            {
                EventBus.Unsubscribe(AlchemyEvents.CraftCompleted, completedHandler);
                EventBus.Unsubscribe(AlchemyEvents.CraftFailed, failedHandler);
            }

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(failed, Is.Zero);
            Assert.That(observed.Success, Is.True);
            Assert.That(observed.RecipeId, Is.EqualTo(AlchemyContentIds.HealRecipe));
            Assert.That(observed.ResultItemId, Is.EqualTo(InventoryContentIds.HealPotion01));
            Assert.That(observed.ResultCount, Is.EqualTo(1));
            Assert.That(_inventory.CountItem(InventoryContentIds.QingxinGrass), Is.Zero);
            Assert.That(_inventory.CountItem(InventoryContentIds.SpiritDust), Is.Zero);
            Assert.That(_inventory.CountItem(InventoryContentIds.HealPotion01), Is.EqualTo(1));
            Assert.That(_alchemy.Xp, Is.EqualTo(100f));
            Assert.That(_alchemy.Level, Is.EqualTo(1));

            string saveDirectory = Path.Combine(_storageRoot, "SaveSlot_0");
            StringAssert.Contains(
                InventoryContentIds.HealPotion01,
                File.ReadAllText(Path.Combine(
                    saveDirectory,
                    InventoryManager.SaveModuleName + ".json")));
            StringAssert.Contains(
                "\"xp\": 100.0",
                File.ReadAllText(Path.Combine(
                    saveDirectory,
                    AlchemySystem.SaveModuleName + ".json")));
        }

        [Test]
        public void FailedCraftPublishesFailureConsumesCatalystAndRefundsMainMaterial()
        {
            AddHealIngredients(1);
            _alchemy.SetRandomValueProvider(() => 1f);
            CraftResultInfo observed = default;
            var completed = 0;
            var failed = 0;
            var refundAcquireEvents = 0;
            Action<CraftResultInfo> completedHandler = _ => completed++;
            Action<CraftResultInfo> failedHandler = info =>
            {
                observed = info;
                failed++;
            };
            Action<ItemAcquireInfo> acquiredHandler = _ => refundAcquireEvents++;
            EventBus.Subscribe(AlchemyEvents.CraftCompleted, completedHandler);
            EventBus.Subscribe(AlchemyEvents.CraftFailed, failedHandler);
            EventBus.Subscribe(InventoryEvents.ItemAcquired, acquiredHandler);
            try
            {
                Assert.That(_alchemy.Craft(AlchemyContentIds.HealRecipe), Is.False);
            }
            finally
            {
                EventBus.Unsubscribe(AlchemyEvents.CraftCompleted, completedHandler);
                EventBus.Unsubscribe(AlchemyEvents.CraftFailed, failedHandler);
                EventBus.Unsubscribe(InventoryEvents.ItemAcquired, acquiredHandler);
            }

            Assert.That(completed, Is.Zero);
            Assert.That(failed, Is.EqualTo(1));
            Assert.That(refundAcquireEvents, Is.Zero);
            Assert.That(observed.Success, Is.False);
            Assert.That(observed.RecipeId, Is.EqualTo(AlchemyContentIds.HealRecipe));
            Assert.That(observed.ResultItemId, Is.Empty);
            Assert.That(_inventory.CountItem(InventoryContentIds.QingxinGrass), Is.EqualTo(2));
            Assert.That(_inventory.CountItem(InventoryContentIds.SpiritDust), Is.Zero);
            Assert.That(_inventory.CountItem(InventoryContentIds.HealPotion01), Is.Zero);
            Assert.That(_alchemy.Xp, Is.Zero);
        }

        [Test]
        public void CumulativeXpRaisesLevelAddsSuccessBonusAndRoundTrips()
        {
            AddHealIngredients(2);
            _alchemy.SetRandomValueProvider(() => 0f);
            Assert.That(_alchemy.Craft(AlchemyContentIds.HealRecipe), Is.True);
            Assert.That(_alchemy.Craft(AlchemyContentIds.HealRecipe), Is.True);
            Assert.That(_alchemy.Xp, Is.EqualTo(200f));
            Assert.That(_alchemy.Level, Is.EqualTo(2));
            Assert.That(
                _alchemy.GetSuccessRate(AlchemyContentIds.HealRecipe),
                Is.EqualTo(0.93f).Within(0.0001f));
            Assert.That(
                _alchemy.GetSuccessRate(AlchemyContentIds.BodyRecipe),
                Is.EqualTo(0.78f).Within(0.0001f));

            _alchemy.RestoreSaveData(new AlchemySaveData());
            Assert.That(_alchemy.Level, Is.EqualTo(1));
            Assert.That(_alchemy.Xp, Is.Zero);
            Assert.That(_save.LoadGame(0), Is.True, _save.LastError);
            Assert.That(_alchemy.Level, Is.EqualTo(2));
            Assert.That(_alchemy.Xp, Is.EqualTo(200f));
        }

        [Test]
        public void MinimalAlchemyPanelListsRecipesCraftsAndRestoresGameplayInput()
        {
            IPlayerInputSource input = ServiceLocator.Get<IPlayerInputSource>();
            Assert.That(input.IsEnabled, Is.True);
            Assert.That(_panel.RecipeButtonCount, Is.EqualTo(4));

            _panel.SetOpen(true);
            _panel.SelectRecipe(AlchemyContentIds.HealRecipe);
            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(input.IsEnabled, Is.False);
            StringAssert.Contains("回血丹", _panel.GetRecipeRowText(0));
            Assert.That(_panel.TryCraftSelected(), Is.False);
            Assert.That(
                _panel.CurrentStatusLocalizationKey,
                Is.EqualTo(AlchemyPanelView.MissingMaterialsLocalizationKey));

            AddHealIngredients(1);
            _alchemy.SetRandomValueProvider(() => 0f);
            Assert.That(_panel.TryCraftSelected(), Is.True);
            Assert.That(
                _panel.CurrentStatusLocalizationKey,
                Is.EqualTo(AlchemySystem.SuccessToastKey));
            Assert.That(_inventory.CountItem(InventoryContentIds.HealPotion01), Is.EqualTo(1));

            _panel.SetOpen(false);
            Assert.That(_panel.IsOpen, Is.False);
            Assert.That(input.IsEnabled, Is.True);
        }

        private void AddHealIngredients(int craftCount)
        {
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.QingxinGrass,
                    2 * craftCount,
                    AcquireSource.Cheat),
                Is.True);
            Assert.That(
                _inventory.AddItem(
                    InventoryContentIds.SpiritDust,
                    craftCount,
                    AcquireSource.Cheat),
                Is.True);
        }

        private static void AssertRecipe(
            string recipeId,
            int requiredLevel,
            float successRate,
            string resultItemId,
            string firstIngredientId,
            int firstIngredientCount,
            string secondIngredientId,
            int secondIngredientCount)
        {
            CraftRecipeData recipe = ConfigDatabase.Instance.GetRecipe(recipeId);
            Assert.That(recipe, Is.Not.Null, recipeId);
            Assert.That(recipe.CraftType, Is.EqualTo(CraftType.Alchemy));
            Assert.That(recipe.RequiredCraftLevel, Is.EqualTo(requiredLevel));
            Assert.That(recipe.BaseSuccessRate, Is.EqualTo(successRate).Within(0.0001f));
            Assert.That(recipe.SuccessResult.ItemId, Is.EqualTo(resultItemId));
            Assert.That(recipe.Ingredients, Has.Length.EqualTo(2));
            Assert.That(recipe.Ingredients[0].ItemId, Is.EqualTo(firstIngredientId));
            Assert.That(recipe.Ingredients[0].Count, Is.EqualTo(firstIngredientCount));
            Assert.That(recipe.Ingredients[0].ConsumedOnFail, Is.False);
            Assert.That(recipe.Ingredients[1].ItemId, Is.EqualTo(secondIngredientId));
            Assert.That(recipe.Ingredients[1].Count, Is.EqualTo(secondIngredientCount));
            Assert.That(recipe.Ingredients[1].ConsumedOnFail, Is.True);
        }

        private static void EnterPlayingState()
        {
            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            if (gameManager.State == GameState.Boot)
            {
                Assert.That(gameManager.TrySetState(GameState.MainMenu), Is.True);
            }

            if (gameManager.State != GameState.Playing)
            {
                Assert.That(gameManager.TrySetState(GameState.Loading), Is.True);
                Assert.That(gameManager.TrySetState(GameState.Playing), Is.True);
            }

            gameManager.SetCombatFlag(false);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<AlchemyPanelView>();
            DestroyAll<Wendao.UI.Shop.ShopPanelView>();
            DestroyAll<AlchemySystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<GatheringSystem>();
            DestroyAll<Wendao.Entities.Player.PlayerController>();
            DestroyAll<Wendao.Systems.Skill.SkillManager>();
            DestroyAll<Wendao.Systems.Equipment.RefineSystem>();
            DestroyAll<Wendao.Systems.Cultivation.BodyRefinementManager>();
            DestroyAll<Wendao.Systems.Cultivation.CultivationManager>();
            DestroyAll<Wendao.Systems.Cultivation.SpiritRootSystem>();
            DestroyAll<Wendao.Systems.Equipment.EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
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
