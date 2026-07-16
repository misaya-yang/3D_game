using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Feedback;

namespace Wendao.Systems.World
{
    public sealed class WeatherSystem : MonoBehaviour, IWeatherService
    {
        public const float MinimumDurationSeconds = 120f;
        public const float MaximumDurationSeconds = 300f;
        public const float TransitionDurationSeconds = 3f;
        public const float RainElementBonus = 0.1f;
        public const float FogVisionMultiplier = 0.75f;

        private WeatherWeight[] _weatherPool = Array.Empty<WeatherWeight>();
        private System.Random _random;
        private WeatherId _transitionFrom = WeatherId.Clear;
        private float _transitionRemaining;
        private float _targetDuration = MinimumDurationSeconds;
        private ParticleSystem _rainParticles;
        private bool _registeredService;
        private string _sceneName = string.Empty;

        public WeatherId Current { get; private set; } = WeatherId.Clear;
        public WeatherId Target { get; private set; } = WeatherId.Clear;
        public float Intensity { get; private set; } = 1f;
        public bool IsTransitioning => _transitionRemaining > 0f;
        public float DurationRemaining { get; private set; }
        public float TransitionRemaining => _transitionRemaining;
        public WeatherWeight[] ActiveWeatherPool => ClonePool(_weatherPool);

