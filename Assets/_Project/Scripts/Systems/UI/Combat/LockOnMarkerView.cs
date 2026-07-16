using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Player;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Combat
{
    public sealed class LockOnMarkerView : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;
        private RectTransform _markerRect;
        private GameObject _target;
        private Transform _targetTransform;

        public bool IsVisible =>
            _canvasGroup != null && _canvasGroup.alpha > 0f;
        public GameObject Target => _target;
        public Transform TargetTransform => _targetTransform;

        private void Awake()
        {
            BuildView();
            ApplyVisible(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LockOnInfo>(
                PlayerEvents.LockOnChanged,
                HandleLockOnChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LockOnInfo>(
                PlayerEvents.LockOnChanged,
                HandleLockOnChanged);
            ClearTarget();
        }

        private void LateUpdate()
        {
            if (_target == null
                || _targetTransform == null
                || !CanRemainLocked(_target))
            {
                ClearTarget();
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                ApplyVisible(false);
                return;
            }

            Vector3 viewport = camera.WorldToViewportPoint(
                _targetTransform.position);
            bool visible = viewport.z > 0f
                && viewport.x >= 0f
                && viewport.x <= 1f
                && viewport.y >= 0f
                && viewport.y <= 1f;
            ApplyVisible(visible);
            if (!visible)
            {
                return;
            }

            _markerRect.anchorMin = new Vector2(viewport.x, viewport.y);
            _markerRect.anchorMax = _markerRect.anchorMin;
            _markerRect.anchoredPosition = Vector2.zero;
        }

        public void RefreshNow()
        {
            LateUpdate();
        }

        private void HandleLockOnChanged(LockOnInfo info)
        {
            if (!info.Locked || info.Target == null)
            {
                ClearTarget();
                return;
            }

            _target = info.Target;
            _targetTransform = ResolveLockOnTransform(info.Target);
            ApplyVisible(_targetTransform != null);
        }

        private void ClearTarget()
        {
            _target = null;
            _targetTransform = null;
            ApplyVisible(false);
        }

        private static Transform ResolveLockOnTransform(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is ILockOnTarget lockOnTarget
                    && lockOnTarget.CanLockOn)
                {
                    return lockOnTarget.LockOnTransform != null
                        ? lockOnTarget.LockOnTransform
                        : target.transform;
                }
            }

            return target.transform;
        }

        private static bool CanRemainLocked(GameObject target)
        {
            MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is ILockOnTarget lockOnTarget)
                {
                    return lockOnTarget.CanLockOn;
                }
            }

            return target.activeInHierarchy;
        }

        private void BuildView()
        {
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "LockOnMarkerCanvas",
                118);
            _canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image marker = RuntimeUiFactory.CreateIcon(
                canvas.transform,
                "LockOnMarkerIcon",
                "target",
                new Vector2(58f, 58f),
                Vector2.zero,
                RuntimeUiTheme.Gold);
            _markerRect = marker.rectTransform;
            marker.raycastTarget = false;
        }

        private void ApplyVisible(bool visible)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }
}
