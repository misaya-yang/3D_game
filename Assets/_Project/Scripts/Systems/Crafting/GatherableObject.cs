using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Input;

namespace Wendao.Systems.Crafting
{
    public sealed class GatherableObject : MonoBehaviour
    {
        private const float RespawnEpsilon = 0.0001f;

        public const float InteractionDistance = 3f;
        public const string InteractionPromptLocalizationKey =
            "ui_gather_interact";
        public const string InteractionPromptDefaultValue = "[E] 采集{0}";
        public const string ProgressLocalizationKey = "ui_gather_progress";
        public const string ProgressDefaultValue = "采集中：{0} {1:P0}";

        [SerializeField] private string _nodeId = string.Empty;
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField, Min(1)] private int _minCount = 1;
        [SerializeField, Min(1)] private int _maxCount = 1;
        [SerializeField, Min(1)] private int _requiredLevel = 1;
        [SerializeField, Min(0.01f)] private float _respawnSeconds = 30f;
        [SerializeField] private string _requiredToolItemId = string.Empty;

        private Component _player;
        private IPlayerInputSource _input;
        private IGatheringService _gathering;
        private Renderer _visualRenderer;
        private Collider _interactionCollider;
        private TextMesh _prompt;

        public string NodeId => _nodeId;
        public string ItemId => _itemId;
        public int MinCount => _minCount;
        public int MaxCount => _maxCount;
        public int RequiredLevel => _requiredLevel;
        public float RespawnSeconds => _respawnSeconds;
        public string RequiredToolItemId => _requiredToolItemId;
        public bool IsAvailable { get; private set; } = true;
        public bool IsReserved { get; private set; }
        public float RespawnRemaining { get; private set; }
        public bool IsPlayerInRange { get; private set; }
        public string CurrentPromptLocalizationKey { get; private set; } =
            InteractionPromptLocalizationKey;
        public string CurrentPromptText => _prompt?.text ?? string.Empty;

        private void Awake()
        {
            _visualRenderer = GetComponent<Renderer>();
            _interactionCollider = GetComponent<Collider>();
            BuildPrompt();
            RefreshInteractionPrompt();
            SetPromptVisible(false);
        }

        private void Update()
        {
            TickRespawn(Time.deltaTime);
            ResolveRuntimeReferences();
            UpdateRange();

            if (IsReserved
                && IsGatheringServiceAlive()
                && ReferenceEquals(_gathering.ActiveGatherable, this))
            {
                CurrentPromptLocalizationKey = ProgressLocalizationKey;
                _prompt.text = string.Format(
                    ProgressDefaultValue,
                    ResolveItemName(),
                    _gathering.Progress01);
                SetPromptVisible(true);
                FacePromptToCamera();
                return;
            }

            CurrentPromptLocalizationKey = InteractionPromptLocalizationKey;
            RefreshInteractionPrompt();
            bool inputAlive = IsInputAlive();
            bool canPrompt = IsAvailable
                && !IsReserved
                && IsPlayerInRange
                && inputAlive
                && _input.IsEnabled
                && IsGatheringServiceAlive()
                && !_gathering.IsGathering;
            SetPromptVisible(canPrompt);
            FacePromptToCamera();

            if (canPrompt && _input.InteractPressedThisFrame)
            {
                TryInteract();
            }
        }

        public void Configure(
            string nodeId,
            string itemId,
            int minCount,
            int maxCount,
            int requiredLevel,
            float respawnSeconds,
            string requiredToolItemId = "")
        {
            _nodeId = nodeId ?? string.Empty;
            _itemId = itemId ?? string.Empty;
            _minCount = Mathf.Max(1, minCount);
            _maxCount = Mathf.Max(_minCount, maxCount);
            _requiredLevel = Mathf.Max(1, requiredLevel);
            _respawnSeconds = Mathf.Max(0.01f, respawnSeconds);
            _requiredToolItemId = requiredToolItemId ?? string.Empty;
            RefreshInteractionPrompt();
        }

        public bool TryInteract()
        {
            ResolveRuntimeReferences();
            UpdateRange();
            if (!IsAvailable
                || IsReserved
                || !IsPlayerInRange
                || !IsGatheringServiceAlive())
            {
                return false;
            }

            return _gathering.Gather(this);
        }

        public void TickRespawn(float deltaTime)
        {
            if (IsAvailable || IsReserved || RespawnRemaining <= 0f)
            {
                return;
            }

            RespawnRemaining = Mathf.Max(
                0f,
                RespawnRemaining - Mathf.Max(0f, deltaTime));
            if (RespawnRemaining <= RespawnEpsilon)
            {
                RespawnRemaining = 0f;
                IsAvailable = true;
                SetAvailabilityVisual(true);
            }
        }

        internal bool TryReserve()
        {
            if (!IsAvailable || IsReserved)
            {
                return false;
            }

            IsReserved = true;
            return true;
        }

        internal void CompleteGather()
        {
            IsReserved = false;
            IsAvailable = false;
            RespawnRemaining = _respawnSeconds;
            SetPromptVisible(false);
            SetAvailabilityVisual(false);
        }

        internal void CancelReservation()
        {
            IsReserved = false;
            SetAvailabilityVisual(IsAvailable);
        }

        private void ResolveRuntimeReferences()
        {
            if (_player == null)
            {
                if (ServiceLocator.TryGet<Wendao.Systems.Inventory.IPlayerHealthService>(
                        out Wendao.Systems.Inventory.IPlayerHealthService health)
                    && health is Component component)
                {
                    _player = component;
                }
            }

            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
            }

            if (!IsGatheringServiceAlive())
            {
                _gathering = null;
                ServiceLocator.TryGet(out _gathering);
            }
        }

        private void UpdateRange()
        {
            IsPlayerInRange = _player != null
                && Vector3.SqrMagnitude(
                    _player.transform.position - transform.position)
                    <= InteractionDistance * InteractionDistance;
        }

        private bool IsInputAlive()
        {
            return _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private bool IsGatheringServiceAlive()
        {
            return _gathering != null
                && (!(_gathering is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private void BuildPrompt()
        {
            var promptObject = new GameObject("GatherInteractionPrompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            _prompt = promptObject.AddComponent<TextMesh>();
            _prompt.anchor = TextAnchor.LowerCenter;
            _prompt.alignment = TextAlignment.Center;
            _prompt.fontSize = 48;
            _prompt.characterSize = 0.045f;
            _prompt.color = new Color(0.75f, 0.95f, 0.67f, 1f);
        }

        private void RefreshInteractionPrompt()
        {
            if (_prompt != null)
            {
                _prompt.text = string.Format(
                    InteractionPromptDefaultValue,
                    ResolveItemName());
            }
        }

        private string ResolveItemName()
        {
            ItemData item = ConfigDatabase.Instance?.GetItem(_itemId);
            return item?.DisplayName ?? _itemId;
        }

        private void SetPromptVisible(bool visible)
        {
            if (_prompt != null && _prompt.gameObject.activeSelf != visible)
            {
                _prompt.gameObject.SetActive(visible);
            }
        }

        private void FacePromptToCamera()
        {
            Camera camera = Camera.main;
            if (_prompt == null || camera == null)
            {
                return;
            }

            Vector3 forward = _prompt.transform.position
                - camera.transform.position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                _prompt.transform.rotation = Quaternion.LookRotation(forward);
            }
        }

        private void SetAvailabilityVisual(bool available)
        {
            if (_visualRenderer != null)
            {
                _visualRenderer.enabled = available;
            }

            if (_interactionCollider != null)
            {
                _interactionCollider.enabled = available;
            }
        }
    }
}