        private void Awake()
        {
            if (ServiceLocator.TryGet<IWeatherService>(
                    out IWeatherService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IWeatherService>(this);
            _registeredService = true;
            _random = new System.Random(Environment.TickCount);
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            ConfigureForScene(SceneManager.GetActiveScene().name);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Update()
        {
            RepairServiceRegistration();
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State == GameState.Playing)
            {
                TickWeather(Time.deltaTime);
            }
        }

        private void LateUpdate()
        {
            if (_rainParticles == null || UnityEngine.Camera.main == null)
            {
                return;
            }

            Transform cameraTransform = UnityEngine.Camera.main.transform;
            _rainParticles.transform.position = cameraTransform.position
                + cameraTransform.forward * 5f
                + Vector3.up * 7f;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<IWeatherService>(
                    out IWeatherService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IWeatherService>();
            }

            _registeredService = false;
            RenderSettings.fog = false;
        }

        public void ForceWeather(WeatherId id, float duration)
        {
            WeatherId requested = NormalizeWeather(id);
            float requestedDuration = IsFinite(duration)
                ? Mathf.Max(0.01f, duration)
                : MinimumDurationSeconds;
            if (!IsTransitioning && requested == Current)
            {
                Target = Current;
                DurationRemaining = requestedDuration;
                _targetDuration = requestedDuration;
                Intensity = 1f;
                ApplyVisuals();
                return;
            }

            _transitionFrom = Current;
            Target = requested;
            _targetDuration = requestedDuration;
            _transitionRemaining = TransitionDurationSeconds;
            Intensity = 0f;
            ApplyVisuals();
        }

        public void TickWeather(float deltaTime)
        {
            if (deltaTime <= 0f || !IsFinite(deltaTime))
            {
                return;
            }

            float clampedDelta = Mathf.Max(0f, deltaTime);
            if (IsTransitioning)
            {
                _transitionRemaining = Mathf.Max(
                    0f,
                    _transitionRemaining - clampedDelta);
                Intensity = 1f - Mathf.Clamp01(
                    _transitionRemaining / TransitionDurationSeconds);
                ApplyVisuals();
                if (_transitionRemaining <= 0f)
                {
                    Current = Target;
                    Intensity = 1f;
                    DurationRemaining = _targetDuration;
                    ApplyVisuals();
                    ApplyAmbience();
                    EventBus.Publish(
                        WorldEnvironmentEvents.WeatherChanged,
                        new WeatherInfo
                        {
                            Weather = Current,
                            Intensity = 1f
                        });
                }

                return;
            }

            DurationRemaining = Mathf.Max(
                0f,
                DurationRemaining - clampedDelta);
            if (DurationRemaining <= 0f)
            {
                ForceWeather(
                    DrawNextWeather(),
                    DrawDuration());
            }
        }

        public void ConfigureForScene(string sceneName)
        {
            _sceneName = sceneName ?? string.Empty;
            _weatherPool = WorldEnvironmentProfiles.GetWeatherPool(sceneName);
            Current = WeatherId.Clear;
            Target = WeatherId.Clear;
            _transitionFrom = WeatherId.Clear;
            _transitionRemaining = 0f;
            Intensity = 1f;
            DurationRemaining = DrawDuration();
            _targetDuration = DurationRemaining;
            ApplyVisuals();
            ApplyAmbience();
        }

        public float GetElementDamageBonus(ElementType element)
        {
            return BlendEffect(
                weather => weather == WeatherId.Rain
                    && (element == ElementType.Water
                        || element == ElementType.Ice
                        || element == ElementType.Lightning)
                        ? RainElementBonus
                        : 0f);
        }

        public float GetVisionMul()
        {
            return BlendEffect(
                weather => weather == WeatherId.Fog
                    ? FogVisionMultiplier
                    : 1f);
        }

        public float GetFlightSpeedMul()
        {
            return BlendEffect(
                weather => weather == WeatherId.Storm ? 0.8f : 1f);
        }

        public void ConfigureRandomSeed(int seed)
        {
            _random = new System.Random(seed);
        }

        public void EnsureRegistered()
        {
            RepairServiceRegistration();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureForScene(scene.name);
        }

        private void ApplyVisuals()
        {
            float fogAmount = BlendEffect(
                weather => weather == WeatherId.Fog ? 1f : 0f);
            float rainAmount = BlendEffect(
                weather => weather == WeatherId.Rain ? 1f : 0f);

            WorldVisualProfile visual =
                WorldEnvironmentProfiles.GetVisualProfile(_sceneName);
            float fogDensity = Mathf.Lerp(
                visual.BaseFogDensity,
                Mathf.Max(0.018f, visual.BaseFogDensity + 0.012f),
                fogAmount);
            RenderSettings.fog = fogDensity > 0.0001f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = Color.Lerp(
                visual.FogColor,
                new Color(0.42f, 0.48f, 0.5f),
                fogAmount);

            EnsureRainParticles();
            if (_rainParticles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = _rainParticles.emission;
            emission.rateOverTime = Mathf.Lerp(0f, 180f, rainAmount);
            if (rainAmount > 0.001f && !_rainParticles.isPlaying)
            {
                _rainParticles.Play(true);
            }
            else if (rainAmount <= 0.001f && _rainParticles.isPlaying)
            {
                _rainParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EnsureRainParticles()
        {
            if (_rainParticles != null)
            {
                return;
            }

            var rainObject = new GameObject("Weather_Rain_Greybox");
            rainObject.transform.SetParent(transform, false);
            _rainParticles = rainObject.AddComponent<ParticleSystem>();
            _rainParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _rainParticles.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = 1.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.startColor = new Color(0.56f, 0.72f, 0.9f, 0.7f);
            main.gravityModifier = 0.15f;
            main.maxParticles = 512;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.ShapeModule shape = _rainParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18f, 1f, 18f);

            ParticleSystem.EmissionModule emission = _rainParticles.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                _rainParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = -11f;
        }

        private void ApplyAmbience()
        {
            if (!ServiceLocator.TryGet<IAudioService>(out IAudioService audio))
            {
                return;
            }

            if (Current == WeatherId.Rain)
            {
                audio.SetAmbience(AudioContentIds.Rain);
                return;
            }

            audio.SetAmbience(
                _sceneName == SceneLoader.DefaultMapSceneName
                    ? AudioContentIds.WindPlain
                    : _sceneName == SceneLoader.CangwuMapSceneName
                        || _sceneName == SceneLoader.BlackwindDungeonSceneName
                        ? AudioContentIds.WindMountain
                        : string.Empty);
        }

        private float BlendEffect(Func<WeatherId, float> selector)
        {
            if (!IsTransitioning)
            {
                return selector(Current);
            }

            return Mathf.Lerp(
                selector(_transitionFrom),
                selector(Target),
                Intensity);
        }

        private WeatherId DrawNextWeather()
        {
            EnsureRandom();
            float total = 0f;
            for (int index = 0; index < _weatherPool.Length; index++)
            {
                total += Mathf.Max(0f, _weatherPool[index]?.Weight ?? 0f);
            }

            if (total <= 0f)
            {
                return WeatherId.Clear;
            }

            double roll = _random.NextDouble() * total;
            float cumulative = 0f;
            for (int index = 0; index < _weatherPool.Length; index++)
            {
                WeatherWeight entry = _weatherPool[index];
                if (entry == null)
                {
                    continue;
                }

                cumulative += Mathf.Max(0f, entry.Weight);
                if (roll <= cumulative)
                {
                    return NormalizeWeather(entry.Weather);
                }
            }

            return WeatherId.Clear;
        }

        private float DrawDuration()
        {
            EnsureRandom();
            return Mathf.Lerp(
                MinimumDurationSeconds,
                MaximumDurationSeconds,
                (float)_random.NextDouble());
        }

        private void EnsureRandom()
        {
            if (_random == null)
            {
                _random = new System.Random(Environment.TickCount);
            }
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IWeatherService>(
                    out IWeatherService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IWeatherService>(this);
            _registeredService = true;
        }

        private static WeatherId NormalizeWeather(WeatherId id)
        {
            return id == WeatherId.Rain || id == WeatherId.Fog
                ? id
                : WeatherId.Clear;
        }

        private static WeatherWeight[] ClonePool(WeatherWeight[] source)
        {
            var copy = new WeatherWeight[source?.Length ?? 0];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = new WeatherWeight
                {
                    Weather = source[index].Weather,
                    Weight = source[index].Weight
                };
            }

            return copy;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
