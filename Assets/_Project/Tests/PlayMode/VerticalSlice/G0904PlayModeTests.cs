using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.CameraSystem;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Entities.Visuals;
using Wendao.Systems.Combat;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Equipment;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;
using Wendao.Systems.Player;
using Wendao.Systems.Skill;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;
using Wendao.UI.Combat;
using Wendao.UI.Common;
using Wendao.UI.Inventory;
using Wendao.UI.SceneFlow;
using Wendao.UI.Skill;
using Wendao.UI.Tutorial;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0904PlayModeTests
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
            SceneUiBootstrap.Install();
            PlayerRuntimeBootstrap.Install();

            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0904Tests_" + Guid.NewGuid().ToString("N"));
            SaveManager.Instance.ConfigureStorageRoot(_storageRoot);
            ServiceLocator.Get<ISpiritRootService>()
                .ChooseRoot(SpiritRootType.Fire);
            Assert.That(SaveManager.Instance.SaveGame(0), Is.True);

            AsyncOperation load = SceneManager.LoadSceneAsync(
                SceneLoader.DefaultMapSceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            EnterPlayingState();
            PlayerRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            TrainingDummyRuntimeBootstrap.EnsureForScene(
                SceneManager.GetActiveScene());
            SceneUiBootstrap.EnsureForScene(SceneManager.GetActiveScene());
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
        public void CombatFlagDrivesCameraFramingAndIncomingDamageStartsCombat()
        {
            GameManager gameManager = GameManager.Instance;
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = player.GetComponent<PlayerStats>();
            Assert.That(gameManager, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(stats, Is.Not.Null);

            GameObject highTarget = new GameObject("[G09-04 Camera Target]");
            highTarget.transform.position = new Vector3(0f, 50f, 0f);
            GameObject cameraObject = new GameObject("[G09-04 Camera]");
            cameraObject.AddComponent<Camera>();
            ThirdPersonCamera camera =
                cameraObject.AddComponent<ThirdPersonCamera>();
            camera.SetTarget(highTarget.transform);

            gameManager.SetCombatFlag(false);
            camera.TickCamera(1f);
            Assert.That(camera.IsCombatMode, Is.False);
            Assert.That(
                camera.CurrentFov,
                Is.EqualTo(ThirdPersonCamera.ExploreFov).Within(0.001f));
            Assert.That(
                camera.CurrentDistance,
                Is.EqualTo(ThirdPersonCamera.ExploreDistance).Within(0.001f));

            gameManager.SetCombatFlag(true);
            camera.TickCamera(1f);
            Assert.That(camera.IsCombatMode, Is.True);
            Assert.That(
                camera.CurrentFov,
                Is.EqualTo(ThirdPersonCamera.CombatFov).Within(0.001f));
            Assert.That(
                camera.CurrentDistance,
                Is.EqualTo(ThirdPersonCamera.CombatDistance).Within(0.001f));

            gameManager.SetCombatFlag(false);
            var enemy = new GameObject("[G09-04 Incoming Enemy]");
            EventBus.Publish(
                CombatEvents.DamageApplied,
                new DamageInfo
                {
                    Source = enemy,
                    Target = player.gameObject,
                    Amount = 1f
                });
            Assert.That(gameManager.IsInCombat, Is.True);

            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(highTarget);
        }

        [UnityTest]
        public IEnumerator InputLockCancelsAttackDodgeAndSkillBeforeResolution()
        {
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            PlayerStats stats = player.GetComponent<PlayerStats>();
            PlayerCombatController combat =
                player.GetComponent<PlayerCombatController>();
            PlayerSkillController skill =
                player.GetComponent<PlayerSkillController>();
            SkillManager skillManager =
                Object.FindAnyObjectByType<SkillManager>();
            TrainingDummy dummy =
                Object.FindAnyObjectByType<TrainingDummy>();
            Assert.That(player, Is.Not.Null);
            Assert.That(combat, Is.Not.Null);
            Assert.That(skill, Is.Not.Null);
            Assert.That(skillManager, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);

            var input = new FakePlayerInputSource();
            ServiceLocator.Unregister<IPlayerInputSource>();
            ServiceLocator.Register<IPlayerInputSource>(input);
            PlayerActionBuffer buffer =
                player.GetComponent<PlayerActionBuffer>();
            player.SetInputSource(input);
            player.SetActionBuffer(buffer);
            combat.SetInputSource(input);
            combat.SetActionBuffer(buffer);
            skill.SetInputSource(input);
            skill.SetActionBuffer(buffer);

            player.TeleportTo(Vector3.zero, Quaternion.identity);
            dummy.ConfigureStats(100f, 0f);
            dummy.transform.SetPositionAndRotation(
                new Vector3(0f, 0.9f, 1.5f),
                Quaternion.identity);
            Physics.SyncTransforms();

            Assert.That(combat.TryStartHeavyAttack(), Is.True);
            input.SetEnabled(false);
            yield return null;
            Assert.That(combat.IsAttacking, Is.False);
            Assert.That(
                player.State,
                Is.Not.EqualTo(PlayerState.HeavyAttack));
            Assert.That(dummy.CurrentHp, Is.EqualTo(100f));

            input.SetEnabled(true);
            player.ForceState(PlayerState.Idle);
            Assert.That(player.TryStartDodge(Vector3.forward), Is.True);
            Vector3 dodgeStart = player.transform.position;
            input.SetEnabled(false);
            yield return null;
            Assert.That(player.State, Is.Not.EqualTo(PlayerState.Dodge));
            Assert.That(
                Vector3.Distance(player.transform.position, dodgeStart),
                Is.LessThan(0.01f));

            input.SetEnabled(true);
            player.ForceState(PlayerState.Idle);
            stats.ConfigureMana(50f);
            Assert.That(
                skillManager.TryCast(
                    0,
                    dummy.transform.position,
                    dummy.gameObject),
                Is.True);
            Assert.That(skillManager.IsCasting, Is.True);
            input.SetEnabled(false);
            yield return null;
            Assert.That(skillManager.IsCasting, Is.False);
            Assert.That(
                player.State,
                Is.Not.EqualTo(PlayerState.SkillCast));
            Assert.That(stats.CurrentMana, Is.EqualTo(50f).Within(0.001f));
            Assert.That(dummy.CurrentHp, Is.EqualTo(100f));
            Assert.That(
                skillManager.GetCooldownRemaining(0),
                Is.Zero.Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PauseFreezesActiveAttackInsteadOfCancellingIt()
        {
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            PlayerCombatController combat =
                player.GetComponent<PlayerCombatController>();
            var input = new FakePlayerInputSource();
            combat.SetInputSource(input);

            player.ForceState(PlayerState.Idle);
            Assert.That(combat.TryStartHeavyAttack(), Is.True);
            float elapsed = combat.AttackElapsed;
            Assert.That(
                GameManager.Instance.TrySetState(GameState.Paused),
                Is.True);
            input.SetEnabled(false);
            yield return null;
            Assert.That(combat.IsAttacking, Is.True);
            Assert.That(combat.AttackElapsed, Is.EqualTo(elapsed).Within(0.001f));

            Assert.That(
                GameManager.Instance.TrySetState(GameState.Playing),
                Is.True);
            input.SetEnabled(true);
            yield return null;
            Assert.That(combat.IsAttacking, Is.True);
        }

        [Test]
        public void LockOnMarkerTracksRealAnchorAndClearsWhenTargetDies()
        {
            LockOnMarkerView marker =
                Object.FindAnyObjectByType<LockOnMarkerView>();
            TrainingDummy dummy =
                Object.FindAnyObjectByType<TrainingDummy>();
            PlayerController player =
                Object.FindAnyObjectByType<PlayerController>();
            Assert.That(marker, Is.Not.Null);
            Assert.That(dummy, Is.Not.Null);

            dummy.ConfigureStats(10f, 0f);
            EventBus.Publish(
                PlayerEvents.LockOnChanged,
                new LockOnInfo
                {
                    Player = player.gameObject,
                    Target = dummy.gameObject,
                    Locked = true
                });
            Assert.That(marker.Target, Is.EqualTo(dummy.gameObject));
            Assert.That(marker.TargetTransform, Is.EqualTo(dummy.transform));
            Assert.That(marker.IsVisible, Is.True);

            dummy.ApplyDamage(
                new DamageInfo
                {
                    Amount = 10f
                });
            marker.RefreshNow();
            Assert.That(marker.IsVisible, Is.False);
            Assert.That(marker.Target, Is.Null);
        }

        [Test]
        public void TransparentAndDestroyedOccludersNeverRemainHidden()
        {
            GameObject target = new GameObject("[G09-04 Occlusion Target]");
            target.transform.position = new Vector3(0f, 30f, 0f);
            GameObject cameraObject = new GameObject("[G09-04 Occlusion Camera]");
            cameraObject.AddComponent<Camera>();
            ThirdPersonCamera camera =
                cameraObject.AddComponent<ThirdPersonCamera>();
            camera.SetTarget(target.transform);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 32.3f, -2.5f);
            wall.transform.localScale = new Vector3(4f, 4f, 0.5f);
            Renderer renderer = wall.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            var transparent = new Material(shader)
            {
                renderQueue =
                    (int)UnityEngine.Rendering.RenderQueue.Transparent
            };
            renderer.sharedMaterial = transparent;

            Physics.SyncTransforms();
            camera.TickCamera(1f);
            Assert.That(camera.FadedOccluderCount, Is.EqualTo(1));
            Assert.That(renderer.forceRenderingOff, Is.False);

            wall.transform.position = new Vector3(100f, 100f, 100f);
            Physics.SyncTransforms();
            camera.TickCamera(1f);
            Assert.That(camera.FadedOccluderCount, Is.Zero);
            Assert.That(renderer.forceRenderingOff, Is.False);

            wall.transform.position = new Vector3(0f, 32.3f, -2.5f);
            Physics.SyncTransforms();
            camera.TickCamera(1f);
            Assert.That(camera.FadedOccluderCount, Is.EqualTo(1));
            Object.DestroyImmediate(wall);
            camera.TickCamera(1f);
            Assert.That(camera.FadedOccluderCount, Is.Zero);

            Object.DestroyImmediate(transparent);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }

        private static void EnterPlayingState()
        {
            GameManager gameManager = GameManager.Instance;
            Assert.That(gameManager, Is.Not.Null);
            if (gameManager.State == GameState.Boot)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.MainMenu),
                    Is.True);
            }

            if (gameManager.State == GameState.MainMenu)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.Loading),
                    Is.True);
            }

            if (gameManager.State == GameState.Loading)
            {
                Assert.That(
                    gameManager.TrySetState(GameState.Playing),
                    Is.True);
            }
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<LockOnMarkerView>();
            DestroyAll<Wendao.Systems.Loot.WorldItemPickup>();
            DestroyAll<EnemyBrain>();
            DestroyAll<EnemySpawner>();
            DestroyAll<PlayerController>();
            DestroyAll<Wendao.Entities.NPC.NPCController>();
            DestroyAll<TrainingDummy>();
            DestroyAll<ThirdPersonCamera>();
            DestroyAll<SkillQuickbarView>();
            DestroyAll<Wendao.UI.Cultivation.SpiritRootSelectionView>();
            DestroyAll<Wendao.UI.Cultivation.CultivationHudView>();
            DestroyAll<InventoryPanelView>();
            DestroyAll<UIManager>();
            DestroyAll<GameToastView>();
            DestroyAll<Wendao.UI.NPC.DialogueView>();
            DestroyAll<Wendao.UI.Quest.QuestTrackerView>();
            DestroyAll<Wendao.UI.Quest.QuestWorldMarkerView>();
            DestroyAll<DamageFloatingTextView>();
            DestroyAll<TutorialToastView>();
            DestroyAll<TutorialManager>();
            DestroyAll<Wendao.Systems.Loot.LootSystem>();
            DestroyAll<SkillProjectile>();
            DestroyAll<SkillManager>();
            DestroyAll<Wendao.Systems.NPC.DialogueManager>();
            DestroyAll<Wendao.Systems.Quest.QuestManager>();
            DestroyAll<CultivationManager>();
            DestroyAll<SpiritRootSystem>();
            DestroyAll<EquipmentManager>();
            DestroyAll<ItemUseSystem>();
            DestroyAll<InventoryManager>();
            DestroyAll<Wendao.Systems.Crafting.AlchemySystem>();
            DestroyAll<Wendao.Systems.Shop.ShopSystem>();
            DestroyAll<Wendao.Systems.Crafting.GatheringSystem>();
            DestroyAll<Wendao.UI.Crafting.AlchemyPanelView>();
            DestroyAll<Wendao.UI.Shop.ShopPanelView>();
            DestroyAll<StatusEffectManager>();
            DestroyAll<CombatFeelController>();
            DestroyAll<CombatSystem>();
            DestroyAll<SceneLoader>();
            DestroyAll<GameManager>();
            DestroyAll<SaveManager>();
            DestroyAll<ConfigDatabase>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < instances.Length; index++)
            {
                if (instances[index] != null)
                {
                    Object.Destroy(instances[index].gameObject);
                }
            }
        }

        private sealed class FakePlayerInputSource : IPlayerInputSource
        {
            public Vector2 Move { get; set; }
            public Vector2 Look { get; set; }
            public bool LookIsPointerDelta { get; set; }
            public bool JumpPressedThisFrame { get; set; }
            public bool JumpHeld { get; set; }
            public bool SprintHeld { get; set; }
            public bool LightAttackPressedThisFrame { get; set; }
            public bool HeavyAttackPressedThisFrame { get; set; }
            public bool DodgePressedThisFrame { get; set; }
            public bool BlockHeld { get; set; }
            public bool LockOnPressedThisFrame { get; set; }
            public bool Skill1PressedThisFrame { get; set; }
            public bool Skill2PressedThisFrame { get; set; }
            public bool Skill3PressedThisFrame { get; set; }
            public bool Skill4PressedThisFrame { get; set; }
            public bool InteractPressedThisFrame { get; set; }
            public bool OpenInventoryPressedThisFrame { get; set; }
            public bool OpenCharacterPressedThisFrame { get; set; }
            public bool OpenSkillPressedThisFrame { get; set; }
            public bool OpenQuestPressedThisFrame { get; set; }
            public bool OpenMapPressedThisFrame { get; set; }
            public bool PausePressedThisFrame { get; set; }
            public bool MountPressedThisFrame { get; set; }
            public bool IsEnabled { get; private set; } = true;

            public void SetEnabled(bool enabled)
            {
                IsEnabled = enabled;
            }
        }
    }
}
