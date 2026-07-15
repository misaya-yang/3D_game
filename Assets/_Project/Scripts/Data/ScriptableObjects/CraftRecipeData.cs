using System;
using UnityEngine;

namespace Wendao.Data
{
    [CreateAssetMenu(fileName = "Recipe_New", menuName = "问道/配方/CraftRecipeData")]
    public class CraftRecipeData : ScriptableObject
    {
        public string Id;
        public string DisplayNameKey;
        public string DisplayName;
        public CraftType CraftType;
        public int RequiredCraftLevel;
        public float BaseSuccessRate;
        public float CraftTime;
        public CraftIngredient[] Ingredients = Array.Empty<CraftIngredient>();
        public CraftResult SuccessResult = new CraftResult();
        public CraftResult FailResult;
    }

    [Serializable]
    public class CraftIngredient
    {
        public string ItemId;
        public int Count;
        public bool ConsumedOnFail = true;
    }

    [Serializable]
    public class CraftResult
    {
        public string ItemId;
        public int MinCount;
        public int MaxCount;
    }
}
