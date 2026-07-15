using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wendao.Core
{
    public interface IPoolable
    {
        void OnTakenFromPool();
        void OnReturnedToPool();
    }

    /// <summary>
    /// Component pool with ownership and double-return protection.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _inactive = new Stack<T>();
        private readonly HashSet<T> _inactiveInstances = new HashSet<T>();
        private readonly HashSet<T> _allInstances = new HashSet<T>();

        public ObjectPool(T prefab, int prewarm, Transform parent = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (prewarm < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(prewarm));
            }

            _prefab = prefab;
            _parent = parent;
            Prewarm(prewarm);
        }

        public int CountAll => _allInstances.Count;

        public int CountInactive => _inactiveInstances.Count;

        public T Get()
        {
            T instance = null;
            while (_inactive.Count > 0 && instance == null)
            {
                instance = _inactive.Pop();
                _inactiveInstances.Remove(instance);
                if (instance == null)
                {
                    _allInstances.Remove(instance);
                }
            }

            if (instance == null)
            {
                instance = CreateInstance();
            }

            instance.gameObject.SetActive(true);
            if (instance is IPoolable poolable)
            {
                poolable.OnTakenFromPool();
            }

            return instance;
        }

        public void Return(T instance)
        {
            if (instance == null)
            {
                return;
            }

            if (!_allInstances.Contains(instance))
            {
                Debug.LogWarning($"Ignoring an instance not owned by {GetType().Name}.", instance);
                return;
            }

            if (!_inactiveInstances.Add(instance))
            {
                Debug.LogWarning($"Ignoring a duplicate return to {GetType().Name}.", instance);
                return;
            }

            if (instance is IPoolable poolable)
            {
                poolable.OnReturnedToPool();
            }

            instance.gameObject.SetActive(false);
            if (_parent != null)
            {
                instance.transform.SetParent(_parent, false);
            }

            _inactive.Push(instance);
        }

        public void Prewarm(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            for (int i = 0; i < count; i++)
            {
                T instance = CreateInstance();
                instance.gameObject.SetActive(false);
                _inactiveInstances.Add(instance);
                _inactive.Push(instance);
            }
        }

        public void Clear()
        {
            foreach (T instance in _allInstances)
            {
                if (instance == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(instance.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(instance.gameObject);
                }
            }

            _inactive.Clear();
            _inactiveInstances.Clear();
            _allInstances.Clear();
        }

        private T CreateInstance()
        {
            T instance = Object.Instantiate(_prefab, _parent, false);
            instance.name = $"{_prefab.name} (Pooled)";
            _allInstances.Add(instance);
            return instance;
        }
    }
}
