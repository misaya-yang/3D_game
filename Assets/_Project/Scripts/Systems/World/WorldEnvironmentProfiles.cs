using Wendao.Data;

namespace Wendao.Systems.World
{
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
