using UnityEngine;

namespace Wendao.Systems.Feedback
{
    public sealed class RuntimeVfxInstance : MonoBehaviour
    {
        private VFXManager _owner;
        private float _remaining;

        public void Initialize(VFXManager owner, float duration)
        {
            _owner = owner;
            _remaining = Mathf.Max(0.05f, duration);
        }

        private void Update()
        {
            _remaining -= Mathf.Max(0f, Time.deltaTime);
            if (_remaining <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_owner != null)
            {
                _owner.NotifyDestroyed(gameObject);
            }
        }
    }
}
