using UnityEngine;

namespace Wendao.Core
{
    /// <summary>
    /// Base class for a scene-persistent MonoBehaviour with exactly one live instance.
    /// Derived classes should override <see cref="OnSingletonAwake"/> instead of Awake.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<T>(FindObjectsInactive.Include);
                }

                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        protected virtual bool PersistAcrossScenes => true;

        protected bool IsSingletonInstance => ReferenceEquals(_instance, this);

        protected virtual void Awake()
        {
            var self = this as T;
            if (self == null)
            {
                Debug.LogError(
                    $"{GetType().FullName} must use itself as Singleton<T>'s type argument.",
                    this);
                Destroy(gameObject);
                return;
            }

            if (_instance != null && !ReferenceEquals(_instance, self))
            {
                Destroy(gameObject);
                return;
            }

            _instance = self;
            if (PersistAcrossScenes && Application.isPlaying)
            {
                if (transform.parent != null)
                {
                    transform.SetParent(null, true);
                }

                DontDestroyOnLoad(gameObject);
            }

            OnSingletonAwake();
        }

        protected virtual void OnDestroy()
        {
            if (!ReferenceEquals(_instance, this))
            {
                return;
            }

            OnSingletonDestroyed();
            _instance = null;
        }

        protected virtual void OnSingletonAwake()
        {
        }

        protected virtual void OnSingletonDestroyed()
        {
        }
    }
}
