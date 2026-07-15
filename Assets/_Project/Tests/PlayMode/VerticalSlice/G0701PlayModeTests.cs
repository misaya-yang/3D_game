using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;
using Wendao.Systems.World;
using Object = UnityEngine.Object;

namespace Wendao.Tests.PlayMode.VerticalSlice
{
    public sealed class G0701PlayModeTests
    {
        private string _storageRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
            _storageRoot = Path.Combine(
                Path.GetTempPath(),
                "WendaoG0701Tests_" + Guid.NewGuid().ToString("N"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            EventBus.Clear();
            DestroyRuntimeObjects();
            yield return null;
            ServiceLocator.Clear();
            if (!string.IsNullOrEmpty(_storageRoot)
                && Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, true);
            }
        }

        [Test]
        public void DayNightCycleChangesLightAndPublishesSixEighteenBoundary()
        {
            SaveManager save = CreateSaveManager();
            DayNightSystem dayNight = new GameObject("[G0701DayNight]")
                .AddComponent<DayNightSystem>();
            Light sun = new GameObject("[G0701Sun]").AddComponent<Light>();
            sun.type = LightType.Directional;
            dayNight.AttachSun(sun);

            var changes = new List<DayNightInfo>();
            EventBus.Subscribe<DayNightInfo>(
                WorldEnvironmentEvents.DayNightChanged,
                changes.Add);

            dayNight.SetTimeOfDay(12f);
            float noonIntensity = sun.intensity;
            dayNight.SetTimeOfDay(17.9f);
            dayNight.ConfigureCycleDuration(240f);
            dayNight.TickTime(2f);

            Assert.That(dayNight.TimeOfDay, Is.EqualTo(18.1f).Within(0.001f));
            Assert.That(dayNight.IsNight, Is.True);
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].IsNight, Is.True);
            Assert.That(sun.intensity, Is.LessThan(noonIntensity));
            Assert.That(
                save.World.TimeOfDay,
                Is.EqualTo(dayNight.TimeOfDay).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator TimeOfDayPersistsThroughWorldSaveRoundTrip()
        {
            SaveManager save = CreateSaveManager();
            DayNightSystem dayNight = new GameObject("[G0701DayNight]")
                .AddComponent<DayNightSystem>();
            dayNight.SetTimeOfDay(22.5f);
            Assert.That(save.SaveGame(0), Is.True, save.LastError);

            Object.Destroy(dayNight.gameObject);
            Object.Destroy(save.gameObject);
            yield return null;
            ServiceLocator.Clear();

            save = CreateSaveManager();
            Assert.That(save.LoadGame(0), Is.True, save.LastError);
            dayNight = new GameObject("[G0701DayNightReloaded]")
                .AddComponent<DayNightSystem>();
            dayNight.RefreshFromSave();

            Assert.That(dayNight.TimeOfDay, Is.EqualTo(22.5f).Within(0.001f));
            Assert.That(dayNight.IsNight, Is.True);
        }

        [Test]
        public void RainAndFogTransitionOverThreeSecondsAndPublishEvents()
        {
            WeatherSystem weather = new GameObject("[G0701Weather]")
                .AddComponent<WeatherSystem>();
            weather.ConfigureForScene(SceneLoader.DefaultMapSceneName);

            var changes = new List<WeatherInfo>();
            EventBus.Subscribe<WeatherInfo>(
                WorldEnvironmentEvents.WeatherChanged,
                changes.Add);

            weather.ForceWeather(WeatherId.Rain, 10f);
            weather.TickWeather(1.5f);
            Assert.That(weather.IsTransitioning, Is.True);
            Assert.That(
                weather.GetElementDamageBonus(ElementType.Ice),
                Is.EqualTo(0.05f).Within(0.001f));
            weather.TickWeather(1.5f);

            Assert.That(weather.Current, Is.EqualTo(WeatherId.Rain));
            Assert.That(
                weather.GetElementDamageBonus(ElementType.Water),
                Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(
                weather.GetElementDamageBonus(ElementType.Fire),
                Is.Zero);
            Assert.That(changes, Has.Count.EqualTo(1));

            weather.ForceWeather(WeatherId.Fog, 10f);
            weather.TickWeather(WeatherSystem.TransitionDurationSeconds);
            Assert.That(weather.Current, Is.EqualTo(WeatherId.Fog));
            Assert.That(
                weather.GetVisionMul(),
                Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(changes, Has.Count.EqualTo(2));
        }

        [Test]
        public void ThreeMapWeatherProfilesProvideCompatibleMvpPools()
        {
            WeatherWeight[] qingshi = WorldEnvironmentProfiles.GetWeatherPool(
                SceneLoader.DefaultMapSceneName);
            WeatherWeight[] cangwu = WorldEnvironmentProfiles.GetWeatherPool(
                SceneLoader.CangwuMapSceneName);
            WeatherWeight[] blackwind = WorldEnvironmentProfiles.GetWeatherPool(
                SceneLoader.BlackwindDungeonSceneName);

            AssertPool(qingshi, 70f, 20f, 10f);
            AssertPool(cangwu, 50f, 20f, 30f);
            Assert.That(blackwind, Has.Length.EqualTo(2));
            Assert.That(blackwind[0].Weather, Is.EqualTo(WeatherId.Clear));
            Assert.That(blackwind[0].Weight, Is.EqualTo(80f));
            Assert.That(blackwind[1].Weather, Is.EqualTo(WeatherId.Fog));
            Assert.That(blackwind[1].Weight, Is.EqualTo(20f));

            qingshi[0].Weight = 0f;
            Assert.That(
                WorldEnvironmentProfiles.GetWeatherPool(
                    SceneLoader.DefaultMapSceneName)[0].Weight,
                Is.EqualTo(70f),
                "Profiles must return defensive copies.");
        }

        [Test]
        public void CombatUsesRainElementBonusAndNightEnemyMultiplier()
        {
            CreateSaveManager();
            DayNightSystem dayNight = new GameObject("[G0701DayNight]")
                .AddComponent<DayNightSystem>();
            WeatherSystem weather = new GameObject("[G0701Weather]")
                .AddComponent<WeatherSystem>();
            CombatSystem combat = new GameObject("[G0701Combat]")
                .AddComponent<CombatSystem>();
            G0701CombatActor source = new GameObject("EnemySource")
                .AddComponent<G0701CombatActor>();
            source.Configure(CombatTeam.Enemy);
            G0701CombatActor target = new GameObject("PlayerTarget")
                .AddComponent<G0701CombatActor>();
            target.Configure(CombatTeam.Player);

            var request = new DamageRequest
            {
                Source = source.gameObject,
                BaseDamage = 100f,
                Type = DamageType.Ice,
                Element = ElementType.Ice,
                Multiplier = 1f,
                CanCrit = false
            };

            dayNight.SetTimeOfDay(12f);
            Assert.That(
                combat.ComputeDamage(request, target).Amount,
                Is.EqualTo(100f).Within(0.001f));

            weather.ForceWeather(WeatherId.Rain, 10f);
            weather.TickWeather(WeatherSystem.TransitionDurationSeconds);
            Assert.That(
                combat.ComputeDamage(request, target).Amount,
                Is.EqualTo(110f).Within(0.001f));

            dayNight.SetTimeOfDay(22f);
            Assert.That(
                combat.ComputeDamage(request, target).Amount,
                Is.EqualTo(121f).Within(0.001f));
        }

        private SaveManager CreateSaveManager()
        {
            SaveManager save = new GameObject("[G0701Save]")
                .AddComponent<SaveManager>();
            save.ConfigureStorageRoot(_storageRoot);
            return save;
        }

        private static void AssertPool(
            WeatherWeight[] pool,
            float clear,
            float rain,
            float fog)
        {
            Assert.That(pool, Has.Length.EqualTo(3));
            Assert.That(pool[0].Weather, Is.EqualTo(WeatherId.Clear));
            Assert.That(pool[0].Weight, Is.EqualTo(clear));
            Assert.That(pool[1].Weather, Is.EqualTo(WeatherId.Rain));
            Assert.That(pool[1].Weight, Is.EqualTo(rain));
            Assert.That(pool[2].Weather, Is.EqualTo(WeatherId.Fog));
            Assert.That(pool[2].Weight, Is.EqualTo(fog));
        }

        private static void DestroyRuntimeObjects()
        {
            DestroyAll<DayNightSystem>();
            DestroyAll<WeatherSystem>();
            DestroyAll<CombatSystem>();
            DestroyAll<G0701CombatActor>();
            DestroyAll<SaveManager>();
            DestroyAll<GameManager>();
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int index = 0; index < lights.Length; index++)
            {
                if (lights[index] != null
                    && lights[index].gameObject.name.StartsWith(
                        "[G0701",
                        StringComparison.Ordinal))
                {
                    Object.Destroy(lights[index].gameObject);
                }
            }
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int index = 0; index < instances.Length; index++)
            {
                if (instances[index] != null)
                {
                    Object.Destroy(instances[index].gameObject);
                }
            }
        }
    }

    public sealed class G0701CombatActor : MonoBehaviour,
        IDamageable,
        ICombatStatsProvider,
        ICombatTeamProvider
    {
        public float CurrentHp { get; private set; } = 1000f;
        public float MaxHp => 1000f;
        public bool IsDead => CurrentHp <= 0f;
        public float Attack => 0f;
        public float Defense => 0f;
        public float CritRate => 0f;
        public float CritDamage => 1.5f;
        public CombatTeam Team { get; private set; }

        public void Configure(CombatTeam team)
        {
            Team = team;
        }

        public void ApplyDamage(DamageInfo info)
        {
            CurrentHp = Mathf.Max(0f, CurrentHp - Mathf.Max(0f, info.Amount));
        }

        public void ApplyHeal(float amount, string sourceId)
        {
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + Mathf.Max(0f, amount));
        }
    }
}
