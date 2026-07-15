using UnityEngine;
using UnityEngine.EventSystems;

namespace Wendao.UI.Skill
{
    public sealed class SkillDragSource : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private CanvasGroup _canvasGroup;

        public string SkillId { get; private set; } = string.Empty;
        public bool IsDragging { get; private set; }

        public void Configure(string skillId)
        {
            SkillId = skillId ?? string.Empty;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(SkillId))
            {
                return;
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            IsDragging = true;
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // The row remains anchored; pointerDrag carries its SkillId to the HUD slot.
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            IsDragging = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
            }
        }
    }
}
