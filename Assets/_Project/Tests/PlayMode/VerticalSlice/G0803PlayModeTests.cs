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
    public sealed class G0803PlayModeTests
    {
        [Test]
        public void CuratedBudgetResourcesArePackagedAndRenderable()
        {
            GameObject[] models = Resources.LoadAll<GameObject>(
                BudgetArtCatalog.ResourceRoot);
            Assert.That(models.Length, Is.GreaterThanOrEqualTo(58));

            foreach (string texturePath in
                BudgetArtCatalog.RequiredCharacterTextureResources)
            {
                Assert.That(
                    Resources.Load<Texture2D>(texturePath),
                    Is.Not.Null,
                    texturePath);
            }

            foreach (string path in BudgetArtCatalog.RequiredCharacterResources)
            {
                GameObject prefab = Resources.Load<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                GameObject instance = Object.Instantiate(prefab);
                Renderer[] renderers =
                    instance.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, path);
                Object.DestroyImmediate(instance);

                var visualProbe = new GameObject("VisualProbe");
                CharacterVisualRole role = path == BudgetArtCatalog.Player
                    ? CharacterVisualRole.Player
                    : path == BudgetArtCatalog.HumanEnemy
                        ? CharacterVisualRole.HumanEnemy
                        : CharacterVisualRole.Npc;
                Assert.That(
                    BudgetVisualFactory.AttachResourceVisual(
                        visualProbe,
                        path,
                        1.7f,
                        false,
                        0f,
                        Vector3.zero,
                        Color.white,
                        false,
                        BudgetMaterialProfile.Character,
                        role),
                    Is.True,
                    path);
                bool hasTexture = false;
                foreach (Renderer renderer in
                    visualProbe.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        hasTexture |= material != null
                            && material.mainTexture != null;
                    }
                }
                Assert.That(hasTexture, Is.True, path + " texture");
                Object.DestroyImmediate(visualProbe);
            }
        }

        [Test]
        public void CharacterAndEnemyVisualsKeepGameplayRootsIntact()
        {
            var player = new GameObject("PlayerVisualProbe");
            var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallback.name = "GreyboxBody";
            fallback.transform.SetParent(player.transform, false);
            Assert.That(BudgetVisualFactory.AttachPlayer(player), Is.True);
            Assert.That(
                player.transform.Find(BudgetVisualFactory.VisualRootName),
                Is.Not.Null);
            Assert.That(fallback.GetComponent<Renderer>().enabled, Is.False);

            var wolf = new GameObject("WolfVisualProbe");
            var wolfFallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            wolfFallback.transform.SetParent(wolf.transform, false);
            EnemyData wolfData = ScriptableObject.CreateInstance<EnemyData>();
            wolfData.Id = EnemyContentIds.GreyWolf;
            Assert.That(BudgetVisualFactory.AttachEnemy(wolf, wolfData), Is.True);
            Assert.That(
                wolf.transform.Find(BudgetVisualFactory.VisualRootName),
                Is.Not.Null);
            Assert.That(wolfFallback.GetComponent<Renderer>().enabled, Is.False);

            Object.DestroyImmediate(player);
            Object.DestroyImmediate(wolf);
            Object.DestroyImmediate(wolfData);
        }

        [UnityTest]
        public IEnumerator ThreeMvpMapsReceiveDistinctBudgetArtWithoutReplacingCollision()
        {
            Scene previous = SceneManager.GetActiveScene();
            string[] sceneNames =
            {
                SceneLoader.DefaultMapSceneName,
                SceneLoader.CangwuMapSceneName,
                SceneLoader.BlackwindDungeonSceneName
            };
            int[] minimums =
            {
                BudgetWorldArtBootstrap.QingshiMinimumDecorationCount,
                BudgetWorldArtBootstrap.CangwuMinimumDecorationCount,
                BudgetWorldArtBootstrap.BlackwindMinimumDecorationCount
            };

            for (int index = 0; index < sceneNames.Length; index++)
            {
                Scene scene = SceneManager.GetSceneByName(sceneNames[index]);
                bool createdScene = !scene.IsValid() || !scene.isLoaded;
                if (createdScene)
                {
                    scene = SceneManager.CreateScene(sceneNames[index]);
                }
                SceneManager.SetActiveScene(scene);
                EnsureGreybox(sceneNames[index], scene);
                GameObject artRoot = BudgetWorldArtBootstrap.EnsureForScene(scene);
                yield return null;

                Assert.That(artRoot, Is.Not.Null, sceneNames[index]);
                Assert.That(
                    BudgetWorldArtBootstrap.CountDecorations(scene),
                    Is.GreaterThanOrEqualTo(minimums[index]),
                    sceneNames[index]);
                Assert.That(FindGameplayGround(sceneNames[index]), Is.Not.Null);
                Assert.That(
                    FindGameplayGround(sceneNames[index]).GetComponent<Collider>().enabled,
                    Is.True,
                    sceneNames[index]);

                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
                if (createdScene)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
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

        private static GameObject FindGameplayGround(string sceneName)
        {
            if (sceneName == SceneLoader.DefaultMapSceneName)
            {
                return GameObject.Find(QingshiGreyboxFactory.GroundName);
            }
            if (sceneName == SceneLoader.CangwuMapSceneName)
            {
                return GameObject.Find(CangwuGreyboxFactory.GroundName);
            }
            return GameObject.Find("Blackwind_B1_Platform");
        }
    }
}
