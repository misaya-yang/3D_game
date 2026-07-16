using UnityEngine;
using UnityEngine.InputSystem;
using Wendao.Core;
using Wendao.Systems.Input;
using Wendao.Systems.Tutorial;

namespace Wendao.Entities.Player
{
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInputSource
    {
        public const string DefaultActionAssetResourcePath = "Input/PlayerInputActions";
        public const string PlayerActionMapName = "Player";

        [SerializeField] private string _actionAssetResourcePath =
            DefaultActionAssetResourcePath;

        private InputActionAsset _runtimeAsset;
        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _lightAttackAction;
        private InputAction _heavyAttackAction;
        private InputAction _dodgeAction;
        private InputAction _blockAction;
        private InputAction _lockOnAction;
        private InputAction _skill1Action;
        private InputAction _skill2Action;
        private InputAction _skill3Action;
        private InputAction _skill4Action;
        private InputAction _interactAction;
        private InputAction _openInventoryAction;
        private InputAction _openCharacterAction;
        private InputAction _openSkillAction;
        private InputAction _openQuestAction;
        private InputAction _openMapAction;
        private InputAction _pauseAction;
        private InputAction _mountAction;
        private bool _registered;

        public Vector2 Move => CanRead(_moveAction, TutorialInputAction.Move)
            ? _moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        public Vector2 Look => CanRead(_lookAction, TutorialInputAction.Look)
            ? _lookAction.ReadValue<Vector2>()
            : Vector2.zero;

        public bool LookIsPointerDelta => CanRead(
                _lookAction,
                TutorialInputAction.Look)
            && _lookAction.activeControl?.device is Pointer;

        public bool JumpPressedThisFrame => CanRead(
                _jumpAction,
                TutorialInputAction.Jump)
            && _jumpAction.WasPressedThisFrame();

        public bool JumpHeld => CanRead(
                _jumpAction,
                TutorialInputAction.Jump)
            && _jumpAction.IsPressed();

        public bool SprintHeld => CanRead(
                _sprintAction,
                TutorialInputAction.Sprint)
            && _sprintAction.IsPressed();

        public bool LightAttackPressedThisFrame => CanRead(
                _lightAttackAction,
                TutorialInputAction.LightAttack)
            && _lightAttackAction.WasPressedThisFrame();

        public bool HeavyAttackPressedThisFrame => CanRead(
                _heavyAttackAction,
                TutorialInputAction.HeavyAttack)
            && _heavyAttackAction.WasPressedThisFrame();

        public bool DodgePressedThisFrame => CanRead(
                _dodgeAction,
                TutorialInputAction.Dodge)
            && _dodgeAction.WasPressedThisFrame();

        public bool BlockHeld => CanRead(
                _blockAction,
                TutorialInputAction.Block)
            && _blockAction.IsPressed();

        public bool LockOnPressedThisFrame => CanRead(
                _lockOnAction,
                TutorialInputAction.LockOn)
            && _lockOnAction.WasPressedThisFrame();

        public bool Skill1PressedThisFrame => CanRead(
                _skill1Action,
                TutorialInputAction.Skill1)
            && _skill1Action.WasPressedThisFrame();

        public bool Skill2PressedThisFrame => CanRead(
                _skill2Action,
                TutorialInputAction.Skill2)
            && _skill2Action.WasPressedThisFrame();

        public bool Skill3PressedThisFrame => CanRead(
                _skill3Action,
                TutorialInputAction.Skill3)
            && _skill3Action.WasPressedThisFrame();

        public bool Skill4PressedThisFrame => CanRead(
                _skill4Action,
                TutorialInputAction.Skill4)
            && _skill4Action.WasPressedThisFrame();

        public bool InteractPressedThisFrame => CanReadUi(_interactAction)
            && _interactAction.WasPressedThisFrame();

        public bool OpenInventoryPressedThisFrame => CanReadUi(_openInventoryAction)
            && _openInventoryAction.WasPressedThisFrame();

        public bool OpenCharacterPressedThisFrame => CanReadUi(
                _openCharacterAction,
                TutorialInputAction.OpenCharacter)
            && _openCharacterAction.WasPressedThisFrame();

        public bool OpenSkillPressedThisFrame => CanReadUi(
                _openSkillAction,
                TutorialInputAction.OpenSkill)
            && _openSkillAction.WasPressedThisFrame();

        public bool OpenQuestPressedThisFrame => CanReadUi(
                _openQuestAction,
                TutorialInputAction.OpenQuest)
            && _openQuestAction.WasPressedThisFrame();

        public bool OpenMapPressedThisFrame => CanReadUi(
                _openMapAction,
                TutorialInputAction.OpenMap)
            && _openMapAction.WasPressedThisFrame();

        public bool PausePressedThisFrame => CanReadUi(
                _pauseAction,
                TutorialInputAction.Pause)
            && _pauseAction.WasPressedThisFrame();

        public bool MountPressedThisFrame => CanRead(
                _mountAction,
                TutorialInputAction.Mount)
            && _mountAction.WasPressedThisFrame();

        public bool IsEnabled { get; private set; } = true;

        private void Awake()
        {
            InputActionAsset template = Resources.Load<InputActionAsset>(
                _actionAssetResourcePath);
            if (template == null)
            {
                Debug.LogError(
                    $"InputActionAsset was not found at Resources/{_actionAssetResourcePath}.",
                    this);
                enabled = false;
                return;
            }

            _runtimeAsset = Instantiate(template);
            _runtimeAsset.name = template.name + " (Runtime)";
            _playerMap = _runtimeAsset.FindActionMap(PlayerActionMapName, true);
            _moveAction = _playerMap.FindAction("Move", true);
            _lookAction = _playerMap.FindAction("Look", true);
            _jumpAction = _playerMap.FindAction("Jump", true);
            _sprintAction = _playerMap.FindAction("Sprint", true);
            _lightAttackAction = _playerMap.FindAction("LightAttack", true);
            _heavyAttackAction = _playerMap.FindAction("HeavyAttack", true);
            _dodgeAction = _playerMap.FindAction("Dodge", true);
            _blockAction = _playerMap.FindAction("Block", true);
            _lockOnAction = _playerMap.FindAction("LockOn", true);
            _skill1Action = _playerMap.FindAction("Skill1", true);
            _skill2Action = _playerMap.FindAction("Skill2", true);
            _skill3Action = _playerMap.FindAction("Skill3", true);
            _skill4Action = _playerMap.FindAction("Skill4", true);
            _interactAction = _playerMap.FindAction("Interact", true);
            _openInventoryAction = _playerMap.FindAction("OpenInventory", true);
            _openCharacterAction = _playerMap.FindAction("OpenCharacter", true);
            _openSkillAction = _playerMap.FindAction("OpenSkill", true);
            _openQuestAction = _playerMap.FindAction("OpenQuest", true);
            _openMapAction = _playerMap.FindAction("OpenMap", true);
            _pauseAction = _playerMap.FindAction("Pause", true);
            _mountAction = _playerMap.FindAction("Mount", true);

            if (ServiceLocator.TryGet<IPlayerInputSource>(out IPlayerInputSource existing)
                && !ReferenceEquals(existing, this))
            {
                Debug.LogError("A player input source is already registered.", this);
                enabled = false;
                return;
            }

            ServiceLocator.Register<IPlayerInputSource>(this);
            _registered = true;
        }

        private void OnEnable()
        {
            _playerMap?.Enable();

            SetCursorCaptured(IsEnabled);
        }

        private void OnDisable()
        {
            _playerMap?.Disable();
            SetCursorCaptured(false);
        }

        private void OnDestroy()
        {
            if (_registered
                && ServiceLocator.TryGet<IPlayerInputSource>(out IPlayerInputSource current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IPlayerInputSource>();
            }

            _registered = false;
            if (_runtimeAsset != null)
            {
                Destroy(_runtimeAsset);
            }
        }

        public void SetEnabled(bool enabled)
        {
            // Service consumers can briefly retain this interface while a scene
            // unload destroys the component. Unity's overloaded null check is
            // still available here and prevents native Behaviour access from
            // throwing during shutdown.
            if (this == null)
            {
                return;
            }

            IsEnabled = enabled;
            if (_playerMap != null && isActiveAndEnabled)
            {
                _playerMap.Enable();
            }

            SetCursorCaptured(enabled);
        }

        private bool CanRead(
            InputAction action,
            TutorialInputAction tutorialAction)
        {
            return this != null
                && IsEnabled
                && isActiveAndEnabled
                && action != null
                && action.enabled
                && IsAllowedByTutorial(tutorialAction);
        }

        private bool CanReadUi(
            InputAction action,
            TutorialInputAction tutorialAction)
        {
            return this != null
                && isActiveAndEnabled
                && action != null
                && action.enabled
                && IsAllowedByTutorial(tutorialAction);
        }

        private bool CanReadUi(InputAction action)
        {
            TutorialInputAction tutorialAction = ReferenceEquals(
                    action,
                    _interactAction)
                ? TutorialInputAction.Interact
                : TutorialInputAction.OpenInventory;
            return CanReadUi(action, tutorialAction);
        }

        private static bool IsAllowedByTutorial(TutorialInputAction action)
        {
            return !ServiceLocator.TryGet<ITutorialService>(
                    out ITutorialService tutorial)
                || tutorial.AllowsInput(action);
        }

        private static void SetCursorCaptured(bool captured)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
