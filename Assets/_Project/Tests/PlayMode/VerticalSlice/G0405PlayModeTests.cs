using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0405PlayModeTests
    {
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
            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            Scene scene = SceneManager.GetActiveScene();
            PlayerRuntimeBootstrap.EnsureForScene(scene);
            StoneGeneralRuntimeBootstrap.EnsureForScene(scene);
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
        }

        [Test]
        public void EveryBossPhaseHasReadableTelegraphForEverySkill()
        {
            EnemyData boss = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.StoneGeneral);
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.BossPhases, Has.Length.EqualTo(3));

            foreach (BossPhase phase in boss.BossPhases)
            {
                Assert.That(phase.Telegraphs, Is.Not.Null);
                Assert.That(
                    phase.Telegraphs.Length,
                    Is.EqualTo(phase.SkillIds.Length));
                var visibleVfxCount = 0;
                foreach (string skillId in phase.SkillIds)
                {
                    BossSkillTelegraph telegraph = Array.Find(
                        phase.Telegraphs,
                        candidate => candidate != null
                            && candidate.SkillId == skillId);
                    Assert.That(telegraph, Is.Not.Null, skillId);
                    Assert.That(telegraph.Duration,
                        Is.GreaterThanOrEqualTo(0.6f), skillId);
                    Assert.That(telegraph.RecoverStun,
                        Is.GreaterThan(0f), skillId);
                    Assert.That(telegraph.Interruptible, Is.False, skillId);
                    if (!string.IsNullOrEmpty(telegraph.VfxId))
                    {
                        visibleVfxCount++;
                    }
                }

                Assert.That(visibleVfxCount, Is.GreaterThanOrEqualTo(1));
            }
        }

        [Test]
        public void TelegraphViewPersistsUntilImpactThenUsesConfiguredRecovery()
        {
            EnemyBrain boss = FindBossAndDisableOthers();
            EnterArena(boss);
            BeginFirstBossSkill(boss);
            BossSkillTelegraph telegraph = boss.ActiveBossTelegraph;

            Assert.That(telegraph, Is.Not.Null);
            Assert.That(boss.IsBossTelegraphVisible, Is.True);
            Assert.That(boss.BossTelegraphView, Is.Not.Null);
            Assert.That(boss.BossTelegraphView.SkillId,
                Is.EqualTo(boss.ActiveSkillId));
            Assert.That(boss.BossTelegraphView.VfxId,
                Is.EqualTo(telegraph.VfxId));

            boss.TickAI(telegraph.Duration * 0.5f);
            Assert.That(boss.IsBossTelegraphVisible, Is.True);
            Assert.That(boss.BossTelegraphView.Progress01,
                Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(boss.BossSkillUseCount, Is.EqualTo(0));

            boss.TickAI(telegraph.Duration * 0.5f + 0.01f);
            Assert.That(boss.IsBossTelegraphVisible, Is.False);
            Assert.That(boss.BossSkillUseCount, Is.EqualTo(1));
            Assert.That(boss.IsInBossRecovery, Is.True);
            Assert.That(boss.BossRecoveryRemaining,
                Is.EqualTo(telegraph.RecoverStun).Within(0.02f));

            boss.TickAI(telegraph.RecoverStun * 0.5f);
            Assert.That(boss.State, Is.EqualTo(EnemyBrainState.Skill));
            Assert.That(boss.IsInBossRecovery, Is.True);
            boss.TickAI(telegraph.RecoverStun * 0.5f);
            Assert.That(boss.IsInBossRecovery, Is.False);
            Assert.That(boss.State,
                Is.EqualTo(EnemyBrainState.Attack)
                    .Or.EqualTo(EnemyBrainState.Chase));
        }

        [Test]
        public void BossHitCarriesSkillIdAndRecoveryWindowIsPunishable()
        {
            EnemyBrain boss = FindBossAndDisableOthers();
            PlayerController player = EnterArena(boss);
            PlayerStats stats = player.GetComponent<PlayerStats>();
            stats.ConfigureBaseStats(10000f, 100f, 0f);
            BeginFirstBossSkill(boss);
            BossSkillTelegraph telegraph = boss.ActiveBossTelegraph;

            DamageInfo bossHit = default;
            Action<DamageInfo> handler = info =>
            {
                if (info.Source == boss.gameObject)
                {
                    bossHit = info;
                }
            };
            EventBus.Subscribe(CombatEvents.DamageApplied, handler);
            try
            {
                boss.TickAI(telegraph.Duration);
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.DamageApplied, handler);
            }

            Assert.That(bossHit.Source, Is.EqualTo(boss.gameObject));
            Assert.That(bossHit.Target, Is.EqualTo(player.gameObject));
            Assert.That(bossHit.SkillId,
                Is.EqualTo(EnemyContentIds.StoneGeneralSlam));
            Assert.That(boss.IsInBossRecovery, Is.True);

            float hpBeforePunish = boss.CurrentHp;
            ServiceLocator.Get<ICombatService>().DealDamage(
                boss,
                new DamageRequest
                {
                    Source = player.gameObject,
                    BaseDamage = 50f,
                    Type = DamageType.True,
                    Multiplier = 1f,
                    CanCrit = false,
                    SkillId = "skill_test_recovery_punish"
                });
            Assert.That(boss.CurrentHp, Is.LessThan(hpBeforePunish));
            Assert.That(boss.IsInBossRecovery, Is.True);
        }

        [Test]
        public void LeavingArenaCancelsActiveTelegraph()
        {
            EnemyBrain boss = FindBossAndDisableOthers();
            PlayerController player = EnterArena(boss);
            BeginFirstBossSkill(boss);
            BossArenaController arena =
                Object.FindAnyObjectByType<BossArenaController>();
            Assert.That(boss.IsBossTelegraphVisible, Is.True);

            player.TeleportTo(
                arena.ArenaCenter
                    + Vector3.forward * (arena.ArenaRadius + 3f),
                Quaternion.identity);
            arena.TickArena();

            Assert.That(boss.IsBossTelegraphVisible, Is.False);
            Assert.That(boss.ActiveBossTelegraph, Is.Null);
            Assert.That(boss.ActiveSkillId, Is.Empty);
            Assert.That(boss.State, Is.EqualTo(EnemyBrainState.Idle));
        }

        private static void BeginFirstBossSkill(EnemyBrain boss)
        {
            boss.TickAI(EnemyBrain.AlertDurationSeconds);
            boss.TickAI(0.01f);
            Assert.That(boss.State, Is.EqualTo(EnemyBrainState.Skill));
            Assert.That(boss.ActiveSkillId,
                Is.EqualTo(EnemyContentIds.StoneGeneralSlam));
        }

        private static PlayerController EnterArena(EnemyBrain boss)
        {
            BossArenaController arena =
                Object.FindAnyObjectByType<BossArenaController>();
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            player.TeleportTo(
                arena.ArenaCenter + Vector3.forward * 2f,
                Quaternion.identity);
            PlayerStats stats = player.GetComponent<PlayerStats>();
            stats.ConfigureBaseStats(10000f, 100f, 0f);
            stats.SetHp(stats.MaxHp);
            arena.TickArena();
            Assert.That(boss.Target, Is.EqualTo(player.gameObject));
            return player;
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

        private static EnemyBrain FindBossAndDisableOthers()
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>();
            EnemyBrain boss = Array.Find(
                enemies,
                enemy => enemy != null
                    && enemy.Data != null
                    && enemy.Data.Id == EnemyContentIds.StoneGeneral);
            Assert.That(boss, Is.Not.Null);
            foreach (EnemyBrain enemy in enemies)
            {
                if (enemy != boss)
                {
                    enemy.enabled = false;
                }
            }

            return boss;
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<Wendao.UI.Combat.DeathView>();
            DestroyAll<BossArenaController>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<TrainingDummy>();
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
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include);
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
