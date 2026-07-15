using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Wendao.Tests.PlayMode
{
    public sealed class BootScenePlayModeTests
    {
        private const string BootSceneName = "Boot";
        private const string BootScenePath = "Assets/_Project/Scenes/Core/Boot.unity";

        [UnityTest]
        public IEnumerator BootSceneLoadsInPlayMode()
        {
            var bootWasLoaded = false;
            void ObserveSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path == BootScenePath)
                {
                    bootWasLoaded = true;
                }
            }

            SceneManager.sceneLoaded += ObserveSceneLoaded;
            var loadOperation = SceneManager.LoadSceneAsync(BootSceneName, LoadSceneMode.Single);

            Assert.That(loadOperation, Is.Not.Null, "Boot scene must be present in Editor Build Settings.");
            yield return loadOperation;
            SceneManager.sceneLoaded -= ObserveSceneLoaded;

            Assert.That(bootWasLoaded, Is.True);
        }
    }
}
