using NUnit.Framework;
using UnityEngine;
using Wendao.Core;

namespace Wendao.Tests.EditMode
{
    public sealed class ObjectPoolTests
    {
        private GameObject _prefabObject;
        private GameObject _parentObject;
        private ObjectPool<PoolProbe> _pool;

        [SetUp]
        public void SetUp()
        {
            _prefabObject = new GameObject("Pool Probe Prefab");
            var prefab = _prefabObject.AddComponent<PoolProbe>();
            _parentObject = new GameObject("Pool Root");
            _pool = new ObjectPool<PoolProbe>(prefab, 2, _parentObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            _pool?.Clear();
            if (_prefabObject != null)
            {
                Object.DestroyImmediate(_prefabObject);
            }

            if (_parentObject != null)
            {
                Object.DestroyImmediate(_parentObject);
            }
        }

        [Test]
        public void GetAndReturn_ReuseOwnedInstanceAndInvokeLifecycle()
        {
            Assert.That(_pool.CountAll, Is.EqualTo(2));
            Assert.That(_pool.CountInactive, Is.EqualTo(2));

            PoolProbe first = _pool.Get();
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(first.TakenCalls, Is.EqualTo(1));
            Assert.That(_pool.CountInactive, Is.EqualTo(1));

            _pool.Return(first);
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(first.ReturnedCalls, Is.EqualTo(1));
            Assert.That(first.transform.parent, Is.SameAs(_parentObject.transform));
            Assert.That(_pool.CountInactive, Is.EqualTo(2));

            PoolProbe reused = _pool.Get();
            Assert.That(reused, Is.SameAs(first));
            Assert.That(reused.TakenCalls, Is.EqualTo(2));
        }

        [Test]
        public void GetBeyondPrewarm_GrowsPoolAndClearDestroysInstances()
        {
            PoolProbe first = _pool.Get();
            PoolProbe second = _pool.Get();
            PoolProbe third = _pool.Get();

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(third, Is.Not.SameAs(first));
            Assert.That(third, Is.Not.SameAs(second));
            Assert.That(_pool.CountAll, Is.EqualTo(3));

            _pool.Clear();

            Assert.That(_pool.CountAll, Is.Zero);
            Assert.That(_pool.CountInactive, Is.Zero);
            Assert.That(first == null, Is.True);
            Assert.That(second == null, Is.True);
            Assert.That(third == null, Is.True);
        }
    }
}
