using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;

namespace Wendao.Systems.World
{
    public sealed class BlackwindDungeonSystem : SafeBehaviour,
        IBlackwindDungeonService
    {
        public const int MinimumFloor = 1;
        public const int MaximumFloor = 5;
        public const int MaximumCheckpoint = 4;

        private readonly bool[] _floorCompleted = new bool[MaximumFloor + 1];
        private bool _registeredService;
        private bool _combatObjectiveCleared;

        public int Checkpoint
        {
            get
            {
                SaveWorldData world = SaveManager.Instance?.World;
                if (world?.DungeonCheckpoint != null
                    && world.DungeonCheckpoint.TryGetValue(
                        MapContentIds.BlackwindMap,
                        out int value))
                {
                    return Mathf.Clamp(value, 0, MaximumCheckpoint);
                }

                return 0;
            }
        }

        public int CurrentFloor { get; private set; } = MinimumFloor;
        public bool IsRunActive { get; private set; }
        public bool IsRunCompleted { get; private set; }
        public bool IsPressurePlateActive { get; private set; }
        public bool IsHealingSpringUsed { get; private set; }

        private void Awake()
        {
            if (ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IBlackwindDungeonService>(this);
            _registeredService = true;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DeathInfo>(
                CombatEvents.PlayerDied,
                HandlePlayerDied);
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DeathInfo>(
                CombatEvents.PlayerDied,
                HandlePlayerDied);
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<IBlackwindDungeonService>(
                    out IBlackwindDungeonService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IBlackwindDungeonService>();
            }

            _registeredService = false;
        }

        public bool IsFloorComplete(int floor)
        {
            return floor >= MinimumFloor
                && floor <= MaximumFloor
                && _floorCompleted[floor];
        }

        public void BeginRun()
        {
            int checkpoint = Checkpoint;
            ResetRuntimeState(checkpoint + 1);
            IsRunActive = true;
            for (int floor = MinimumFloor; floor <= checkpoint; floor++)
            {
                _floorCompleted[floor] = true;
            }

            EventBus.Publish(
                BlackwindDungeonEvents.RunStarted,
                new BlackwindRunInfo
                {
                    StartFloor = CurrentFloor,
                    Checkpoint = checkpoint,
                    WasFailure = false
                });
        }

        public void EndRun()
        {
            IsRunActive = false;
            IsRunCompleted = false;
            IsPressurePlateActive = false;
            IsHealingSpringUsed = false;
            _combatObjectiveCleared = false;
        }

        public bool EnterFloor(int floor)
        {
            if (!IsRunActive
                || floor < MinimumFloor
                || floor > MaximumFloor
                || floor > Checkpoint + 1)
            {
                return false;
            }

            if (CurrentFloor == floor)
            {
                return true;
            }

            CurrentFloor = floor;
            _combatObjectiveCleared = false;
            EventBus.Publish(
                BlackwindDungeonEvents.FloorEntered,
                new BlackwindFloorInfo
                {
                    Floor = floor,
                    Checkpoint = Checkpoint
                });
            return true;
        }

        public bool ActivatePressurePlate()
        {
            if (!IsRunActive || CurrentFloor != 1 || IsPressurePlateActive)
            {
                return false;
            }

            IsPressurePlateActive = true;
            TryCompleteFirstFloor();
            return true;
        }

        public bool NotifyCombatObjectiveCleared(int floor)
        {
            if (!IsRunActive
                || floor != CurrentFloor
                || IsFloorComplete(floor))
            {
                return false;
            }

            _combatObjectiveCleared = true;
            switch (floor)
            {
                case 1:
                    TryCompleteFirstFloor();
                    return true;
                case 2:
                case 4:
                    CompleteFloor(floor);
                    return true;
                case 5:
                    NotifyBossDefeated();
                    return true;
                default:
                    return false;
            }
        }

        public bool CompleteExplorationFloor(int floor)
        {
            if (!IsRunActive
                || floor != 3
                || CurrentFloor != floor
                || IsFloorComplete(floor))
            {
                return false;
            }

            CompleteFloor(floor);
            return true;
        }

        public bool TryUseHealingSpring()
        {
            if (!IsRunActive
                || CurrentFloor != 4
                || IsHealingSpringUsed)
            {
                return false;
            }

            IsHealingSpringUsed = true;
            return true;
        }

        public void NotifyBossDefeated()
        {
            if (!IsRunActive || CurrentFloor != 5 || IsRunCompleted)
            {
                return;
            }

            _floorCompleted[5] = true;
            IsRunCompleted = true;
            IsHealingSpringUsed = false;
            PersistCriticalWorldState();
            EventBus.Publish(
                BlackwindDungeonEvents.RunCompleted,
                new BlackwindFloorInfo
                {
                    Floor = 5,
                    Checkpoint = Checkpoint
                });
        }

        private void TryCompleteFirstFloor()
        {
            if (IsPressurePlateActive && _combatObjectiveCleared)
            {
                CompleteFloor(1);
            }
        }

        private void CompleteFloor(int floor)
        {
            if (floor < MinimumFloor
                || floor > MaximumCheckpoint
                || _floorCompleted[floor])
            {
                return;
            }

            _floorCompleted[floor] = true;
            SaveWorldData world = SaveManager.Instance?.World;
            if (world?.DungeonCheckpoint != null)
            {
                world.DungeonCheckpoint[MapContentIds.BlackwindMap] =
                    Mathf.Max(Checkpoint, floor);
            }

            PersistCriticalWorldState();
            EventBus.Publish(
                BlackwindDungeonEvents.FloorCompleted,
                new BlackwindFloorInfo
                {
                    Floor = floor,
                    Checkpoint = Checkpoint
                });
        }

        private void HandlePlayerDied(DeathInfo info)
        {
            if (!IsRunActive)
            {
                return;
            }

            int checkpoint = Checkpoint;
            ResetRuntimeState(checkpoint + 1);
            IsRunActive = true;
            for (int floor = MinimumFloor; floor <= checkpoint; floor++)
            {
                _floorCompleted[floor] = true;
            }

            EventBus.Publish(
                BlackwindDungeonEvents.RunReset,
                new BlackwindRunInfo
                {
                    StartFloor = CurrentFloor,
                    Checkpoint = checkpoint,
                    WasFailure = true
                });
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.name == SceneLoader.BlackwindDungeonSceneName)
            {
                EndRun();
            }
        }

        private void ResetRuntimeState(int startFloor)
        {
            Array.Clear(_floorCompleted, 0, _floorCompleted.Length);
            CurrentFloor = Mathf.Clamp(
                startFloor,
                MinimumFloor,
                MaximumFloor);
            IsRunCompleted = false;
            IsPressurePlateActive = false;
            IsHealingSpringUsed = false;
            _combatObjectiveCleared = false;
        }

        private static void PersistCriticalWorldState()
        {
            SaveManager save = SaveManager.Instance;
            if (save != null && save.ActiveSlot >= 0)
            {
                save.SaveGame(save.ActiveSlot);
            }
        }
    }
}
