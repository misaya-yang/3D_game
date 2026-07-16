using UnityEngine;
using Wendao.Data;

namespace Wendao.Systems.World
{
    public readonly struct WorldVisualProfile
    {
        public WorldVisualProfile(
            Color fogColor,
            float baseFogDensity,
            Color ambientSkyColor,
            Color ambientEquatorColor,
            Color ambientGroundColor,
            float ambientIntensity,
            Color sunColor,
            float sunIntensity,
            Color skyTint,
            Color horizonColor,
            float skyExposure,
            bool useSkybox)
        {
            FogColor = fogColor;
            BaseFogDensity = baseFogDensity;
            AmbientSkyColor = ambientSkyColor;
            AmbientEquatorColor = ambientEquatorColor;
            AmbientGroundColor = ambientGroundColor;
            AmbientIntensity = ambientIntensity;
            SunColor = sunColor;
            SunIntensity = sunIntensity;
            SkyTint = skyTint;
            HorizonColor = horizonColor;
            SkyExposure = skyExposure;
            UseSkybox = useSkybox;
        }

        public Color FogColor { get; }
        public float BaseFogDensity { get; }
        public Color AmbientSkyColor { get; }
        public Color AmbientEquatorColor { get; }
        public Color AmbientGroundColor { get; }
        public float AmbientIntensity { get; }
        public Color SunColor { get; }
        public float SunIntensity { get; }
        public Color SkyTint { get; }
        public Color HorizonColor { get; }
        public float SkyExposure { get; }
        public bool UseSkybox { get; }
    }

    public static class WorldEnvironmentProfiles
    {
        private static readonly WeatherWeight[] QingshiWeather =
        {
            Weight(WeatherId.Clear, 70f),
            Weight(WeatherId.Rain, 20f),
            Weight(WeatherId.Fog, 10f)
        };

        private static readonly WeatherWeight[] CangwuWeather =
        {
            Weight(WeatherId.Clear, 50f),
            Weight(WeatherId.Rain, 20f),
            Weight(WeatherId.Fog, 30f)
        };

        private static readonly WeatherWeight[] BlackwindWeather =
        {
            Weight(WeatherId.Clear, 80f),
            Weight(WeatherId.Fog, 20f)
        };

        private static readonly WorldVisualProfile QingshiVisual =
            new WorldVisualProfile(
                new Color(0.56f, 0.63f, 0.58f, 1f),
                0.0035f,
                new Color(0.48f, 0.56f, 0.5f, 1f),
                new Color(0.31f, 0.34f, 0.28f, 1f),
                new Color(0.13f, 0.14f, 0.11f, 1f),
                0.72f,
                new Color(1f, 0.89f, 0.73f, 1f),
                0.78f,
                new Color(0.45f, 0.58f, 0.61f, 1f),
                new Color(0.7f, 0.69f, 0.58f, 1f),
                0.72f,
                true);

        private static readonly WorldVisualProfile CangwuVisual =
            new WorldVisualProfile(
                new Color(0.34f, 0.43f, 0.43f, 1f),
                0.0105f,
                new Color(0.34f, 0.45f, 0.44f, 1f),
                new Color(0.22f, 0.29f, 0.27f, 1f),
                new Color(0.09f, 0.12f, 0.11f, 1f),
                0.58f,
                new Color(0.72f, 0.84f, 0.8f, 1f),
                0.56f,
                new Color(0.29f, 0.43f, 0.45f, 1f),
                new Color(0.45f, 0.52f, 0.49f, 1f),
                0.58f,
                true);

        private static readonly WorldVisualProfile BlackwindVisual =
            new WorldVisualProfile(
                new Color(0.055f, 0.075f, 0.09f, 1f),
                0.0125f,
                new Color(0.18f, 0.23f, 0.27f, 1f),
                new Color(0.1f, 0.13f, 0.15f, 1f),
                new Color(0.04f, 0.05f, 0.06f, 1f),
                0.58f,
                new Color(0.58f, 0.69f, 0.76f, 1f),
                0.5f,
                new Color(0.045f, 0.065f, 0.085f, 1f),
                new Color(0.025f, 0.035f, 0.045f, 1f),
                0.3f,
                false);

        public static WeatherWeight[] GetWeatherPool(string sceneName)
        {
            WeatherWeight[] source;
            if (sceneName == SceneLoader.CangwuMapSceneName)
            {
                source = CangwuWeather;
            }
            else if (sceneName == SceneLoader.BlackwindDungeonSceneName)
            {
                source = BlackwindWeather;
            }
            else
            {
                source = QingshiWeather;
            }

            var copy = new WeatherWeight[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                copy[index] = Weight(
                    source[index].Weather,
                    source[index].Weight);
            }

            return copy;
        }

        public static WorldVisualProfile GetVisualProfile(string sceneName)
        {
            if (sceneName == SceneLoader.CangwuMapSceneName)
            {
                return CangwuVisual;
            }

            if (sceneName == SceneLoader.BlackwindDungeonSceneName)
            {
                return BlackwindVisual;
            }

            return QingshiVisual;
        }

        private static WeatherWeight Weight(WeatherId id, float weight)
        {
            return new WeatherWeight
            {
                Weather = id,
                Weight = weight
            };
        }
    }
}
