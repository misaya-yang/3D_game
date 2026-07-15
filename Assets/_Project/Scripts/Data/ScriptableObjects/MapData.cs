using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Map_New", menuName = "问道/世界/MapData")]
    public class MapData : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public string SceneName;
        public int RecommendedRealm;
        public string[] TeleportPointIds = Array.Empty<string>();
        public WeatherWeight[] WeatherPool = Array.Empty<WeatherWeight>();
        public bool AllowFlight;
        public AudioClip DefaultBgm;
    }

    [Serializable]
    public class WeatherWeight
    {
        public WeatherId Weather;
        public float Weight;
    }
}
