using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.Crafting
{
    public sealed class AlchemyFurnaceInteractable : MonoBehaviour
    {
        public const float InteractionDistance = 3.2f;
        public const string InteractionPromptLocalizationKey =
            "ui_alchemy_furnace_interact";
        public const string InteractionPromptDefaultValue =
            "[E] 使用{0}";
        public const string DefaultDisplayName = "青铜丹炉";

        private Component _player;
        private IPlayerInputSource _input;
        private TextMesh _prompt;
        private string _displayName = DefaultDisplayName;

        public string FurnaceId { get; private set; } = string.Empty;
        public string DisplayNameLocalizationKey { get; private set; } =
            string.Empty;
        public string DisplayName => _displayName;
        public bool IsPlayerInRange { get; private set; }
        public bool CanInteract { get; private set; }
        public int InteractionCount { get; private set; }
        public string CurrentPromptText => _prompt?.text ?? string.Empty;

        private void Awake()
        {
            BuildPrompt();
            RefreshPrompt();
            SetPromptVisible(false);
        }

        private void Update()
        {
            ResolveRuntimeReferences();
            UpdateRange();
            CanInteract = IsPlayerInRange
                && IsInputAlive()
                && _input.IsEnabled
                && (GameManager.Instance == null
                    || GameManager.Instance.State == GameState.Playing);
            SetPromptVisible(CanInteract);
            FacePromptToCamera();

            if (CanInteract && _input.InteractPressedThisFrame)
            {
                TryInteract();
            }
        }

        public void Configure(
            string furnaceId,
            string displayNameLocalizationKey,
            string displayName)
        {
            FurnaceId = furnaceId ?? string.Empty;
            DisplayNameLocalizationKey =
                displayNameLocalizationKey ?? string.Empty;
            _displayName = string.IsNullOrWhiteSpace(displayName)
                ? DefaultDisplayName
                : displayName;
            RefreshPrompt();
        }

        public bool TryInteract()
        {
            ResolveRuntimeReferences();
            UpdateRange();
            bool inputAlive = IsInputAlive();
            if (string.IsNullOrWhiteSpace(FurnaceId)
                || !IsPlayerInRange
                || !inputAlive
                || !_input.IsEnabled
                || (GameManager.Instance != null
                    && GameManager.Instance.State != GameState.Playing))
            {
                return false;
            }

            InteractionCount++;
            EventBus.Publish(
                AlchemyEvents.FurnaceInteracted,
                new AlchemyFurnaceInfo
                {
                    FurnaceId = FurnaceId
                });
            return true;
        }

        private void ResolveRuntimeReferences()
        {
            if (_player == null
                && ServiceLocator.TryGet<IPlayerHealthService>(
                    out IPlayerHealthService health)
                && health is Component component)
            {
                _player = component;
            }

            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
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
                && (!(_input is Object unityObject) || unityObject != null);
        }

        private void BuildPrompt()
        {
            var promptObject = new GameObject("AlchemyFurnacePrompt");
            promptObject.transform.SetParent(transform, false);
            promptObject.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            _prompt = promptObject.AddComponent<TextMesh>();
            _prompt.anchor = TextAnchor.LowerCenter;
            _prompt.alignment = TextAlignment.Center;
            _prompt.fontSize = 48;
            _prompt.characterSize = 0.045f;
            _prompt.color = new Color(0.95f, 0.76f, 0.36f, 1f);
        }

        private void RefreshPrompt()
        {
            if (_prompt != null)
            {
                _prompt.text = string.Format(
                    InteractionPromptDefaultValue,
                    _displayName);
            }
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
    }
}
