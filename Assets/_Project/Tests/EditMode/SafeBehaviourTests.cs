using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wendao.Tests.EditMode
{
    public sealed class SafeBehaviourTests
    {
        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void SafeStart_CompletesNormallyWithoutDisablingComponent()
        {
            _gameObject = new GameObject("SafeBehaviour Test");
            var probe = _gameObject.AddComponent<SafeBehaviourProbe>();

            probe.InvokeStartForTest();

            Assert.That(probe.SafeStartCalls, Is.EqualTo(1));
            Assert.That(probe.enabled, Is.True);
            Assert.That(probe.ObservedFailure, Is.Null);
        }

        [Test]
        public void SafeStart_ContainsExceptionAndDisablesFailedComponent()
        {
            _gameObject = new GameObject("SafeBehaviour Failure Test");
            var probe = _gameObject.AddComponent<SafeBehaviourProbe>();
            probe.ThrowOnStart = true;
            LogAssert.Expect(LogType.Exception, new Regex("expected safe start failure"));

            Assert.DoesNotThrow(probe.InvokeStartForTest);

            Assert.That(probe.SafeStartCalls, Is.EqualTo(1));
            Assert.That(probe.enabled, Is.False);
            Assert.That(probe.ObservedFailure, Is.Not.Null);
        }
    }
}
