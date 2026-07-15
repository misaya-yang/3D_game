using UnityEngine;
using UnityEngine.EventSystems;

namespace Wendao.UI.Skill
{
    public sealed class SkillQuickbarSlotDropTarget : MonoBehaviour, IDropHandler
    {
        private SkillQuickbarView _owner;

        public int BarIndex { get; private set; } = -1;
        public string LastDroppedSkillId { get; private set; } = string.Empty;

        public void Configure(SkillQuickbarView owner, int barIndex)
        {
            _owner = owner;
            BarIndex = barIndex;
        }

        public void OnDrop(PointerEventData eventData)
        {
            SkillDragSource source = eventData?.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<SkillDragSource>()
                : null;
            if (source == null
                || string.IsNullOrEmpty(source.SkillId)
                || _owner == null
                || !_owner.TryEquipDroppedSkill(source.SkillId, BarIndex))
            {
                return;
            }

            LastDroppedSkillId = source.SkillId;
        }
    }
}
