namespace Wendao.Systems.Enemy
{
    public interface IEnemySpawnService
    {
        bool TrySpawn(string enemyId, int count, out int spawnedCount);
        bool TryDefeatAll(out int defeatedCount);
    }
}
