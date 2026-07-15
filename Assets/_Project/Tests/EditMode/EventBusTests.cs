using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;

namespace Wendao.Tests.EditMode
{
    public sealed class EventBusTests
    {
        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
        }

        [Test]
        public void TypedSubscribePublishAndUnsubscribeUseThePayload()
        {
            const string eventName = "test_typed";
            var observed = 0;
            Action<int> handler = value => observed += value;

            EventBus.Subscribe(eventName, handler);
            EventBus.Publish(eventName, 7);
            EventBus.Unsubscribe(eventName, handler);
            EventBus.Publish(eventName, 11);

            Assert.That(observed, Is.EqualTo(7));
        }

        [Test]
        public void ParameterlessSubscribePublishAndUnsubscribeInvokeOnce()
        {
            const string eventName = "test_parameterless";
            var calls = 0;
            Action handler = () => calls++;

            EventBus.Subscribe(eventName, handler);
            EventBus.Publish(eventName);
            EventBus.Unsubscribe(eventName, handler);
            EventBus.Publish(eventName);

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void TypedAndParameterlessEventsWithTheSameNameAreIsolated()
        {
            const string eventName = "test_overload";
            var typedCalls = 0;
            var parameterlessCalls = 0;

            EventBus.Subscribe<int>(eventName, _ => typedCalls++);
            EventBus.Subscribe(eventName, () => parameterlessCalls++);

            EventBus.Publish(eventName, 1);
            EventBus.Publish(eventName);

            Assert.That(typedCalls, Is.EqualTo(1));
            Assert.That(parameterlessCalls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingTypedHandlerDoesNotBlockLaterHandlers()
        {
            const string eventName = "test_typed_exception";
            var laterHandlerCalls = 0;
            LogAssert.Expect(LogType.Exception, new Regex("expected typed handler failure"));

            EventBus.Subscribe<int>(eventName, _ =>
                throw new InvalidOperationException("expected typed handler failure"));
            EventBus.Subscribe<int>(eventName, _ => laterHandlerCalls++);

            EventBus.Publish(eventName, 1);

            Assert.That(laterHandlerCalls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingParameterlessHandlerDoesNotBlockLaterHandlers()
        {
            const string eventName = "test_parameterless_exception";
            var laterHandlerCalls = 0;
            LogAssert.Expect(LogType.Exception, new Regex("expected parameterless handler failure"));

            EventBus.Subscribe(eventName, () =>
                throw new InvalidOperationException("expected parameterless handler failure"));
            EventBus.Subscribe(eventName, () => laterHandlerCalls++);

            EventBus.Publish(eventName);

            Assert.That(laterHandlerCalls, Is.EqualTo(1));
        }

        [Test]
        public void ClearRemovesTypedAndParameterlessHandlers()
        {
            const string eventName = "test_clear";
            var typedCalls = 0;
            var parameterlessCalls = 0;

            EventBus.Subscribe<int>(eventName, _ => typedCalls++);
            EventBus.Subscribe(eventName, () => parameterlessCalls++);

            EventBus.Clear();
            EventBus.Publish(eventName, 1);
            EventBus.Publish(eventName);

            Assert.That(typedCalls, Is.Zero);
            Assert.That(parameterlessCalls, Is.Zero);
        }
    }
}

