using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;

namespace Wendao.Systems.World
{
    public sealed class SceneLoader : Singleton<SceneLoader>
    {
        public const string MapLoadedEvent = "OnMapLoaded";
        public const string BootSceneName = "Boot";
        public const string MainMenuSceneName = "MainMenu";
        public const string LoadingSceneName = "Loading";
        public const string DefaultMapId = "map_qingshi";
        public const string DefaultMapSceneName = "Map_Qingshi";
        public const string CangwuMapId = "map_cangwu";
        public const string CangwuMapSceneName = "Map_Cangwu";
        public const string BlackwindMapId = "map_blackwind";
        public const string BlackwindDungeonSceneName = "Dungeon_Blackwind";

        private const float LoadingSceneWeight = 0.1f;
        private const float TargetReadyProgress = 0.95f;

        private readonly Dictionary<string, string> _mapScenes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [DefaultMapId] = DefaultMapSceneName,
                [CangwuMapId] = CangwuMapSceneName,
                [BlackwindMapId] = BlackwindDungeonSceneName
            };
        private readonly SceneLoadProgress _progress = new SceneLoadProgress();

        public event Action<float> ProgressChanged
        {
            add => _progress.Changed += value;
            remove => _progress.Changed -= value;
        }

        public float Progress => _progress.Value;
        public int LoadSequence => _progress.Sequence;
        public bool IsLoading { get; private set; }
        public string PendingMapId { get; private set; } = string.Empty;
        public string PendingSpawnId { get; private set; } = string.Empty;
        public string LastLoadedMapId { get; private set; } = string.Empty;
        public string LastError { get; private set; }
        public bool HasQueuedMapLoad { get; private set; }
        public string QueuedMapId { get; private set; } = string.Empty;
        public string QueuedSpawnId { get; private set; } = string.Empty;

        public bool LoadMap(string mapId, string spawnId)
        {
            if (IsLoading)
            {
                return FailImmediate("A scene load is already in progress.");
            }

            if (HasQueuedMapLoad)
            {
                return FailImmediate("A map load is already queued.");
            }

            if (!TryGetSceneName(mapId, out string targetScene))
            {
                return FailImmediate($"Unknown map id: {mapId}");
            }

            if (!Application.CanStreamedLevelBeLoaded(LoadingSceneName)
                || !Application.CanStreamedLevelBeLoaded(targetScene))
            {
                return FailImmediate(
                    $"Loading or target scene is missing from Build Settings: {targetScene}");
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null
                || (gameManager.State != GameState.MainMenu
                    && gameManager.State != GameState.Playing))
            {
                return FailImmediate("Maps can only be loaded from MainMenu or Playing state.");
            }

            if (gameManager.State == GameState.Playing
                && ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation)
                && cultivation.IsBreakthroughActive)
            {
                LastError = null;
                HasQueuedMapLoad = true;
                QueuedMapId = mapId;
                QueuedSpawnId = spawnId ?? string.Empty;
                return true;
            }

            GameState previousState = gameManager.State;
            if (!gameManager.TrySetState(GameState.Loading))
            {
                return FailImmediate("GameManager rejected the transition to Loading.");
            }

