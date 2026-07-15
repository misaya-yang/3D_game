namespace Wendao.Systems.Crafting
{
    public interface IAlchemyService
    {
        int Level { get; }
        float Xp { get; }

        bool CanCraft(string recipeId);
        float GetSuccessRate(string recipeId);
        bool Craft(string recipeId);
    }
}
