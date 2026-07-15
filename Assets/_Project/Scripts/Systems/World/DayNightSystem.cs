using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;

namespace Wendao.Systems.World
{
    public sealed class DayNightSystem : MonoBehaviour, IDayNightService
    {
        public const float DefaultCycleDurationSeconds = 48f * 60f;
        public const float SunriseHour = 6f;
        public const float SunsetHour = 18f;
        public const float NightEnemyAttackMultiplier = 1.1f;
        public const string RuntimeLightName = "Directional Light_DayNight_Greybox";

        [SerializeField, Min(60f)]
        private float _cycleDurationSeconds = DefaultCycleDurationSeconds;

        private SaveWorldData _boundWorld;
        private Light _sun;
        private bool _registeredService;

        public float TimeOfDay { get; private set; } = 10f;
        public bool IsNight => IsNightAt(TimeOfDay);
        public float EnemyAttackMultiplier => IsNight
            ? NightEnemyAttackMultiplier
            : 1f;
        public float CycleDurationSeconds => _cycleDurationSeconds;
        public Light ActiveSun => _sun;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IDayNightService>(
                    out IDayNightService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IDayNightService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            RefreshFromSave();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            EnsureSceneLight(SceneManager.GetActiveScene());
            ApplyLighting();
        }

        private void Update()
        {
            RepairServiceRegistration();
            if (!ReferenceEquals(_boundWorld, SaveManager.Instance?.World))
            {
                RefreshFromSave();
            }

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Playing)
            {
                TickTime(Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<IDayNightService>(
                    out IDayNightService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IDayNightService>();
            }

            _registeredService = false;
        }

        public void SetTimeOfDay(float hour)
        {
            if (float.IsNaN(hour) || float.IsInfinity(hour))
            {
                return;
            }

            bool wasNight = IsNight;
            TimeOfDay = NormalizeHour(hour);
            WriteToWorld();
            ApplyLighting();
            if (wasNight != IsNight)
            {
                PublishChange();
            }
        }

        public void TickTime(float deltaTime)
        {
            if (deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            bool wasNight = IsNight;
            float secondsPerDay = Mathf.Max(60f, _cycleDurationSeconds);
            TimeOfDay = NormalizeHour(
                TimeOfDay + deltaTime * 24f / secondsPerDay);
            WriteToWorld();
            ApplyLighting();
            if (wasNight != IsNight)
            {
                PublishChange();
            }
        }

        public void RefreshFromSave()
        {
            _boundWorld = SaveManager.Instance?.World;
            if (_boundWorld != null)
            {
                TimeOfDay = IsValidHour(_boundWorld.TimeOfDay)
                    ? _boundWorld.TimeOfDay
                    : 10f;
                _boundWorld.TimeOfDay = TimeOfDay;
            }

            ApplyLighting();
        }

        public void ConfigureCycleDuration(float seconds)
        {
            if (!float.IsNaN(seconds) && !float.IsInfinity(seconds))
            {
                _cycleDurationSeconds = Mathf.Max(60f, seconds);
            }
        }

        public void AttachSun(Light sun)
        {
            _sun = sun;
            ApplyLighting();
        }

        public void EnsureRegistered()
        {
            RepairServiceRegistration();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _sun = null;
            EnsureSceneLight(scene);
            ApplyLighting();
        }

        private void EnsureSceneLight(Scene scene)
        {
            if (!IsGameplayScene(scene.name) || !scene.isLoaded)
            {
                _sun = null;
                return;
            }

            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int index = 0; index < lights.Length; index++)
            {
                if (lights[index] != null
                    && lights[index].type == LightType.Directional
                    && lights[index].gameObject.scene == scene)
                {
                    _sun = lights[index];
                    return;
                }
            }

            var lightObject = new GameObject(RuntimeLightName);
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            _sun = lightObject.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
        }

        private void ApplyLighting()
        {
            if (_sun == null)
            {
                EnsureSceneLight(SceneManager.GetActiveScene());
            }

            float daylight = Mathf.Clamp01(
                Mathf.Sin((TimeOfDay - SunriseHour) / 12f * Mathf.PI));
            if (_sun != null)
            {
                float sunAngle = TimeOfDay / 24f * 360f - 90f;
                _sun.transform.rotation = Quaternion.Euler(
                    sunAngle,
                    -32f,
                    0f);
                _sun.intensity = Mathf.Lerp(0.08f, 1.1f, daylight);
                _sun.color = Color.Lerp(
                    new Color(0.34f, 0.43f, 0.68f),
                    new Color(1f, 0.94f, 0.8f),
                    daylight);
            }

            RenderSettings.ambientIntensity = Mathf.Lerp(0.18f, 1f, daylight);
        }

        private void WriteToWorld()
        {
            if (!ReferenceEquals(_boundWorld, SaveManager.Instance?.World))
            {
                _boundWorld = SaveManager.Instance?.World;
            }

            if (_boundWorld != null)
            {
                _boundWorld.TimeOfDay = TimeOfDay;
            }
        }

        private void PublishChange()
        {
            EventBus.Publish(
                WorldEnvironmentEvents.DayNightChanged,
                new DayNightInfo
                {
                    IsNight = IsNight,
                    TimeOfDay = TimeOfDay
                });
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IDayNightService>(
                    out IDayNightService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IDayNightService>(this);
            _registeredService = true;
        }

        private static bool IsGameplayScene(string sceneName)
        {
            return sceneName == SceneLoader.DefaultMapSceneName
                || sceneName == SceneLoader.CangwuMapSceneName
                || sceneName == SceneLoader.BlackwindDungeonSceneName;
        }

        private static bool IsNightAt(float hour)
        {
            return hour < SunriseHour || hour >= SunsetHour;
        }

        private static bool IsValidHour(float hour)
        {
            return !float.IsNaN(hour)
                && !float.IsInfinity(hour)
                && hour >= 0f
                && hour < 24f;
        }

        private static float NormalizeHour(float hour)
        {
            return Mathf.Repeat(hour, 24f);
        }
    }
}