            string sourceScene = SceneManager.GetActiveScene().name;
            BeginLoad(mapId, spawnId);
            StartCoroutine(LoadMapRoutine(targetScene, sourceScene, previousState));
            return true;
        }

        public bool LoadMainMenu()
        {
            if (IsLoading)
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            {
                return FailImmediate("MainMenu scene is missing from Build Settings.");
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return FailImmediate("GameManager is unavailable.");
            }

            if (gameManager.State != GameState.MainMenu
                && !gameManager.TrySetState(GameState.MainMenu))
            {
                return FailImmediate("GameManager rejected the transition to MainMenu.");
            }

            ClearQueuedMapLoad();
            BeginLoad(string.Empty, string.Empty);
            StartCoroutine(LoadMainMenuRoutine());
            return true;
        }

        public bool RegisterMapScene(string mapId, string sceneName)
        {
            if (string.IsNullOrWhiteSpace(mapId)
                || string.IsNullOrWhiteSpace(sceneName)
                || _mapScenes.ContainsKey(mapId))
            {
                return false;
            }

            _mapScenes.Add(mapId, sceneName);
            return true;
        }

        public bool TryGetSceneName(string mapId, out string sceneName)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                sceneName = null;
                return false;
            }

            return _mapScenes.TryGetValue(mapId, out sceneName);
        }

        protected override void OnSingletonAwake()
        {
            IsLoading = false;
            PendingMapId = string.Empty;
            PendingSpawnId = string.Empty;
            LastLoadedMapId = string.Empty;
            LastError = null;
            ClearQueuedMapLoad();
        }

        private void Update()
        {
            if (!HasQueuedMapLoad || IsLoading)
            {
                return;
            }

            if (ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation)
                && cultivation.IsBreakthroughActive)
            {
                return;
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return;
            }

            if (gameManager.State == GameState.MainMenu)
            {
                ClearQueuedMapLoad();
                return;
            }

            if (gameManager.State != GameState.Playing)
            {
                return;
            }

            string mapId = QueuedMapId;
            string spawnId = QueuedSpawnId;
            ClearQueuedMapLoad();
            LoadMap(mapId, spawnId);
        }

        public bool CancelQueuedMapLoad()
        {
            if (!HasQueuedMapLoad)
            {
                return false;
            }

            ClearQueuedMapLoad();
            return true;
        }

        private IEnumerator LoadMainMenuRoutine()
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(
                MainMenuSceneName,
                LoadSceneMode.Single);
            if (operation == null)
            {
                CompleteFailure("Unity did not create the MainMenu load operation.");
                yield break;
            }

            while (!operation.isDone)
            {
                _progress.Report(NormalizeAsyncProgress(operation.progress));
                yield return null;
            }

            _progress.Report(1f);
            CompleteSuccess();
        }

        private IEnumerator LoadMapRoutine(
            string targetScene,
            string sourceScene,
            GameState previousState)
        {
            AsyncOperation loadingScreen = SceneManager.LoadSceneAsync(
                LoadingSceneName,
                LoadSceneMode.Single);
            if (loadingScreen == null)
            {
                RestoreState(previousState);
                CompleteFailure("Unity did not create the Loading scene operation.");
                yield break;
            }

            while (!loadingScreen.isDone)
            {
                _progress.Report(
                    LoadingSceneWeight * NormalizeAsyncProgress(loadingScreen.progress));
                yield return null;
            }

            _progress.Report(LoadingSceneWeight);
            yield return null;

            AsyncOperation targetOperation = SceneManager.LoadSceneAsync(
                targetScene,
                LoadSceneMode.Single);
            if (targetOperation == null)
            {
                yield return RecoverSourceScene(sourceScene, previousState);
                CompleteFailure($"Unity did not create the scene operation for {targetScene}.");
                yield break;
            }

            targetOperation.allowSceneActivation = false;
            while (targetOperation.progress < 0.9f)
            {
                float targetProgress = NormalizeAsyncProgress(targetOperation.progress);
                _progress.Report(
                    Mathf.Lerp(LoadingSceneWeight, TargetReadyProgress, targetProgress));
                yield return null;
            }

            _progress.Report(TargetReadyProgress);
            yield return null;
            targetOperation.allowSceneActivation = true;

            while (!targetOperation.isDone)
            {
                yield return null;
            }

            _progress.Report(1f);
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null || !gameManager.TrySetState(GameState.Playing))
            {
                CompleteFailure("Target scene loaded, but GameManager could not enter Playing.");
                yield break;
            }

            string loadedMapId = PendingMapId;
            string loadedSpawnId = PendingSpawnId;
            LastLoadedMapId = loadedMapId;
            CompleteSuccess();
            EventBus.Publish(
                MapLoadedEvent,
                new MapInfo
                {
                    MapId = loadedMapId,
                    SpawnId = loadedSpawnId
                });
        }

        private IEnumerator RecoverSourceScene(string sourceScene, GameState previousState)
        {
            if (!string.IsNullOrEmpty(sourceScene)
                && Application.CanStreamedLevelBeLoaded(sourceScene)
                && SceneManager.GetActiveScene().name != sourceScene)
            {
                AsyncOperation recovery = SceneManager.LoadSceneAsync(
                    sourceScene,
                    LoadSceneMode.Single);
                if (recovery != null)
                {
                    yield return recovery;
                }
            }

            RestoreState(previousState);
        }

        private void BeginLoad(string mapId, string spawnId)
        {
            LastError = null;
            PendingMapId = mapId ?? string.Empty;
            PendingSpawnId = spawnId ?? string.Empty;
            IsLoading = true;
            _progress.Begin();
        }

        private void ClearQueuedMapLoad()
        {
            HasQueuedMapLoad = false;
            QueuedMapId = string.Empty;
            QueuedSpawnId = string.Empty;
        }

        private void CompleteSuccess()
        {
            IsLoading = false;
            PendingMapId = string.Empty;
            PendingSpawnId = string.Empty;
            LastError = null;
        }

        private void CompleteFailure(string error)
        {
            IsLoading = false;
            PendingMapId = string.Empty;
            PendingSpawnId = string.Empty;
            LastError = error;
            Debug.LogError(error, this);
        }

        private void RestoreState(GameState previousState)
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Loading)
            {
                gameManager.TrySetState(previousState);
            }
        }

        private bool FailImmediate(string error)
        {
            LastError = error;
            Debug.LogError(error, this);
            return false;
        }

        private static float NormalizeAsyncProgress(float operationProgress)
        {
            return Mathf.Clamp01(operationProgress / 0.9f);
        }
    }
}
