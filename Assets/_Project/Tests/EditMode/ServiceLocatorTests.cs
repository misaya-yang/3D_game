using System;
using NUnit.Framework;
using Wendao.Core;

namespace Wendao.Tests.EditMode
{
    public sealed class ServiceLocatorTests
    {
        private interface IFakeService
        {
            int Value { get; }
        }

        private sealed class FakeService : IFakeService
        {
            public FakeService(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        [Test]
        public void RegisterGetAndTryGetUseTheRequestedContractType()
        {
            IFakeService service = new FakeService(42);

            ServiceLocator.Register(service);

            Assert.That(ServiceLocator.Get<IFakeService>(), Is.SameAs(service));
            Assert.That(ServiceLocator.TryGet<IFakeService>(out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(service));
            Assert.That(resolved.Value, Is.EqualTo(42));
        }

        [Test]
        public void RegisteringTheSameInstanceTwiceIsIdempotent()
        {
            IFakeService service = new FakeService(1);

            ServiceLocator.Register(service);

            Assert.DoesNotThrow(() => ServiceLocator.Register(service));
            Assert.That(ServiceLocator.Get<IFakeService>(), Is.SameAs(service));
        }

        [Test]
        public void RegisteringADifferentInstanceForTheSameContractFails()
        {
            ServiceLocator.Register<IFakeService>(new FakeService(1));

            Assert.Throws<InvalidOperationException>(() =>
                ServiceLocator.Register<IFakeService>(new FakeService(2)));
        }

        [Test]
        public void UnregisterRemovesTheService()
        {
            ServiceLocator.Register<IFakeService>(new FakeService(1));

            ServiceLocator.Unregister<IFakeService>();

            Assert.That(ServiceLocator.TryGet<IFakeService>(out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IFakeService>());
        }

        [Test]
        public void ClearRemovesEveryService()
        {
            ServiceLocator.Register<IFakeService>(new FakeService(1));
            ServiceLocator.Register(new FakeService(2));

            ServiceLocator.Clear();

            Assert.That(ServiceLocator.TryGet<IFakeService>(out _), Is.False);
            Assert.That(ServiceLocator.TryGet<FakeService>(out _), Is.False);
        }

        [Test]
        public void NullServicesAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ServiceLocator.Register<IFakeService>(null));
        }
    }
}

