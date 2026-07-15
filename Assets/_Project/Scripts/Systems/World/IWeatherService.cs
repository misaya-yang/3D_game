using Wendao.Data;

namespace Wendao.Systems.World
{
    public interface IWeatherService
    {
        WeatherId Current { get; }
        WeatherId Target { get; }
        float Intensity { get; }
        bool IsTransitioning { get; }
        float DurationRemaining { get; }

        void ForceWeather(WeatherId id, float duration);
        void TickWeather(float deltaTime);
        void ConfigureForScene(string sceneName);
        float GetElementDamageBonus(ElementType element);
        float GetVisionMul();
        float GetFlightSpeedMul();
    }
}
