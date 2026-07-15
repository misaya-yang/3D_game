using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.UI.SceneFlow;

namespace Wendao.UI.Combat
{
    public sealed class DamageFloatingTextView : MonoBehaviour
    {
        private const float Lifetime = 0.85f;
        private const float VerticalSpeed = 90f;
        private const int MaximumVisibleNumbers = 32;

        private readonly List<DamageNumberEntry> _entries =
            new List<DamageNumberEntry>();

        private Canvas _canvas;
        private RectTransform _canvasRect;

        public int ActiveNumberCount => _entries.Count;
        public float LastDamageAmount { get; private set; }
        public string LastRenderedText { get; private set; } = string.Empty;
        public ElementReactionType LastReaction { get; private set; }
        public string LastReactionLocalizationKey { get; private set; } =
            string.Empty;
        public string LastReactionRenderedText { get; private set; } =
            string.Empty;
        public Color LastReactionColor { get; private set; } = Color.white;
        public int ReactionNumberCount { get; private set; }

        private void Awake()
        {
            _canvas = RuntimeUiFactory.CreateCanvas(
                transform,
                "DamageFloatingTextCanvas",
                150);
            _canvasRect = (RectTransform)_canvas.transform;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Subscribe<ElementReactionInfo>(
                CombatEvents.ElementReactionTriggered,
                HandleElementReactionTriggered);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
            EventBus.Unsubscribe<ElementReactionInfo>(
                CombatEvents.ElementReactionTriggered,
                HandleElementReactionTriggered);
        }

        private void Update()
        {
            UnityEngine.Camera worldCamera = UnityEngine.Camera.main;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                DamageNumberEntry entry = _entries[i];
                entry.Age += Time.unscaledDeltaTime;
                if (entry.Age >= Lifetime || entry.Label == null)
                {
                    if (entry.Label != null)
                    {
                        Destroy(entry.Label.gameObject);
                    }

                    _entries.RemoveAt(i);
                    continue;
                }

                UpdateEntryPose(entry, worldCamera);
            }
        }

        private void HandleDamageApplied(DamageInfo info)
        {
            if (info.Amount <= 0f || _canvas == null)
            {
                return;
            }

            string renderedText = Mathf.CeilToInt(info.Amount)
                .ToString(CultureInfo.InvariantCulture);
            SpawnFloatingText(
                "DamageNumber",
                renderedText,
                info.IsCritical ? 36 : 30,
                info.IsCritical
                    ? new Color(1f, 0.75f, 0.2f, 1f)
                    : new Color(1f, 0.92f, 0.82f, 1f),
                info.HitPoint + Vector3.up * 0.35f);
            LastDamageAmount = info.Amount;
            LastRenderedText = renderedText;
        }

        private void HandleElementReactionTriggered(ElementReactionInfo info)
        {
            if (info.Reaction == ElementReactionType.None || _canvas == null)
            {
                return;
            }

            string key = GetReactionLocalizationKey(info.Reaction);
            string defaultValue = GetReactionDefaultValue(info.Reaction);
            Color color = GetReactionColor(info.Reaction);
            Vector3 worldPosition = info.Target != null
                ? info.Target.transform.position + Vector3.up * 1.25f
                : Vector3.zero;
            SpawnFloatingText(
                "ElementReactionNumber",
                defaultValue,
                34,
                color,
                worldPosition);

            LastReaction = info.Reaction;
            LastReactionLocalizationKey = key;
            LastReactionRenderedText = defaultValue;
            LastReactionColor = color;
            ReactionNumberCount++;
        }

        public static string GetReactionLocalizationKey(
            ElementReactionType reaction)
        {
            switch (reaction)
            {
                case ElementReactionType.Melt:
                    return "reaction_name_melt";
                case ElementReactionType.BurnBurst:
                    return "reaction_name_burn_burst";
                case ElementReactionType.Shock:
                    return "reaction_name_shock";
                case ElementReactionType.Spread:
                    return "reaction_name_spread";
                case ElementReactionType.Sever:
                    return "reaction_name_sever";
                default:
                    return string.Empty;
            }
        }

        public static string GetReactionDefaultValue(
            ElementReactionType reaction)
        {
            switch (reaction)
            {
                case ElementReactionType.Melt:
                    return "融化";
                case ElementReactionType.BurnBurst:
                    return "燃爆";
                case ElementReactionType.Shock:
                    return "感电";
                case ElementReactionType.Spread:
                    return "扩散";
                case ElementReactionType.Sever:
                    return "断灵";
                default:
                    return string.Empty;
            }
        }

        public static Color GetReactionColor(ElementReactionType reaction)
        {
            switch (reaction)
            {
                case ElementReactionType.Melt:
                    return new Color(0.45f, 0.85f, 1f, 1f);
                case ElementReactionType.BurnBurst:
                    return new Color(1f, 0.36f, 0.12f, 1f);
                case ElementReactionType.Shock:
                    return new Color(0.82f, 0.68f, 1f, 1f);
                case ElementReactionType.Spread:
                    return new Color(0.42f, 1f, 0.62f, 1f);
                case ElementReactionType.Sever:
                    return new Color(1f, 0.88f, 0.38f, 1f);
                default:
                    return Color.white;
            }
        }

        private void SpawnFloatingText(
            string objectName,
            string renderedText,
            int fontSize,
            Color color,
            Vector3 worldPosition)
        {
            if (_entries.Count >= MaximumVisibleNumbers)
            {
                DamageNumberEntry oldest = _entries[0];
                if (oldest.Label != null)
                {
                    Destroy(oldest.Label.gameObject);
                }

                _entries.RemoveAt(0);
            }

            Text label = RuntimeUiFactory.CreateText(
                _canvas.transform,
                objectName,
                renderedText,
                fontSize,
                color,
                new Vector2(220f, 64f),
                Vector2.zero);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;

            var entry = new DamageNumberEntry
            {
                Label = label,
                Rect = label.rectTransform,
                WorldPosition = worldPosition,
                Age = 0f,
                BaseColor = label.color
            };
            _entries.Add(entry);
            UpdateEntryPose(entry, UnityEngine.Camera.main);
        }

        private void UpdateEntryPose(
            DamageNumberEntry entry,
            UnityEngine.Camera worldCamera)
        {
            Vector2 localPoint = Vector2.zero;
            bool visible = worldCamera == null;
            if (worldCamera != null)
            {
                Vector3 screenPoint = worldCamera.WorldToScreenPoint(entry.WorldPosition);
                visible = screenPoint.z > 0f
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRect,
                        screenPoint,
                        null,
                        out localPoint);
            }

            entry.Rect.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            entry.Rect.anchoredPosition = localPoint
                + Vector2.up * (entry.Age * VerticalSpeed);
            Color color = entry.BaseColor;
            color.a = 1f - entry.Age / Lifetime;
            entry.Label.color = color;
        }

        private sealed class DamageNumberEntry
        {
            public Text Label;
            public RectTransform Rect;
            public Vector3 WorldPosition;
            public float Age;
            public Color BaseColor;
        }
    }
}
