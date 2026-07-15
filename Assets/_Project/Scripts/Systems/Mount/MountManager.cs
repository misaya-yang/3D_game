using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Input;
using Wendao.Systems.Player;
using Wendao.Systems.Tutorial;
using Wendao.Systems.World;

namespace Wendao.Systems.Mount
{
    [DefaultExecutionOrder(60)]
    public sealed class MountManager : SafeBehaviour, IMountService
    {
        public const string SaveModuleName = "mounts";
        public const float SpiritHorseSpeedMultiplier = 1.5f;
        public const float FlyingSwordGroundSpeedMultiplier = 1f;
        public const float MaximumFlightHeight = 40f;

        private readonly List<string> _unlockedMountIds = new List<string>();
        private readonly HashSet<string> _noFlyZones =
            new HashSet<string>(StringComparer.Ordinal);

        private ReadOnlyCollection<string> _readOnlyUnlockedMountIds;
        private IPlayerInputSource _input;
        private IPlayerMountLocomotion _locomotion;
        private SaveManager _registeredSaveManager;
        private GameObject _mountVisual;
        private GameObject _visualActor;
        private bool _registeredService;
        private bool _registeredSaveModule;

        public bool IsMounted { get; private set; }
        public bool IsFlying { get; private set; }
        public string ActiveMountId { get; private set; } = string.Empty;
        public string SelectedMountId { get; private set; } =
            MountContentIds.SpiritHorse;
        public bool IsFlightAllowedInCurrentMap => IsKnownMapFlightAllowed()
            && _noFlyZones.Count == 0;
        public IReadOnlyList<string> UnlockedMountIds =>
            _readOnlyUnlockedMountIds;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IMountService>(out IMountService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _readOnlyUnlockedMountIds = _unlockedMountIds.AsReadOnly();
            ResetMounts();
            ServiceLocator.Register<IMountService>(this);
            _registeredService = true;
            TryRegisterSaveModule();

            EventBus.Subscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Subscribe<MapInfo>(
                SceneLoader.MapLoadedEvent,
                HandleMapLoaded);

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        protected override void SafeStart()
        {
            ResolveRuntimeServices();
            RefreshRealmUnlocks(false);
        }

        private void Update()
        {
            RepairServiceRegistration();
            RepairSaveRegistration();
            ResolveRuntimeServices();
            RefreshRealmUnlocks(true);
            RepairLocomotionBinding();

            if (_input == null
                || !_input.IsEnabled
                || !_input.MountPressedThisFrame
                || !IsGameplayRunning())
            {
                return;
            }

            ToggleSelectedMount();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<RealmChangeInfo>(
                CultivationEvents.RealmBreakthrough,
                HandleRealmBreakthrough);
            EventBus.Unsubscribe<MapInfo>(
                SceneLoader.MapLoadedEvent,
                HandleMapLoaded);

            DestroyMountVisual();
            if (_registeredSaveModule && _registeredSaveManager != null)
            {
                _registeredSaveManager.UnregisterModule(SaveModuleName);
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            if (_registeredService
                && ServiceLocator.TryGet<IMountService>(out IMountService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IMountService>();
            }

            _registeredService = false;
        }

        public bool IsUnlocked(string mountId)
        {
            return !string.IsNullOrEmpty(mountId)
                && _unlockedMountIds.Contains(mountId);
        }

        public bool TryMount(string mountId)
        {
            RefreshRealmUnlocks(true);
            ResolveRuntimeServices();
            if (!IsSupportedMount(mountId)
                || !IsUnlocked(mountId)
                || _locomotion == null
                || !_locomotion.CanChangeMountState
                || !IsGameplayRunning())
            {
                if (string.Equals(
                        mountId,
                        MountContentIds.FlyingSword,
                        StringComparison.Ordinal)
                    && !HasReachedFoundation())
                {
                    PublishToast(
                        MountContentIds.RealmBlockedToastKey,
                        MountContentIds.RealmBlockedToastDefault);
                }

                return false;
            }

            if (IsMounted)
            {
                if (string.Equals(ActiveMountId, mountId, StringComparison.Ordinal))
                {
                    return true;
                }

                Dismount();
            }

            IsMounted = true;
            IsFlying = false;
            ActiveMountId = mountId;
            SelectedMountId = mountId;
            _locomotion.SetMountedState(
                true,
                GetGroundSpeedMultiplier(mountId));
            EnsureMountVisual();
            EventBus.Publish(
                MountEvents.MountChanged,
                new MountInfo
                {
                    MountId = mountId,
                    Mounted = true
                });
            if (string.Equals(
                    mountId,
                    MountContentIds.SpiritHorse,
                    StringComparison.Ordinal))
            {
                PublishToast(
                    MountContentIds.HorseMountedToastKey,
                    MountContentIds.HorseMountedToastDefault);
            }

            PersistChanges();
            return true;
        }

        public void Dismount()
        {
            if (!IsMounted)
            {
                return;
            }

            string previousMountId = ActiveMountId;
            if (IsFlying)
            {
                Land();
            }

            ResolveRuntimeServices();
            _locomotion?.SetMountedState(false, 1f);
            IsMounted = false;
            ActiveMountId = string.Empty;
            DestroyMountVisual();
            EventBus.Publish(
                MountEvents.MountChanged,
                new MountInfo
                {
                    MountId = previousMountId,
                    Mounted = false
                });
            PublishToast(
                MountContentIds.DismountedToastKey,
                MountContentIds.DismountedToastDefault);
        }

        public bool TryTakeOff()
        {
            ResolveRuntimeServices();
            if (!IsMounted
                || IsFlying
                || !string.Equals(
                    ActiveMountId,
                    MountContentIds.FlyingSword,
                    StringComparison.Ordinal)
                || !HasReachedFoundation()
                || _locomotion == null
                || !_locomotion.IsGrounded)
            {
                return false;
            }

            if (!IsFlightAllowedInCurrentMap)
            {
                PublishToast(
                    MountContentIds.FlightBlockedToastKey,
                    MountContentIds.FlightBlockedToastDefault);
                return false;
            }

            if (!_locomotion.SetFlyingState(true, MaximumFlightHeight))
            {
                return false;
            }

            IsFlying = true;
            EventBus.Publish(
                MountEvents.FlightStateChanged,
                new FlightInfo { IsFlying = true });
            PublishToast(
                MountContentIds.FlightStartedToastKey,
                MountContentIds.FlightStartedToastDefault);
            if (ServiceLocator.TryGet<ITutorialService>(out ITutorialService tutorial))
            {
                tutorial.RequestStart(TutorialManager.FlightTutorialId);
            }

            return true;
        }

        public void Land()
        {
            if (!IsFlying)
            {
                return;
            }

            ResolveRuntimeServices();
            _locomotion?.SetFlyingState(false, MaximumFlightHeight);
            IsFlying = false;
            EventBus.Publish(
                MountEvents.FlightStateChanged,
                new FlightInfo { IsFlying = false });

            if (ServiceLocator.TryGet<ITutorialService>(out ITutorialService tutorial)
                && tutorial.IsActive
                && string.Equals(
                    tutorial.ActiveTutorialId,
                    TutorialManager.FlightTutorialId,
                    StringComparison.Ordinal))
            {
                tutorial.Complete(TutorialManager.FlightTutorialId);
            }
        }

        public void SetNoFlyZoneActive(string zoneId, bool active)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                return;
            }

            if (active)
            {
                _noFlyZones.Add(zoneId);
                if (IsFlying)
                {
                    Land();
                    PublishToast(
                        MountContentIds.FlightBlockedToastKey,
                        MountContentIds.FlightBlockedToastDefault);
                }

                return;
            }

            _noFlyZones.Remove(zoneId);
        }

        public MountSaveData CaptureSaveData()
        {
            return new MountSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                UnlockedMountIds = new List<string>(_unlockedMountIds),
                SelectedMountId = SelectedMountId ?? string.Empty
            };
        }

