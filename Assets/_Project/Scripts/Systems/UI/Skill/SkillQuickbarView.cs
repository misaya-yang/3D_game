using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Skill;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Skill
{
    public sealed class SkillQuickbarView : MonoBehaviour
    {
        public const string SkillNameLocalizationKey =
            "skill_name_skill_basic_qi_bolt";
        public const string SkillNameDefaultValue = "引气弹";
        public const string SkillDescriptionLocalizationKey =
            "skill_desc_skill_basic_qi_bolt";
        public const string SkillDescriptionDefaultValue =
            "凝聚灵气，向前射出一道引气弹。";
        public const string SlotOneLocalizationKey = "ui_skill_slot_one";
        public const string SlotOneDefaultValue = "1  {0}";
        public const string SlotLocalizationKey = "ui_skill_slot";
        public const string SlotDefaultValue = "{0}  {1}";
        public const string CooldownLocalizationKey = "ui_skill_cooldown";
        public const string CooldownDefaultValue = "冷却 {0:0.0}秒";
        public const string PlayerManaLocalizationKey = "ui_player_mana";
        public const string PlayerManaDefaultValue = "灵力 {0:0}/{1:0}";
        public const string EmptyLocalizationKey = "ui_skill_empty";
        public const string EmptyDefaultValue = "空";

        private readonly Image[] _slotBackgrounds =
            new Image[SkillManager.BarSlotCount];
        private readonly Text[] _slotLabels =
            new Text[SkillManager.BarSlotCount];
        private readonly Text[] _cooldownLabels =
            new Text[SkillManager.BarSlotCount];
        private readonly Image[] _slotIcons =
            new Image[SkillManager.BarSlotCount];
        private readonly string[] _currentSkillIds =
            new string[SkillManager.BarSlotCount];
        private readonly SkillQuickbarSlotDropTarget[] _dropTargets =
            new SkillQuickbarSlotDropTarget[SkillManager.BarSlotCount];

        private Text _manaLabel;
        private ISkillService _skillService;
        private IPlayerResourceService _resourceService;

        public int SlotCount => _slotLabels.Length;
        public string CurrentSkillId => GetCurrentSkillId(0);
        public string CurrentSkillNameLocalizationKey { get; private set; } =
            EmptyLocalizationKey;
        public string SlotText => GetSlotText(0);
        public string CooldownText => GetCooldownText(0);
        public string ManaText => _manaLabel != null ? _manaLabel.text : string.Empty;

        private void Awake()
        {
            BuildView();
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            ServiceLocator.TryGet(out _skillService);
            ServiceLocator.TryGet(out _resourceService);

            string[] equipped = _skillService?.EquippedIds;
            float currentMana = _resourceService?.CurrentMana ?? 0f;
            for (int index = 0; index < _slotLabels.Length; index++)
            {
                string skillId = equipped != null && index < equipped.Length
                    ? equipped[index] ?? string.Empty
                    : string.Empty;
                _currentSkillIds[index] = skillId;
                SkillData skill = ConfigDatabase.Instance?.GetSkill(skillId);
                string displayName = skill != null
                    ? skill.DisplayName
                    : EmptyDefaultValue;
                _slotLabels[index].text = index == 0
                    ? string.Format(SlotOneDefaultValue, displayName)
                    : string.Format(SlotDefaultValue, index + 1, displayName);

                float cooldown = _skillService?.GetCooldownRemaining(index) ?? 0f;
                _cooldownLabels[index].text = cooldown > 0.0001f
                    ? string.Format(CooldownDefaultValue, cooldown)
                    : string.Empty;

                bool canAfford = skill == null
                    || currentMana + 0.0001f >= skill.BaseManaCost;
                _slotBackgrounds[index].color = cooldown > 0.0001f
                    ? new Color(0.08f, 0.12f, 0.1f, 0.94f)
                    : canAfford
                        ? new Color(0.16f, 0.31f, 0.24f, 0.96f)
                        : new Color(0.39f, 0.14f, 0.12f, 0.96f);
                _slotIcons[index].sprite = RuntimeUiTheme.GetIcon(
                    skill != null ? "target" : "locked");
                _slotIcons[index].color = skill != null
                    ? RuntimeUiTheme.GoldSoft
                    : new Color(0.48f, 0.54f, 0.49f, 0.75f);
            }

            SkillData primary = ConfigDatabase.Instance?.GetSkill(
                GetCurrentSkillId(0));
            CurrentSkillNameLocalizationKey = primary != null
                ? GetSkillNameLocalizationKey(primary.Id)
                : EmptyLocalizationKey;

            float maxMana = _resourceService?.MaxMana ?? 0f;
            _manaLabel.text = string.Format(
                PlayerManaDefaultValue,
                currentMana,
                maxMana);
        }

        public string GetCurrentSkillId(int barIndex)
        {
            return barIndex >= 0 && barIndex < _currentSkillIds.Length
                ? _currentSkillIds[barIndex] ?? string.Empty
                : string.Empty;
        }

        public string GetSlotText(int barIndex)
        {
            return barIndex >= 0
                && barIndex < _slotLabels.Length
                && _slotLabels[barIndex] != null
                ? _slotLabels[barIndex].text
                : string.Empty;
        }

        public string GetCooldownText(int barIndex)
        {
            return barIndex >= 0
                && barIndex < _cooldownLabels.Length
                && _cooldownLabels[barIndex] != null
                ? _cooldownLabels[barIndex].text
                : string.Empty;
        }

        public SkillQuickbarSlotDropTarget GetDropTarget(int barIndex)
        {
            return barIndex >= 0 && barIndex < _dropTargets.Length
                ? _dropTargets[barIndex]
                : null;
        }

        public bool TryEquipDroppedSkill(string skillId, int barIndex)
        {
            if (_skillService == null)
            {
                ServiceLocator.TryGet(out _skillService);
            }

            bool equipped = _skillService != null
                && _skillService.Equip(skillId, barIndex);
            if (equipped)
            {
                Refresh();
            }

            return equipped;
        }

        public static string GetSkillNameLocalizationKey(string skillId)
        {
            return string.IsNullOrEmpty(skillId)
                ? EmptyLocalizationKey
                : "skill_name_" + skillId;
        }

        private void BuildView()
        {
            RuntimeUiFactory.EnsureEventSystem();
            Canvas canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "SkillQuickbarCanvas",
                230);
            for (int index = 0; index < _slotBackgrounds.Length; index++)
            {
                float x = (index - 1.5f) * 122f;
                Image background = RuntimeUiFactory.CreateImage(
                    canvas.transform,
                    $"SkillSlot{index + 1}",
                    RuntimeUiTheme.SurfaceRaised,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(110f, 94f),
                    new Vector2(x, 68f));
                background.sprite = RuntimeUiTheme.SquareButtonSprite;
                background.type = Image.Type.Sliced;
                _slotBackgrounds[index] = background;
                _slotIcons[index] = RuntimeUiFactory.CreateIcon(
                    background.transform,
                    $"SkillSlot{index + 1}Icon",
                    "locked",
                    new Vector2(34f, 34f),
                    new Vector2(0f, 14f),
                    RuntimeUiTheme.Muted);
                _slotLabels[index] = RuntimeUiFactory.CreateText(
                    background.transform,
                    $"SkillSlot{index + 1}Label",
                    string.Format(SlotDefaultValue, index + 1, EmptyDefaultValue),
                    17,
                    RuntimeUiTheme.Parchment,
                    new Vector2(100f, 31f),
                    new Vector2(0f, -24f));
                RuntimeUiTheme.StyleText(
                    _slotLabels[index],
                    RuntimeUiTextRole.Body);
                _cooldownLabels[index] = RuntimeUiFactory.CreateText(
                    background.transform,
                    $"SkillSlot{index + 1}Cooldown",
                    string.Empty,
                    16,
                    RuntimeUiTheme.GoldSoft,
                    new Vector2(100f, 36f),
                    new Vector2(0f, 12f));
                RuntimeUiTheme.StyleText(
                    _cooldownLabels[index],
                    RuntimeUiTextRole.Warning);
                SkillQuickbarSlotDropTarget dropTarget =
                    background.gameObject.AddComponent<SkillQuickbarSlotDropTarget>();
                dropTarget.Configure(this, index);
                _dropTargets[index] = dropTarget;
            }

            _manaLabel = RuntimeUiFactory.CreateText(
                canvas.transform,
                "PlayerManaLabel",
                string.Format(PlayerManaDefaultValue, 0f, 0f),
                18,
                RuntimeUiTheme.Mana,
                new Vector2(260f, 30f),
                new Vector2(0f, 124f));
            RuntimeUiTheme.StyleText(_manaLabel, RuntimeUiTextRole.Body);
            RectTransform manaRect = _manaLabel.rectTransform;
            manaRect.anchorMin = new Vector2(0.5f, 0f);
            manaRect.anchorMax = new Vector2(0.5f, 0f);
        }
    }
}
