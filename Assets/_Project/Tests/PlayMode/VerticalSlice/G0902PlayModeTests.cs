using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wendao.Data;
using Wendao.Entities.Visuals;
using Wendao.Systems.Enemy;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0902PlayModeTests
    {
        [Test]
        public void RefinedCc0ResourcesArePackaged()
        {
            Assert.That(
                Resources.Load<GameObject>(BudgetArtCatalog.Player),
                Is.Not.Null);
            Assert.That(
                Resources.Load<GameObject>(BudgetArtCatalog.Wolf),
                Is.Not.Null);
            Assert.That(
                Resources.Load<GameObject>(BudgetArtCatalog.StoneGeneral),
                Is.Not.Null);
            Assert.That(
                Resources.Load<Texture2D>(BudgetArtCatalog.Player),
                Is.Not.Null);
            Assert.That(
                Resources.Load<Texture2D>(BudgetArtCatalog.StoneGeneral),
                Is.Not.Null);
            Assert.That(
                Resources.Load<Texture2D>(BudgetArtCatalog.QingshiSurface),
                Is.Not.Null);
            Assert.That(
                Resources.Load<Texture2D>(BudgetArtCatalog.CangwuSurface),
                Is.Not.Null);
            Assert.That(
                Resources.Load<Texture2D>(BudgetArtCatalog.BlackwindSurface),
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PlayerCreaturesAndBossUseDistinctRuntimeModels()
        {
            var player = new GameObject("G09-02 Player Probe");
            CapsuleCollider gameplayCollider = player.AddComponent<CapsuleCollider>();
            Assert.That(BudgetVisualFactory.AttachPlayer(player), Is.True);

            GameObject playerModel = FindChildStartingWith(
                player,
                "Model_Cultivator");
            Assert.That(playerModel, Is.Not.Null);
            Renderer auxiliaryCube = FindRenderer(playerModel, "Cube");
            if (auxiliaryCube != null)
            {
                Assert.That(auxiliaryCube.enabled, Is.False);
            }
            Assert.That(
                HasEnabledRenderer(playerModel, "Cube"),
                Is.False);
            Assert.That(gameplayCollider, Is.Not.Null);

            var wolf = new GameObject("G09-02 Wolf Probe");
            EnemyData wolfData = ScriptableObject.CreateInstance<EnemyData>();
            wolfData.Id = EnemyContentIds.GreyWolf;
            Assert.That(BudgetVisualFactory.AttachEnemy(wolf, wolfData), Is.True);
            Assert.That(
                FindChildStartingWith(wolf, "Model_Wolf"),
                Is.Not.Null);

            var eliteWolf = new GameObject("G09-02 Elite Wolf Probe");
            EnemyData eliteData = ScriptableObject.CreateInstance<EnemyData>();
            eliteData.Id = EnemyContentIds.EliteWolf;
            Assert.That(
                BudgetVisualFactory.AttachEnemy(eliteWolf, eliteData),
                Is.True);
            Assert.That(
                CalculateEnabledBounds(eliteWolf).size.magnitude,
                Is.GreaterThan(CalculateEnabledBounds(wolf).size.magnitude));

            var boss = new GameObject("G09-02 Stone General Probe");
            EnemyData bossData = ScriptableObject.CreateInstance<EnemyData>();
            bossData.Id = EnemyContentIds.StoneGeneral;
            Assert.That(BudgetVisualFactory.AttachEnemy(boss, bossData), Is.True);
            Assert.That(
                FindChildStartingWith(boss, "Model_Goleling_Evolved"),
                Is.Null);
            Assert.That(
                FindChildStartingWith(boss, "Model_StoneGeneral"),
                Is.Not.Null);

            yield return null;

            Assert.That(
                playerModel.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(
                FindChildStartingWith(wolf, "Model_Wolf")
                    .GetComponentsInChildren<Collider>(true),
                Is.Empty);

            Object.DestroyImmediate(player);
            Object.DestroyImmediate(wolf);
            Object.DestroyImmediate(eliteWolf);
            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(wolfData);
            Object.DestroyImmediate(eliteData);
            Object.DestroyImmediate(bossData);
        }

        [UnityTest]
        public IEnumerator ThreeMapsUseTexturedSurfacesAndCangwuHasNavigation()
        {
            Scene previous = SceneManager.GetActiveScene();
            string[] sceneNames =
            {
                SceneLoader.DefaultMapSceneName,
                SceneLoader.CangwuMapSceneName,
                SceneLoader.BlackwindDungeonSceneName
            };
            string[] surfaceNames =
            {
                QingshiGreyboxFactory.GroundName,
                CangwuGreyboxFactory.GroundName,
                "Blackwind_B1_Platform"
            };
            string[] expectedTextures =
            {
                "grass_path_2_diff_1k",
                "forest_ground_04_diff_1k",
                "rocky_terrain_diff_1k"
            };

            for (int index = 0; index < sceneNames.Length; index++)
            {
                Scene scene = SceneManager.GetSceneByName(sceneNames[index]);
                bool created = !scene.IsValid() || !scene.isLoaded;
                if (created)
                {
                    scene = SceneManager.CreateScene(sceneNames[index]);
                }
                SceneManager.SetActiveScene(scene);
                EnsureGreybox(sceneNames[index], scene);
                GameObject artRoot = BudgetWorldArtBootstrap.EnsureForScene(scene);
                yield return null;

                Assert.That(artRoot, Is.Not.Null, sceneNames[index]);
                Transform surface = FindTransform(scene, surfaceNames[index]);
                Assert.That(surface, Is.Not.Null, surfaceNames[index]);
                Collider collider = surface.GetComponent<Collider>();
                Renderer renderer = surface.GetComponent<Renderer>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.enabled, Is.True);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null);
                StringAssert.Contains(
                    expectedTextures[index],
                    renderer.sharedMaterial.mainTexture.name);
                AssertNoBrokenMaterials(scene);

                if (sceneNames[index] == SceneLoader.CangwuMapSceneName)
                {
                    Transform mapRoot = FindTransform(
                        scene,
                        CangwuGreyboxFactory.RootName);
                    Assert.That(mapRoot, Is.Not.Null);
                    CangwuNavigationSurface navigation =
                        mapRoot.GetComponent<CangwuNavigationSurface>();
                    Assert.That(navigation, Is.Not.Null);
                    Assert.That(navigation.BuildSourceCount, Is.GreaterThan(0));
                    Assert.That(navigation.IsBuilt, Is.True);
                }

                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
                if (created)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        [Test]
        public void MapVisualProfilesRemainDistinctAndReadable()
        {
            WorldVisualProfile qingshi = WorldEnvironmentProfiles.GetVisualProfile(
                SceneLoader.DefaultMapSceneName);
            WorldVisualProfile cangwu = WorldEnvironmentProfiles.GetVisualProfile(
                SceneLoader.CangwuMapSceneName);
            WorldVisualProfile blackwind = WorldEnvironmentProfiles.GetVisualProfile(
                SceneLoader.BlackwindDungeonSceneName);

            Assert.That(qingshi.UseSkybox, Is.True);
            Assert.That(cangwu.UseSkybox, Is.True);
            Assert.That(blackwind.UseSkybox, Is.False);
            Assert.That(qingshi.FogColor, Is.Not.EqualTo(cangwu.FogColor));
            Assert.That(cangwu.FogColor, Is.Not.EqualTo(blackwind.FogColor));
            Assert.That(blackwind.AmbientIntensity, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(blackwind.BaseFogDensity, Is.LessThan(0.02f));
        }

        private static void EnsureGreybox(string sceneName, Scene scene)
        {
            if (sceneName == SceneLoader.DefaultMapSceneName)
            {
                QingshiGreyboxFactory.EnsureCreated(scene);
            }
            else if (sceneName == SceneLoader.CangwuMapSceneName)
            {
                CangwuGreyboxFactory.EnsureCreated(scene);
            }
            else
            {
                BlackwindDungeonFactory.EnsureCreated(scene);
            }
        }

        private static GameObject FindChildStartingWith(
            GameObject root,
            string prefix)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return child.gameObject;
                }
            }
            return null;
        }

        private static Renderer FindRenderer(GameObject root, string name)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == name)
                {
                    return renderer;
                }
            }
            return null;
        }

        private static bool HasEnabledRenderer(GameObject root, string name)
        {
            Renderer renderer = FindRenderer(root, name);
            return renderer != null && renderer.enabled;
        }

        private static Bounds CalculateEnabledBounds(GameObject root)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            Assert.That(found, Is.True);
            return bounds;
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName)
                    {
                        return child;
                    }
                }
            }
            return null;
        }

        private static void AssertNoBrokenMaterials(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in
                    root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, renderer.name);
                        Assert.That(material.shader, Is.Not.Null, material.name);
                        Assert.That(material.shader.isSupported, Is.True, material.name);
                        Assert.That(
                            material.shader.name,
                            Is.Not.EqualTo("Hidden/InternalErrorShader"),
                            material.name);
                    }
                }
            }
        }
    }
}