        public void RestoreSaveData(MountSaveData data)
        {
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || data.UnlockedMountIds == null)
            {
                throw new InvalidDataException("Mount save data is invalid.");
            }

            var restored = new List<string>(data.UnlockedMountIds.Count + 1);
            for (int index = 0; index < data.UnlockedMountIds.Count; index++)
            {
                string mountId = data.UnlockedMountIds[index];
                if (!IsSupportedMount(mountId) || restored.Contains(mountId))
                {
                    throw new InvalidDataException(
                        "Mount save contains an invalid mount id.");
                }

                restored.Add(mountId);
            }

            if (!restored.Contains(MountContentIds.SpiritHorse))
            {
                restored.Insert(0, MountContentIds.SpiritHorse);
            }

            string selectedMountId = data.SelectedMountId ?? string.Empty;
            if (string.IsNullOrEmpty(selectedMountId))
            {
                selectedMountId = MountContentIds.SpiritHorse;
            }

            if (!restored.Contains(selectedMountId))
            {
                throw new InvalidDataException(
                    "Mount save selected an unavailable mount.");
            }

            ForceRuntimeDismount();
            _unlockedMountIds.Clear();
            _unlockedMountIds.AddRange(restored);
            SelectedMountId = selectedMountId;
            RefreshRealmUnlocks(false);
        }

