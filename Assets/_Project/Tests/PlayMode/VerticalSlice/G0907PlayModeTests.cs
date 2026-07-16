using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Entities.Visuals;
using Wendao.Systems.Diagnostics;
using Wendao.Systems.Enemy;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0907PlayModeTests
    {
        [Test]
        public void CuratedCharacterFilesContainRequiredMotionClips()
        {
            AssertClipSet(
                BudgetArtCatalog.Player,
                "Idle",
                "Run",
                "Attack",
                "Roll",
                "RecieveHit",
                "Death");
            AssertClipSet(
                BudgetArtCatalog.HumanEnemy,
                "Idle",
                "Run",
                "Dagger_Attack",
                "Roll",
                "RecieveHit",
                "Death");
            AssertClipSet(
                BudgetArtCatalog.NpcGuard,
                "Idle",
                "Run",
                "RecieveHit",
                "Death");
            AssertClipSet(
                BudgetArtCatalog.NpcHealer,
                "Idle",
                "Run",
                "Staff_Attack",
                "Death");
            AssertClipSet(
                BudgetArtCatalog.NpcHermit,
                "Idle",
                "Run",
                "Spell1",
                "Death");
            AssertClipSet(
                BudgetArtCatalog.Wolf,
                "Idle",
                "Gallop",
                "Attack",
                "Idle_HitReact_Left",
                "Death");
            AssertClipSet(
                BudgetArtCatalog.StoneGeneral,
                "Idle",
                "Run",
                "Punch",
                "RecieveHit",
                "Death");
            AssertModelParts(
                BudgetArtCatalog.StoneGeneral,
                "StoneGeneral_Body",
                "StoneGeneral_Maul",
                "StoneGeneral_Core",
                "StoneGeneral_Crown");
        }

        [Test]
        public void ModularCharacterResourcesContainAuthoredParts()
        {
            AssertModelParts(
                BudgetArtCatalog.NpcGuard,
                "Cultivator_Head",
                "Cultivator_Hair",
                "Cultivator_Body",
                "Modular_Guard_Bow");
            AssertModelParts(
                BudgetArtCatalog.NpcHealer,
                "Cultivator_Head",
                "Cultivator_Hair",
                "Cultivator_Body",
                "Modular_Healer_Staff");
            AssertModelParts(
                BudgetArtCatalog.NpcHermit,
                "Cultivator_Head",
                "Cultivator_Hair",
                "Cultivator_Body",
                "Modular_Hermit_Staff");
            AssertModelParts(
                BudgetArtCatalog.HumanEnemy,
                "Cultivator_Head",
                "Cultivator_Hair",
                "Cultivator_Hood",
                "Modular_Bandit_Dagger");
        }

        [Test]
        public void StoneGeneralIdleClipBindsToImportedRig()
        {
            GameObject prefab = Resources.Load<GameObject>(
                BudgetArtCatalog.StoneGeneral);
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                AnimationClip idle = Resources
                    .LoadAll<AnimationClip>(
                        BudgetArtCatalog.StoneGeneral)
                    .First(clip => clip.name.EndsWith(
                        "Idle",
                        System.StringComparison.OrdinalIgnoreCase));
                Transform lowerArm = FindTransform(
                    instance,
                    "LowerArm.L");
                Assert.That(lowerArm, Is.Not.Null);
                Quaternion initial = lowerArm.localRotation;
                idle.SampleAnimation(instance, idle.length * 0.35f);
                Assert.That(
                    Quaternion.Angle(
                        initial,
                        lowerArm.localRotation),
                    Is.GreaterThan(0.5f),
                    "Imported boss Idle clip did not bind to its rig.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [UnityTest]
        public IEnumerator PlayerUsesAnimatedCultivatorArtDirectionLayer()
        {
            var player = new GameObject("G09-07 Player Probe");
            CapsuleCollider gameplayCollider =
                player.AddComponent<CapsuleCollider>();

            Assert.That(BudgetVisualFactory.AttachPlayer(player), Is.True);
            Transform upperArm = FindTransform(player, "UpperArm.L");
            Transform armature = FindTransform(
                player,
                "CultivatorArmature");
            Assert.That(upperArm, Is.Not.Null);
            Assert.That(armature, Is.Not.Null);
            Transform[] animatedTransforms =
                armature.GetComponentsInChildren<Transform>(true);
            Quaternion[] initialRotations = animatedTransforms
                .Select(transform => transform.localRotation)
                .ToArray();
            Vector3[] initialPositions = animatedTransforms
                .Select(transform => transform.localPosition)
                .ToArray();
            yield return new WaitForSeconds(0.2f);

            RuntimeCharacterAnimator runtimeAnimator =
                player.GetComponentInChildren<RuntimeCharacterAnimator>(true);
            CultivatorPlayerStyle style =
                player.GetComponentInChildren<CultivatorPlayerStyle>(true);
            Assert.That(runtimeAnimator, Is.Not.Null);
            Assert.That(runtimeAnimator.IsGraphValid, Is.True);
            Assert.That(runtimeAnimator.ImportedClipCount, Is.GreaterThan(5));
            StringAssert.Contains("Idle", runtimeAnimator.CurrentClipName);
            Assert.That(
                Mathf.Max(
                    MaximumRotationDelta(
                        animatedTransforms,
                        initialRotations),
                    MaximumPositionDelta(
                        animatedTransforms,
                        initialPositions) * 100f),
                Is.GreaterThan(1f),
                "Idle clip must visibly leave the imported bind pose.");
            Assert.That(style, Is.Not.Null);
            Assert.That(style.IsConfigured, Is.True);
            Assert.That(style.WeaponBone, Is.Not.Null);
            Assert.That(
                FindTransform(player, "Model_Cultivator"),
                Is.Not.Null);
            Assert.That(
                player.GetComponentsInChildren<Transform>(true).Count(
                    child => child.name.StartsWith(
                        "Cultivator_Hair",
                        System.StringComparison.Ordinal)),
                Is.GreaterThanOrEqualTo(1),
                "The curated hair may be one authored mesh; placeholder "
                + "piece count is not a quality invariant.");
            Assert.That(
                FindTransform(player, "Monk.001"),
                Is.Null,
                "White beard accessory must not ship in the protagonist.");
            Assert.That(
                FindRenderer(player, "Cultivator_Jian"),
                Is.Not.Null);
            AssertRendererUsesTexture(
                player,
                "Cultivator_Body",
                "Robe");
            AssertRendererUsesTexture(
                player,
                "Cultivator_Head",
                "Skin");
            AssertRendererUsesTexture(
                player,
                "Cultivator_Hair",
                "Hair");
            Assert.That(gameplayCollider, Is.Not.Null);
            Assert.That(
                player.transform.Find(BudgetVisualFactory.VisualRootName)
                    .GetComponentsInChildren<Collider>(true),
                Is.Empty);

            Object.Destroy(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NpcHumanEnemyWolfAndBossUseLiveIdleMotion()
        {
            var healer = new GameObject("YaoLao_G09-07");
            var humanEnemy = new GameObject("Bandit_G09-07");
            var wolf = new GameObject("Wolf_G09-07");
            var boss = new GameObject("StoneGeneral_G09-07");
            EnemyData banditData = ScriptableObject.CreateInstance<EnemyData>();
            banditData.Id = EnemyContentIds.Bandit;
            EnemyData wolfData = ScriptableObject.CreateInstance<EnemyData>();
            wolfData.Id = EnemyContentIds.GreyWolf;
            EnemyData bossData = ScriptableObject.CreateInstance<EnemyData>();
            bossData.Id = EnemyContentIds.StoneGeneral;

            Assert.That(
                BudgetVisualFactory.AttachNpc(healer, healer.name),
                Is.True);
            Assert.That(
                BudgetVisualFactory.AttachEnemy(humanEnemy, banditData),
                Is.True);
            Assert.That(
                BudgetVisualFactory.AttachEnemy(wolf, wolfData),
                Is.True);
            Assert.That(
                BudgetVisualFactory.AttachEnemy(boss, bossData),
                Is.True);

            AssertConfiguredModularStyle(
                healer,
                BudgetArtCatalog.NpcHealer);
            AssertConfiguredModularStyle(
                humanEnemy,
                BudgetArtCatalog.HumanEnemy);
            StoneGeneralStyle bossStyle =
                boss.GetComponentInChildren<StoneGeneralStyle>(true);
            Assert.That(bossStyle, Is.Not.Null);
            Assert.That(bossStyle.IsConfigured, Is.True);
            Assert.That(
                FindRenderer(boss, "StoneGeneral_Core"),
                Is.Not.Null);
            AssertRendererUsesTexture(
                healer,
                "Cultivator_Body",
                "Robe");
            AssertRendererUsesTexture(
                healer,
                "Cultivator_Hair",
                "Hair");
            AssertRendererUsesTexture(
                humanEnemy,
                "Cultivator_Body",
                "Ranger");
            AssertRendererUsesTexture(
                humanEnemy,
                "Cultivator_Head",
                "Skin");

            GameObject[] roots = { healer, humanEnemy, wolf, boss };
            Transform[][] transforms = roots
                .Select(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();
            Quaternion[][] rotations = transforms
                .Select(group => group
                    .Select(transform => transform.localRotation)
                    .ToArray())
                .ToArray();
            Vector3[][] positions = transforms
                .Select(group => group
                    .Select(transform => transform.localPosition)
                    .ToArray())
                .ToArray();

            yield return new WaitForSeconds(0.2f);

            for (int index = 0; index < roots.Length; index++)
            {
                RuntimeCharacterAnimator animator =
                    roots[index].GetComponentInChildren<
                        RuntimeCharacterAnimator>(true);
                Assert.That(animator, Is.Not.Null, roots[index].name);
                Assert.That(animator.IsGraphValid, Is.True, roots[index].name);
                Assert.That(
                    Mathf.Max(
                        MaximumRotationDelta(
                            transforms[index],
                            rotations[index]),
                        MaximumPositionDelta(
                            transforms[index],
                            positions[index]) * 100f),
                    Is.GreaterThan(0.5f),
                    roots[index].name + " remained in bind pose.");
                Bounds bounds = CalculateEnabledBounds(roots[index]);
                Assert.That(
                    bounds.size.magnitude,
                    Is.GreaterThan(0.3f),
                    roots[index].name + " has no visible model bounds.");
                Assert.That(
                    Vector3.Distance(
                        bounds.center,
                        roots[index].transform.position),
                    Is.LessThan(5f),
                    roots[index].name + " animation displaced its model root.");
            }

            foreach (GameObject root in roots)
            {
                Object.Destroy(root);
            }
            Object.Destroy(banditData);
            Object.Destroy(wolfData);
            Object.Destroy(bossData);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerActionStatesSelectAttackRollAndDeathClips()
        {
            var player = new GameObject("G09-07 Player State Probe");
            player.AddComponent<CharacterController>();
            PlayerController controller =
                player.AddComponent<PlayerController>();
            player.AddComponent<PlayerStats>();
            player.AddComponent<PlayerCombatController>();
            Assert.That(BudgetVisualFactory.AttachPlayer(player), Is.True);
            RuntimeCharacterAnimator animator =
                player.GetComponentInChildren<RuntimeCharacterAnimator>(true);
            Assert.That(animator, Is.Not.Null);
            yield return null;

            controller.ForceState(PlayerState.LightAttack);
            animator.SendMessage(
                "Update",
                SendMessageOptions.DontRequireReceiver);
            StringAssert.Contains("Attack", animator.CurrentClipName);

            controller.ForceState(PlayerState.Dodge);
            animator.SendMessage(
                "Update",
                SendMessageOptions.DontRequireReceiver);
            StringAssert.Contains("Roll", animator.CurrentClipName);

            controller.ForceState(PlayerState.Dead);
            animator.SendMessage(
                "Update",
                SendMessageOptions.DontRequireReceiver);
            StringAssert.Contains("Death", animator.CurrentClipName);

            Object.Destroy(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WolfDeathSelectsAndHoldsDeathMotion()
        {
            var wolf = new GameObject("G09-07 Wolf Death Probe");
            wolf.AddComponent<CharacterController>();
            wolf.AddComponent<NavMeshAgent>();
            EnemyBrain brain = wolf.AddComponent<EnemyBrain>();
            EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
            data.Id = EnemyContentIds.GreyWolf;
            data.MaxHp = 10f;
            data.Rank = EnemyRank.Normal;
            brain.SpawnInit(data, Vector3.zero);
            Assert.That(BudgetVisualFactory.AttachEnemy(wolf, data), Is.True);
            RuntimeCharacterAnimator animator =
                wolf.GetComponentInChildren<RuntimeCharacterAnimator>(true);
            Assert.That(animator, Is.Not.Null);
            yield return null;

            brain.ApplyDamage(
                new DamageInfo
                {
                    Target = wolf,
                    Amount = 20f,
                    Type = DamageType.True
                });
            yield return new WaitForSeconds(0.2f);

            Assert.That(brain.IsDead, Is.True);
            StringAssert.Contains("Death", animator.CurrentClipName);

            Object.Destroy(wolf);
            Object.Destroy(data);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwentyCharacterCombatLineupMeetsPerformanceBudget()
        {
            int medium = System.Array.IndexOf(
                QualitySettings.names,
                MvpRuntimeDiagnostics.TargetQualityName);
            Assert.That(medium, Is.GreaterThanOrEqualTo(0));
            QualitySettings.SetQualityLevel(medium, true);
            QualitySettings.vSyncCount = 0;
            Screen.SetResolution(
                MvpRuntimeDiagnostics.TargetWidth,
                MvpRuntimeDiagnostics.TargetHeight,
                false);

            var lineup = new GameObject("G09-07 Performance Lineup");
            var cameraObject = new GameObject(
                "G09-07 Performance Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 48f;
            camera.transform.position =
                new Vector3(0f, 10.5f, -23f);
            camera.transform.LookAt(
                new Vector3(0f, 1.4f, 3.5f),
                Vector3.up);
            var lightObject = new GameObject(
                "G09-07 Performance Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObject.transform.rotation =
                Quaternion.Euler(42f, -28f, 0f);

            for (int index = 0; index < 20; index++)
            {
                var actor = new GameObject(
                    "PerformanceActor_" + index);
                actor.transform.SetParent(lineup.transform, false);
                actor.transform.localPosition = new Vector3(
                    (index % 5 - 2) * 3.2f,
                    0f,
                    (index / 5) * 3.1f);

                bool attached;
                switch (index % 5)
                {
                    case 0:
                        attached =
                            BudgetVisualFactory.AttachPlayer(actor);
                        break;
                    case 1:
                        attached = BudgetVisualFactory.AttachNpc(
                            actor,
                            "QingshiGuard");
                        break;
                    case 2:
                        attached = AttachPerformanceEnemy(
                            actor,
                            EnemyContentIds.Bandit);
                        break;
                    case 3:
                        attached = AttachPerformanceEnemy(
                            actor,
                            EnemyContentIds.EliteWolf);
                        break;
                    default:
                        attached = AttachPerformanceEnemy(
                            actor,
                            EnemyContentIds.StoneGeneral);
                        break;
                }

                Assert.That(
                    attached,
                    Is.True,
                    actor.name);
            }

            for (int warmup = 0; warmup < 45; warmup++)
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
                    diagnostics.SampleFrame(
                        Mathf.Max(
                            0.000001f,
                            now - previous));
                    previous = now;
                }

                MvpRuntimeDiagnosticsReport report =
                    diagnostics.Report;
                int rendererCount = lineup
                    .GetComponentsInChildren<Renderer>(true)
                    .Count(renderer =>
                        renderer != null
                        && renderer.enabled);
                TestContext.WriteLine(
                    "G09-07 20-character proxy: "
                    + "1920x1080 Medium; "
                    + $"renderers={rendererCount}; "
                    + $"avg={report.AverageFramesPerSecond:0.0}fps; "
                    + $"worst={report.WorstFrameMilliseconds:0.00}ms; "
                    + "memory="
                    + $"{report.PeakAllocatedMemoryBytes / (1024f * 1024f):0.0}"
                    + "MiB.");
                Assert.That(rendererCount, Is.GreaterThanOrEqualTo(100));
                Assert.That(
                    report.AverageFramesPerSecond,
                    Is.GreaterThanOrEqualTo(
                        MvpRuntimeDiagnostics.MinimumFramesPerSecond));
                Assert.That(report.MeetsMemoryBudget, Is.True);
                Assert.That(report.ErrorCount, Is.Zero);
                Assert.That(report.ExceptionCount, Is.Zero);
            }

            Object.Destroy(lineup);
            Object.Destroy(cameraObject);
            Object.Destroy(lightObject);
            yield return null;
        }

        private static bool AttachPerformanceEnemy(
            GameObject actor,
            string enemyId)
        {
            EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
            data.Id = enemyId;
            bool attached =
                BudgetVisualFactory.AttachEnemy(actor, data);
            Object.DestroyImmediate(data);
            return attached;
        }

        private static void AssertModelParts(
            string resourcePath,
            params string[] requiredNames)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            Assert.That(prefab, Is.Not.Null, resourcePath);
            string[] names = prefab
                .GetComponentsInChildren<Transform>(true)
                .Select(child => child.name)
                .ToArray();
            foreach (string requiredName in requiredNames)
            {
                Assert.That(
                    names,
                    Does.Contain(requiredName),
                    resourcePath + " missing " + requiredName);
            }
        }

        private static void AssertConfiguredModularStyle(
            GameObject root,
            string expectedResourcePath)
        {
            ModularCharacterStyle style =
                root.GetComponentInChildren<ModularCharacterStyle>(true);
            Assert.That(style, Is.Not.Null, root.name);
            Assert.That(style.IsConfigured, Is.True, root.name);
            Assert.That(
                style.ResourcePath,
                Is.EqualTo(expectedResourcePath),
                root.name);
        }

        private static void AssertClipSet(
            string resourcePath,
            params string[] requiredSuffixes)
        {
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(
                resourcePath);
            Assert.That(clips, Is.Not.Empty, resourcePath);
            foreach (string suffix in requiredSuffixes)
            {
                Assert.That(
                    clips.Any(clip => clip != null
                        && clip.name.EndsWith(
                            suffix,
                            System.StringComparison.OrdinalIgnoreCase)),
                    Is.True,
                    resourcePath + " missing " + suffix);
            }
        }

        private static Transform FindTransform(
            GameObject root,
            string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
        }

        private static Renderer FindRenderer(
            GameObject root,
            string objectName)
        {
            return root.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => renderer.name == objectName);
        }

        private static void AssertRendererUsesTexture(
            GameObject root,
            string objectName,
            string expectedTextureName)
        {
            Renderer renderer = FindRenderer(root, objectName);
            Assert.That(renderer, Is.Not.Null, objectName);
            Assert.That(
                renderer.sharedMaterials.Any(material =>
                    material != null
                    && material.mainTexture != null
                    && material.mainTexture.name == expectedTextureName),
                Is.True,
                objectName + " did not receive " + expectedTextureName);
        }

        private static float MaximumRotationDelta(
            Transform[] transforms,
            Quaternion[] initial)
        {
            float maximum = 0f;
            for (int index = 0; index < transforms.Length; index++)
            {
                maximum = Mathf.Max(
                    maximum,
                    Quaternion.Angle(
                        initial[index],
                        transforms[index].localRotation));
            }
            return maximum;
        }

        private static float MaximumPositionDelta(
            Transform[] transforms,
            Vector3[] initial)
        {
            float maximum = 0f;
            for (int index = 0; index < transforms.Length; index++)
            {
                maximum = Mathf.Max(
                    maximum,
                    Vector3.Distance(
                        initial[index],
                        transforms[index].localPosition));
            }
            return maximum;
        }

        private static Bounds CalculateEnabledBounds(GameObject root)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in
                root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }
                bounds = found
                    ? Encapsulate(bounds, renderer.bounds)
                    : renderer.bounds;
                found = true;
            }
            return bounds;
        }

        private static Bounds Encapsulate(Bounds current, Bounds next)
        {
            current.Encapsulate(next);
            return current;
        }

    }
}
