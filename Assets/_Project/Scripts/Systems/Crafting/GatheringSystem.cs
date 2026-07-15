using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.Crafting
{
    public sealed class GatheringSystem : SafeBehaviour, IGatheringService
    {
        public const float GatherDurationSeconds = 1.5f;
        public const string SuccessToastKey = "ui_gather_success";
        public const string SuccessToastDefault = "采得{0} ×{1}";
        public const string InterruptedToastKey = "ui_gather_interrupted";
        public const string InterruptedToastDefault = "受击打断了采集。";
        public const string UnavailableToastKey = "ui_gather_unavailable";
        public const string UnavailableToastDefault =
            "此处暂不可采集，请检查等级、工具与背包。";

        private Func<int, int, int> _countProvider;
        private IPlayerInputSource _input;
        private bool _inputWasEnabled;
        private bool _registeredService;
        private float _elapsed;

        public int Level { get; private set; } = 1;
        public bool IsGathering => !ReferenceEquals(ActiveGatherable, null);
        public GatherableObject ActiveGatherable { get; private set; }
        public float Progress01 => IsGathering
            ? Mathf.Clamp01(_elapsed / GatherDurationSeconds)
            : 0f;
        public float RemainingSeconds => IsGathering
            ? Mathf.Max(0f, GatherDurationSeconds - _elapsed)
            : 0f;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IGatheringService>(
                    out IGatheringService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _countProvider = (minimum, maximumExclusive) =>
                UnityEngine.Random.Range(minimum, maximumExclusive);
            ServiceLocator.Register<IGatheringService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.PlayerDamaged,
                HandlePlayerDamaged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.PlayerDamaged,
                HandlePlayerDamaged);
            CancelInternal(false);
        }

        private void Update()
        {
            RepairServiceRegistration();
            TickGathering(Time.deltaTime);
        }

        private void OnDestroy()
        {
            CancelInternal(false);
            if (_registeredService
                && ServiceLocator.TryGet<IGatheringService>(
                    out IGatheringService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IGatheringService>();
            }

            _registeredService = false;
        }

        public bool CanGather(GatherableObject gatherable)
        {
            if (gatherable == null
                || IsGathering
                || !gatherable.IsAvailable
                || gatherable.IsReserved
                || Level < gatherable.RequiredLevel
                || ConfigDatabase.Instance?.GetItem(gatherable.ItemId) == null
                || !IsGameplayRunning()
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory)
                || !inventory.CanAdd(
                    gatherable.ItemId,
                    gatherable.MaxCount))
            {
                return false;
            }

            return string.IsNullOrEmpty(gatherable.RequiredToolItemId)
                || inventory.CountItem(gatherable.RequiredToolItemId) > 0;
        }

        public bool Gather(GatherableObject gatherable)
        {
            if (!CanGather(gatherable) || !gatherable.TryReserve())
            {
                PublishToast(UnavailableToastKey, UnavailableToastDefault);
                return false;
            }

            ActiveGatherable = gatherable;
            _elapsed = 0f;
            ResolveInput();
            bool inputAlive = IsInputAlive();
            _inputWasEnabled = inputAlive && _input.IsEnabled;
            if (inputAlive)
            {
                _input.SetEnabled(false);
            }

            return true;
        }

        public bool CancelActiveGather()
        {
            return CancelInternal(false);
        }

        public void TickGathering(float deltaTime)
        {
            if (!IsGathering)
            {
                return;
            }

            if (ActiveGatherable == null
                || !ActiveGatherable.IsReserved)
            {
                CancelInternal(false);
                return;
            }

            _elapsed += Mathf.Max(0f, deltaTime);
            if (_elapsed + 0.0001f < GatherDurationSeconds)
            {
                return;
            }

            CompleteGather();
        }

        public void SetCountProvider(Func<int, int, int> provider)
        {
            _countProvider = provider ?? ((minimum, maximumExclusive) =>
                UnityEngine.Random.Range(minimum, maximumExclusive));
        }

        private void CompleteGather()
        {
            GatherableObject gatherable = ActiveGatherable;
            if (gatherable == null
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                CancelInternal(false);
                return;
            }

            int count = Mathf.Clamp(
                (_countProvider ?? ((minimum, maximumExclusive) =>
                    UnityEngine.Random.Range(minimum, maximumExclusive))).Invoke(
                        gatherable.MinCount,
                        gatherable.MaxCount + 1),
                gatherable.MinCount,
                gatherable.MaxCount);
            if (!inventory.AddItem(
                    gatherable.ItemId,
                    count,
                    AcquireSource.Gather))
            {
                CancelInternal(false);
                PublishToast(UnavailableToastKey, UnavailableToastDefault);
                return;
            }

            gatherable.CompleteGather();
            ItemData item = ConfigDatabase.Instance?.GetItem(gatherable.ItemId);
            ActiveGatherable = null;
            _elapsed = 0f;
            RestoreGameplayInput();
            PersistInventory();
            PublishToast(
                SuccessToastKey,
                string.Format(
                    SuccessToastDefault,
                    item?.DisplayName ?? gatherable.ItemId,
                    count));
        }

        private void HandlePlayerDamaged(DamageInfo info)
        {
            if (!IsGathering || info.Amount <= 0f)
            {
                return;
            }

            if (CancelInternal(false))
            {
                PublishToast(InterruptedToastKey, InterruptedToastDefault);
            }
        }

        private bool CancelInternal(bool publishUnavailable)
        {
            GatherableObject gatherable = ActiveGatherable;
            if (ReferenceEquals(gatherable, null))
            {
                return false;
            }

            if (gatherable != null)
            {
                gatherable.CancelReservation();
            }
            ActiveGatherable = null;
            _elapsed = 0f;
            RestoreGameplayInput();
            if (publishUnavailable)
            {
                PublishToast(UnavailableToastKey, UnavailableToastDefault);
            }

            return true;
        }

        private void ResolveInput()
        {
            if (!IsInputAlive())
            {
                _input = null;
                ServiceLocator.TryGet(out _input);
            }
        }

        private void RestoreGameplayInput()
        {
            ResolveInput();
            GameManager gameManager = GameManager.Instance;
            bool gameAllowsInput = gameManager == null
                || gameManager.State == GameState.Playing;
            if (IsInputAlive())
            {
                _input.SetEnabled(_inputWasEnabled && gameAllowsInput);
            }
        }

        private bool IsInputAlive()
        {
            return _input != null
                && (!(_input is UnityEngine.Object unityObject)
                    || unityObject != null);
        }

        private static bool IsGameplayRunning()
        {
            GameManager gameManager = GameManager.Instance;
            return gameManager == null || gameManager.State == GameState.Playing;
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IGatheringService>(
                    out IGatheringService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IGatheringService>(this);
            _registeredService = true;
        }

        private static void PersistInventory()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule(InventoryManager.SaveModuleName);
            }
        }

        private static void PublishToast(string key, string defaultValue)
        {
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = key,
                    DefaultValue = defaultValue,
                    Duration = 2.5f
                });
        }
    }
}