        public void ResetMounts()
        {
            ForceRuntimeDismount();
            _unlockedMountIds.Clear();
            _unlockedMountIds.Add(MountContentIds.SpiritHorse);
            SelectedMountId = MountContentIds.SpiritHorse;
            _noFlyZones.Clear();
        }

        public bool RefreshRealmUnlocks(bool notify)
        {
            if (!HasReachedFoundation()
                || _unlockedMountIds.Contains(MountContentIds.FlyingSword))
            {
                return false;
            }

            _unlockedMountIds.Add(MountContentIds.FlyingSword);
            SelectedMountId = MountContentIds.FlyingSword;
            if (notify)
            {
                PublishToast(
                    MountContentIds.SwordUnlockedToastKey,
                    MountContentIds.SwordUnlockedToastDefault);
            }

            PersistChanges();
            return true;
        }

        private void ToggleSelectedMount()
        {
            if (IsMounted)
            {
                Dismount();
                return;
            }

            string mountId = IsUnlocked(SelectedMountId)
                ? SelectedMountId
                : MountContentIds.SpiritHorse;
            if (TryMount(mountId)
                && string.Equals(
                    mountId,
                    MountContentIds.FlyingSword,
                    StringComparison.Ordinal))
            {
                TryTakeOff();
            }
        }

        private void RepairLocomotionBinding()
        {
            if (_locomotion == null || _locomotion.Actor == null)
            {
                return;
            }

            if (IsMounted && !ReferenceEquals(_visualActor, _locomotion.Actor))
            {
                _locomotion.SetMountedState(
                    true,
                    GetGroundSpeedMultiplier(ActiveMountId));
                if (IsFlying)
                {
                    _locomotion.SetFlyingState(true, MaximumFlightHeight);
                }

                EnsureMountVisual();
            }
        }

        private void ResolveRuntimeServices()
        {
            if (_input == null || IsMissingUnityService(_input))
            {
                ServiceLocator.TryGet(out _input);
            }

            if (_locomotion == null || IsMissingUnityService(_locomotion))
            {
                ServiceLocator.TryGet(out _locomotion);
            }
        }

        private bool HasReachedFoundation()
        {
            if (ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation))
            {
                return cultivation.Realm >= RealmType.Foundation;
            }

