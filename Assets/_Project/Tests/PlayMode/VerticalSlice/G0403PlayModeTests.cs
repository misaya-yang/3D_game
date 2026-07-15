using System;
using System.Collections;
using System.Collections.Generic;
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
using Wendao.UI.Combat;
using Wendao.UI.SceneFlow;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0403PlayModeTests
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
            SceneUiBootstrap.Install();
            PlayerRuntimeBootstrap.Install();
            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            Scene scene = SceneManager.GetActiveScene();
            PlayerRuntimeBootstrap.EnsureForScene(scene);
            SceneUiBootstrap.EnsureForScene(scene);
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
        public void StoneGeneralContentArenaAndBossBarMatchSpec()
        {
            EnemyData data = ConfigDatabase.Instance.GetEnemy(
                EnemyContentIds.StoneGeneral);
            EnemyBrain boss = FindBoss();
            BossArenaController arena =
                Object.FindAnyObjectByType<BossArenaController>();
            BossHealthBarView view =
                Object.FindAnyObjectByType<BossHealthBarView>();

            Assert.That(data, Is.Not.Null);
            Assert.That(data.DisplayName, Is.EqualTo("黑风石将军"));
            Assert.That(data.Rank, Is.EqualTo(EnemyRank.Boss));
            Assert.That(data.MaxHp, Is.EqualTo(12000f));
            Assert.That(data.Attack, Is.EqualTo(70f));
            Assert.That(data.CultivationXpReward, Is.EqualTo(2000f));
            Assert.That(data.BossPhases, Has.Length.EqualTo(3));
            Assert.That(data.BossPhases[0].HpThreshold, Is.EqualTo(1f));
            Assert.That(data.BossPhases[1].HpThreshold, Is.EqualTo(0.7f));
            Assert.That(data.BossPhases[2].HpThreshold, Is.EqualTo(0.3f));

            Assert.That(boss, Is.Not.Null);
            Assert.That(boss.IsOnNavMesh, Is.True);
            Assert.That(boss.CurrentHp, Is.EqualTo(12000f));
            Assert.That(
                boss.transform.Find("StoneGeneralVisual_Greybox"),
                Is.Not.Null);
            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.Boss, Is.SameAs(boss));
            Assert.That(arena.ArenaRadius, Is.EqualTo(6.5f));
            Assert.That(view, Is.Not.Null);
            Assert.That(view.IsVisible, Is.False);

            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            player.TeleportTo(
                arena.ArenaCenter + Vector3.forward * 4f,
                Quaternion.identity);
            arena.TickArena();
            view.RefreshNow();
            Assert.That(arena.IsPlayerInside, Is.True);
            Assert.That(boss.State, Is.EqualTo(EnemyBrainState.Alert));
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.DisplayedHp01, Is.EqualTo(1f));
            Assert.That(view.DisplayedPhase, Is.EqualTo(1));
            Assert.That(
                view.CurrentNameLocalizationKey,
                Is.EqualTo("enemy_name_enemy_boss_stone_general"));
        }

        [Test]
        public void HpThresholdsPublishPhaseEventsAndGrantTransitionInvincibility()
        {
            EnemyBrain boss = FindBossAndDisableOthers();
            PlayerController player = EnterArena(boss);
            var phases = new List<BossPhaseInfo>();
            Action<BossPhaseInfo> handler = info => phases.Add(info);
            EventBus.Subscribe(CombatEvents.BossPhaseChanged, handler);
            try
            {
                boss.ApplyDamage(
                    new DamageInfo
                    {
                        Source = player.gameObject,
                        Amount = boss.MaxHp * 0.31f,
                        Type = DamageType.True
                    });
                Assert.That(boss.CurrentBossPhase, Is.EqualTo(1));
                Assert.That(boss.IsBossTransitioning, Is.True);
                Assert.That(boss.IsInvincible, Is.True);
                Assert.That(
                    boss.BossTransitionRemaining,
                    Is.EqualTo(EnemyBrain.BossPhaseTransitionSeconds));
                float hpDuringTransition = boss.CurrentHp;
                boss.ApplyDamage(
                    new DamageInfo
                    {
                        Source = player.gameObject,
                        Amount = 1000f,
                        Type = DamageType.True
                    });
                Assert.That(boss.CurrentHp, Is.EqualTo(hpDuringTransition));

                boss.TickAI(EnemyBrain.BossPhaseTransitionSeconds);
                Assert.That(boss.IsBossTransitioning, Is.False);
                Assert.That(
                    boss.CurrentBossSkillIds,
                    Is.EqualTo(new[]
                    {
                        EnemyContentIds.StoneGeneralCharge,
                        EnemyContentIds.StoneGeneralSummon
                    }));

                boss.ApplyDamage(
                    new DamageInfo
                    {
                        Source = player.gameObject,
                        Amount = boss.CurrentHp - boss.MaxHp * 0.29f,
                        Type = DamageType.True
                    });
                Assert.That(boss.CurrentBossPhase, Is.EqualTo(2));
                boss.TickAI(EnemyBrain.BossPhaseTransitionSeconds);
                Assert.That(
                    boss.EffectiveAttackInterval,
                    Is.EqualTo(
                        boss.Data.AttackInterval
                        / EnemyBrain.BossRageAttackSpeedMultiplier)
                        .Within(0.001f));
            }
            finally
            {
                EventBus.Unsubscribe(CombatEvents.BossPhaseChanged, handler);
            }

            Assert.That(phases, Has.Count.EqualTo(2));
            Assert.That(phases[0].OldPhase, Is.EqualTo(0));
            Assert.That(phases[0].NewPhase, Is.EqualTo(1));
            Assert.That(phases[1].OldPhase, Is.EqualTo(1));
            Assert.That(phases[1].NewPhase, Is.EqualTo(2));
        }

        [Test]
        public void EachBossPhaseSelectsItsOwnSkillSet()
        {
            EnemyBrain boss = FindBossAndDisableOthers();
            PlayerController player = EnterArena(boss);
            PlayerStats stats = player.GetComponent<PlayerStats>();
            stats.ConfigureBaseStats(10000f, 10f, 0f);

            boss.TickAI(EnemyBrain.AlertDurationSeconds);
            boss.TickAI(0.01f);
            Assert.That(boss.State, Is.EqualTo(EnemyBrainState.Skill));
            Assert.That(
                boss.ActiveSkillId,
                Is.EqualTo(EnemyContentIds.StoneGeneralSlam));

            boss.ApplyDamage(
                new DamageInfo
                {
                    Source = player.gameObject,
                    Amount = boss.MaxHp * 0.31f,
                    Type = DamageType.True
                });
            boss.TickAI(EnemyBrain.BossPhaseTransitionSeconds);
            boss.TickAI(0.01f);
            Assert.That(
                boss.ActiveSkillId,
                Is.EqualTo(EnemyContentIds.StoneGeneralCharge));

            boss.ApplyDamage(
                new DamageInfo
                {
                    Source = player.gameObject,
                    Amount = boss.CurrentHp - boss.MaxHp * 0.29f,
                    Type = DamageType.True
                });
            boss.TickAI(EnemyBrain.BossPhaseTransitionSeconds);
            boss.TickAI(0.01f);
            Assert.That(
                boss.ActiveSkillId,
                Is.EqualTo(EnemyContentIds.StoneGeneralRageSlam));
        }

        [Test]
        public void LeavingArenaResetsBossHealthPhaseTargetAndBossBar()
        {
            EnemyBrain boss = FindBossAndDisableOthers();
            PlayerController player = EnterArena(boss);
            BossArenaController arena =
                Object.FindAnyObjectByType<BossArenaController>();
            BossHealthBarView view =
                Object.FindAnyObjectByType<BossHealthBarView>();

            boss.ApplyDamage(
                new DamageInfo
                {
                    Source = player.gameObject,
                    Amount = boss.MaxHp * 0.31f,
                    Type = DamageType.True
                });
            Assert.That(boss.CurrentBossPhase, Is.EqualTo(1));
            view.RefreshNow();
            Assert.That(view.IsVisible, Is.True);

            player.TeleportTo(
                arena.ArenaCenter
                    + Vector3.forward * (arena.ArenaRadius + 3f),
                Quaternion.identity);
            arena.TickArena();
            view.RefreshNow();

            Assert.That(arena.IsPlayerInside, Is.False);
            Assert.That(boss.State, Is.EqualTo(EnemyBrainState.Idle));
            Assert.That(boss.Target, Is.Null);
            Assert.That(boss.CurrentHp, Is.EqualTo(boss.MaxHp));
            Assert.That(boss.CurrentBossPhase, Is.EqualTo(0));
            Assert.That(boss.IsBossTransitioning, Is.False);
            Assert.That(
                HorizontalDistance(boss.transform.position, boss.SpawnPosition),
                Is.LessThan(0.01f));
            Assert.That(view.IsVisible, Is.False);
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
            player.GetComponent<PlayerStats>().SetHp(
                player.GetComponent<PlayerStats>().MaxHp);
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

        private static EnemyBrain FindBoss()
        {
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>();
            for (int index = 0; index < enemies.Length; index++)
            {
                if (enemies[index].Data != null
                    && enemies[index].Data.Id == EnemyContentIds.StoneGeneral)
                {
                    return enemies[index];
                }
            }

            return null;
        }

        private static EnemyBrain FindBossAndDisableOthers()
        {
            EnemyBrain boss = FindBoss();
            Assert.That(boss, Is.Not.Null);
            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>();
            for (int index = 0; index < enemies.Length; index++)
            {
                if (enemies[index] != boss)
                {
                    enemies[index].enabled = false;
                }
            }

            return boss;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<BossHealthBarView>();
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
