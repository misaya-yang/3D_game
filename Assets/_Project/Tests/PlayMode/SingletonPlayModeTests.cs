using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wendao.Tests.PlayMode
{
    public sealed class SingletonPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyAllTestSingletons();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAllTestSingletons();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstInstanceBecomesThePersistentInstance()
        {
            var gameObject = new GameObject("Primary Test Singleton");
            var singleton = gameObject.AddComponent<TestSingleton>();

            yield return null;

            Assert.That(TestSingleton.HasInstance, Is.True);
            Assert.That(TestSingleton.Instance, Is.SameAs(singleton));
            Assert.That(singleton.AwakeHookCalls, Is.EqualTo(1));
            Assert.That(singleton.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"));
        }

        [UnityTest]
        public IEnumerator DuplicateInstanceIsDestroyedWithoutReplacingThePrimary()
        {
            var primaryObject = new GameObject("Primary Test Singleton");
            var primary = primaryObject.AddComponent<TestSingleton>();
            var duplicateObject = new GameObject("Duplicate Test Singleton");
            var duplicate = duplicateObject.AddComponent<TestSingleton>();

            yield return null;

            Assert.That(TestSingleton.Instance, Is.SameAs(primary));
            Assert.That(primary.AwakeHookCalls, Is.EqualTo(1));
            Assert.That(duplicate == null, Is.True);
            Assert.That(duplicateObject == null, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyingThePrimaryClearsTheStaticInstance()
        {
            var gameObject = new GameObject("Primary Test Singleton");
            gameObject.AddComponent<TestSingleton>();

            Object.Destroy(gameObject);
            yield return null;

            Assert.That(TestSingleton.HasInstance, Is.False);
            Assert.That(TestSingleton.Instance, Is.Null);
        }

        private static void DestroyAllTestSingletons()
        {
            var instances = Object.FindObjectsByType<TestSingleton>(FindObjectsInactive.Include);

            foreach (var instance in instances)
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }
        }
    }
}