            return (SaveManager.Instance?.Profile?.Realm ?? 0)
                >= (int)RealmType.Foundation;
        }

        private bool IsKnownMapFlightAllowed()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(
                    sceneName,
                    SceneLoader.BlackwindDungeonSceneName,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return !string.Equals(sceneName, SceneLoader.MainMenuSceneName, StringComparison.Ordinal)
                && !string.Equals(sceneName, SceneLoader.BootSceneName, StringComparison.Ordinal)
                && !string.Equals(sceneName, SceneLoader.LoadingSceneName, StringComparison.Ordinal);
        }

        private void HandleRealmBreakthrough(RealmChangeInfo info)
        {
            if (info.Success && info.NewRealm >= RealmType.Foundation)
            {
                RefreshRealmUnlocks(true);
            }
        }

        private void HandleMapLoaded(MapInfo info)
        {
            Dismount();
            _noFlyZones.Clear();
        }

        private void EnsureMountVisual()
        {
            if (!IsMounted || _locomotion?.Actor == null)
            {
                DestroyMountVisual();
                return;
            }

            if (_mountVisual != null
                && ReferenceEquals(_visualActor, _locomotion.Actor))
            {
                return;
            }

            DestroyMountVisual();
            _visualActor = _locomotion.Actor;
            bool flyingSword = string.Equals(
                ActiveMountId,
                MountContentIds.FlyingSword,
                StringComparison.Ordinal);
            _mountVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _mountVisual.name = flyingSword
                ? "MountVisual_FlyingSword"
                : "MountVisual_SpiritHorse";
            _mountVisual.transform.SetParent(_visualActor.transform, false);
            _mountVisual.transform.localPosition = flyingSword
                ? new Vector3(0f, -0.55f, 0f)
                : new Vector3(0f, -0.45f, -0.2f);
            _mountVisual.transform.localScale = flyingSword
                ? new Vector3(0.18f, 0.07f, 1.65f)
                : new Vector3(0.85f, 0.5f, 1.5f);

            Collider collider = _mountVisual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            Renderer renderer = _mountVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                var properties = new MaterialPropertyBlock();
                Color color = flyingSword
                    ? new Color(0.25f, 0.9f, 1f, 1f)
                    : new Color(0.35f, 0.16f, 0.06f, 1f);
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(properties);
            }
        }

        private void DestroyMountVisual()
        {
            if (_mountVisual != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_mountVisual);
                }
                else
                {
                    DestroyImmediate(_mountVisual);
                }
            }

            _mountVisual = null;
            _visualActor = null;
        }

        private void ForceRuntimeDismount()
        {
            ResolveRuntimeServices();
            if (IsFlying)
            {
                _locomotion?.SetFlyingState(false, MaximumFlightHeight);
            }

            _locomotion?.SetMountedState(false, 1f);
            IsFlying = false;
            IsMounted = false;
            ActiveMountId = string.Empty;
            DestroyMountVisual();
        }

        private bool TryRegisterSaveModule()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                return false;
            }

            _registeredSaveModule = saveManager.RegisterModule(
                SaveModuleName,
                CaptureSaveData,
                RestoreSaveData,
                ResetMounts,
                optional: true);
            if (_registeredSaveModule)
            {
                _registeredSaveManager = saveManager;
            }

            return _registeredSaveModule;
        }

        private void RepairSaveRegistration()
        {
            SaveManager current = SaveManager.Instance;
            if (_registeredSaveManager == current && _registeredSaveModule)
            {
                return;
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            TryRegisterSaveModule();
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IMountService>(out IMountService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IMountService>(this);
            _registeredService = true;
        }

        private static float GetGroundSpeedMultiplier(string mountId)
        {
            return string.Equals(
                    mountId,
                    MountContentIds.SpiritHorse,
                    StringComparison.Ordinal)
                ? SpiritHorseSpeedMultiplier
                : FlyingSwordGroundSpeedMultiplier;
        }

        private static bool IsSupportedMount(string mountId)
        {
            return string.Equals(
                    mountId,
                    MountContentIds.SpiritHorse,
                    StringComparison.Ordinal)
                || string.Equals(
                    mountId,
                    MountContentIds.FlyingSword,
                    StringComparison.Ordinal);
        }

        private static bool IsGameplayRunning()
        {
            GameManager gameManager = GameManager.Instance;
            return gameManager == null || gameManager.State == GameState.Playing;
        }

        private static bool IsMissingUnityService(object service)
        {
            return service is UnityEngine.Object unityObject && unityObject == null;
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

        private static void PersistChanges()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule(SaveModuleName);
            }
        }
    }
}
