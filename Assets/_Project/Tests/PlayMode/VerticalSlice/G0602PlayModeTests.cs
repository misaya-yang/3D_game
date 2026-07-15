using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Crafting;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Inventory;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0602PlayModeTests
    {
        private string _storageRoot;
        private SaveManager _save;
        private TutorialManager _tutorial;
        private TutorialToastView _overlay;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<SaveManager>();
            yield return null;
            ServiceLocator.Clear();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0602Tests_" + Guid.NewGuid().ToString("N"));
            _save = new GameObject("[G0602Save]").AddComponent<SaveManager>();
            _save.ConfigureStorageRoot(_storageRoot);
            _save.World.TutorialsCompleted.Add(TutorialManager.MoveTutorialId);
            _save.World.TutorialsCompleted.Add(TutorialManager.CombatTutorialId);
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);
            _tutorial = new GameObject("[G0602Tutorial]")
                .AddComponent<TutorialManager>();
            _overlay = new GameObject("G0602TutorialOverlay")
                .AddComponent<TutorialToastView>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<SaveManager>();
            yield return null;
            ServiceLocator.Clear();

            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void EightKnownTutorialsAllPublishARealFourPieceCutout()
        {
            _save.World.TutorialsCompleted.Clear();
            Assert.That(_tutorial.KnownTutorialIds.Count, Is.GreaterThanOrEqualTo(8));
            for (int index = 0; index < _tutorial.KnownTutorialIds.Count; index++)
            {
                string tutorialId = _tutorial.KnownTutorialIds[index];
                Assert.That(_tutorial.TryStart(tutorialId), Is.True, tutorialId);
                Assert.That(_overlay.IsVisible, Is.True, tutorialId);
                Assert.That(_overlay.VisibleMaskCount, Is.EqualTo(4), tutorialId);
                Assert.That(
                    _overlay.CurrentFocusRectNormalized.width,
                    Is.GreaterThan(0f),
                    tutorialId);
                Assert.That(
                    _overlay.CurrentFocusRectNormalized.height,
                    Is.GreaterThan(0f),
                    tutorialId);
                _tutorial.Complete(tutorialId);
                Assert.That(_tutorial.HasCompleted(tutorialId), Is.True, tutorialId);
            }
        }

        [Test]
        public void ForcedTutorialGatesActionsWhileOptionalTutorialAllowsAndDismisses()
        {
            _save.World.TutorialsCompleted.Clear();
            Assert.That(_tutorial.TryStart(TutorialManager.MoveTutorialId), Is.True);
            Assert.That(_tutorial.IsForced, Is.True);
            Assert.That(_overlay.IsForcedPrompt, Is.True);
            Assert.That(_overlay.CanDismiss, Is.False);
            Assert.That(
                _tutorial.AllowsInput(TutorialInputAction.Move),
                Is.True);
            Assert.That(
                _tutorial.AllowsInput(TutorialInputAction.HeavyAttack),
                Is.False);
            Assert.That(
                _tutorial.AllowsInput(TutorialInputAction.OpenInventory),
                Is.False);
            Assert.That(
                _tutorial.AllowsInput(TutorialInputAction.Pause),
                Is.True);
            Assert.That(_overlay.DismissCurrent(), Is.False);
            _tutorial.Complete(TutorialManager.MoveTutorialId);

            Assert.That(
                _tutorial.TryStart(TutorialManager.InventoryTutorialId),
                Is.True);
            Assert.That(_tutorial.IsForced, Is.False);
            Assert.That(_overlay.IsForcedPrompt, Is.False);
            Assert.That(_overlay.CanDismiss, Is.True);
            Assert.That(
                _tutorial.AllowsInput(TutorialInputAction.HeavyAttack),
                Is.True);
            Assert.That(
                _tutorial.AllowsInput(TutorialInputAction.OpenInventory),
                Is.True);
            Assert.That(_overlay.DismissCurrent(), Is.True);
            Assert.That(
                _tutorial.HasCompleted(TutorialManager.InventoryTutorialId),
                Is.True);
        }

        [Test]
        public void RuntimeEventsTriggerAndCompleteOptionalTutorials()
        {
            MarkCoreTutorialsCompleted();

            EventBus.Publish(
                InventoryEvents.ItemAcquired,
                new ItemAcquireInfo
                {
                    ItemId = InventoryContentIds.HealPotion01,
                    Count = 1,
                    Source = AcquireSource.Loot
                });
            AssertActive(TutorialManager.InventoryTutorialId);
            EventBus.Publish(
                InventoryEvents.ItemUsed,
                new ItemUseInfo
                {
                    ItemId = InventoryContentIds.HealPotion01,
                    SlotIndex = 0
                });
            Assert.That(
                _tutorial.HasCompleted(TutorialManager.InventoryTutorialId),
                Is.True);

            EventBus.Publish(
                SkillEvents.SkillLearned,
                new SkillInfo
                {
                    SkillId = SkillContentIds.BasicQiBolt,
                    Level = 1
                });
            AssertActive(TutorialManager.SkillTutorialId);
            EventBus.Publish(
                SkillEvents.SkillCast,
                new SkillCastInfo { SkillId = SkillContentIds.BasicQiBolt });
            Assert.That(
                _tutorial.HasCompleted(TutorialManager.SkillTutorialId),
                Is.True);

            var cultivation = new FakeCultivationService { CanBreakthroughValue = true };
            ServiceLocator.Unregister<ICultivationService>();
            ServiceLocator.Register<ICultivationService>(cultivation);
            EventBus.Publish(
                CultivationEvents.XpGained,
                new XpGainInfo { Amount = 100f, Source = XpSourceType.Quest });
            AssertActive(TutorialManager.CultivationTutorialId);
            EventBus.Publish(
                CultivationEvents.RealmBreakthrough,
                new RealmChangeInfo { Success = true });
            Assert.That(
                _tutorial.HasCompleted(TutorialManager.CultivationTutorialId),
                Is.True);

            EventBus.Publish(
                SceneLoader.MapLoadedEvent,
                new MapInfo { MapId = SceneLoader.BlackwindMapId });
            AssertActive(TutorialManager.DungeonTutorialId);
            EventBus.Publish(
                BlackwindDungeonEvents.FloorEntered,
                new BlackwindFloorInfo { Floor = 1, Checkpoint = 0 });
            Assert.That(
                _tutorial.HasCompleted(TutorialManager.DungeonTutorialId),
                Is.True);

            Assert.That(
                _tutorial.RequestStart(TutorialManager.AlchemyTutorialId),
                Is.True);
            AssertActive(TutorialManager.AlchemyTutorialId);
            EventBus.Publish(
                AlchemyEvents.CraftCompleted,
                new CraftResultInfo
                {
                    RecipeId = AlchemyContentIds.HealRecipe,
                    Success = true
                });
            Assert.That(
                _tutorial.HasCompleted(TutorialManager.AlchemyTutorialId),
                Is.True);
        }

        [UnityTest]
        public IEnumerator ExistingWorldCompletionKeysSurviveLoadAndNeverReplay()
        {
            string[] completedIds =
            {
                TutorialManager.MoveTutorialId,
                TutorialManager.CombatTutorialId,
                TutorialManager.InventoryTutorialId
            };
            if (!_save.World.TutorialsCompleted.Contains(
                    TutorialManager.InventoryTutorialId))
            {
                _save.World.TutorialsCompleted.Add(
                    TutorialManager.InventoryTutorialId);
            }
            Assert.That(_save.SaveGame(0), Is.True, _save.LastError);
            string worldPath = Path.Combine(
                _storageRoot,
                "SaveSlot_0",
                "world.json");
            string worldJson = File.ReadAllText(worldPath);
            for (int index = 0; index < completedIds.Length; index++)
            {
                StringAssert.Contains(completedIds[index], worldJson);
            }

            Object.Destroy(_tutorial.gameObject);
            Object.Destroy(_save.gameObject);
            yield return null;

            _save = new GameObject("[G0602ReloadedSave]")
                .AddComponent<SaveManager>();
            _save.ConfigureStorageRoot(_storageRoot);
            Assert.That(_save.LoadGame(0), Is.True, _save.LastError);
            _tutorial = new GameObject("[G0602ReloadedTutorial]")
                .AddComponent<TutorialManager>();
            yield return null;

            for (int index = 0; index < completedIds.Length; index++)
            {
                Assert.That(_tutorial.HasCompleted(completedIds[index]), Is.True);
                Assert.That(_tutorial.TryStart(completedIds[index]), Is.False);
            }
        }

        private void MarkCoreTutorialsCompleted()
        {
            if (!_save.World.TutorialsCompleted.Contains(
                    TutorialManager.MoveTutorialId))
            {
                _save.World.TutorialsCompleted.Add(TutorialManager.MoveTutorialId);
            }

            if (!_save.World.TutorialsCompleted.Contains(
                    TutorialManager.CombatTutorialId))
            {
                _save.World.TutorialsCompleted.Add(TutorialManager.CombatTutorialId);
            }
        }

        private void AssertActive(string tutorialId)
        {
            Assert.That(_tutorial.IsActive, Is.True, tutorialId);
            Assert.That(_tutorial.ActiveTutorialId, Is.EqualTo(tutorialId));
            Assert.That(_overlay.CurrentLocalizationKey, Is.Not.Empty);
            Assert.That(_overlay.VisibleMaskCount, Is.EqualTo(4));
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int index = 0; index < instances.Length; index++)
            {
                if (instances[index] != null)
                {
                    Object.Destroy(instances[index].gameObject);
                }
            }
        }

        private sealed class FakeCultivationService : ICultivationService
        {
            public RealmType Realm => RealmType.QiCondensation;
            public int SubStage => 9;
            public float CurrentXp => 1000f;
            public float XpToNext => 1000f;
            public BreakthroughState CurrentBreakthroughState =>
                BreakthroughState.Idle;
            public bool IsBreakingThrough => false;
            public bool IsBreakthroughActive => false;
            public bool IsBreakthroughInvincible => false;
            public int CeremonyBeat => -1;
            public bool CanBreakthroughValue { get; set; }

            public void AddXp(float amount, XpSourceType source)
            {
            }

            public float ApplyDeathXpPenalty(float percent)
            {
                return 0f;
            }

            public bool CanLevelSubStage()
            {
                return false;
            }

            public bool TryAdvanceSubStage()
            {
                return false;
            }

            public bool CanBreakthrough()
            {
                return CanBreakthroughValue;
            }

            public IReadOnlyList<BreakthroughBlocker> GetBreakthroughBlockers()
            {
                return Array.Empty<BreakthroughBlocker>();
            }

            public float GetBreakthroughSuccessRate()
            {
                return 1f;
            }

            public bool TryBreakthrough()
            {
                return false;
            }
        }
    }
}
