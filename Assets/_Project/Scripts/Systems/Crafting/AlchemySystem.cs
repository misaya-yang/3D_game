using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.Crafting
{
    public sealed class AlchemySystem : SafeBehaviour, IAlchemyService
    {
        public const string SaveModuleName = "alchemy";
        public const float SuccessXpPerRecipeLevel = 100f;
        public const string SuccessToastKey = "ui_alchemy_success";
        public const string SuccessToastDefault = "炼制成功：{0} ×{1}";
        public const string FailureToastKey = "ui_alchemy_failure";
        public const string FailureToastDefault = "丹火失衡，炼制失败。";

        private Func<float> _randomValueProvider;
        private bool _registeredService;
        private bool _registeredSaveModule;
        private SaveManager _registeredSaveManager;

        public int Level { get; private set; } = 1;
        public float Xp { get; private set; }

        private void Awake()
        {
            if (ServiceLocator.TryGet<IAlchemyService>(out IAlchemyService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ResetProgress();
            _randomValueProvider = () => UnityEngine.Random.value;
            ServiceLocator.Register<IAlchemyService>(this);
            _registeredService = true;
            TryRegisterSaveModule();

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            RepairServiceRegistration();
            RepairSaveRegistration();
        }

        private void OnDestroy()
        {
            if (_registeredSaveModule && _registeredSaveManager != null)
            {
                _registeredSaveManager.UnregisterModule(SaveModuleName);
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            if (_registeredService
                && ServiceLocator.TryGet<IAlchemyService>(out IAlchemyService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IAlchemyService>();
            }

            _registeredService = false;
        }

        public bool CanCraft(string recipeId)
        {
            CraftRecipeData recipe = GetValidRecipe(recipeId);
            if (recipe == null
                || Level < recipe.RequiredCraftLevel
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return false;
            }

            foreach (CraftIngredient ingredient in recipe.Ingredients)
            {
                if (inventory.CountItem(ingredient.ItemId) < ingredient.Count)
                {
                    return false;
                }
            }

            return CanFitResultAfterConsumption(
                    inventory,
                    recipe.Ingredients,
                    recipe.SuccessResult)
                && CanFitResultAfterConsumption(
                    inventory,
                    GetFailureConsumedIngredients(recipe.Ingredients),
                    recipe.FailResult);
        }

        public float GetSuccessRate(string recipeId)
        {
            CraftRecipeData recipe = GetValidRecipe(recipeId);
            if (recipe == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(recipe.BaseSuccessRate + GetSuccessBonus(Level));
        }

        public bool Craft(string recipeId)
        {
            if (!CanCraft(recipeId)
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return false;
            }

            CraftRecipeData recipe = ConfigDatabase.Instance.GetRecipe(recipeId);
            var removedIngredients = new List<CraftIngredient>(
                recipe.Ingredients.Length);
            foreach (CraftIngredient ingredient in recipe.Ingredients)
            {
                if (!inventory.RemoveItem(ingredient.ItemId, ingredient.Count))
                {
                    RefundIngredients(inventory, removedIngredients, refundAll: true);
                    return false;
                }

                removedIngredients.Add(ingredient);
            }

            float roll = Mathf.Clamp01((_randomValueProvider
                ?? (() => UnityEngine.Random.value)).Invoke());
            bool succeeded = roll <= GetSuccessRate(recipeId);
            CraftResult result = succeeded ? recipe.SuccessResult : recipe.FailResult;
            int resultCount = ResolveResultCount(result);
            if (succeeded)
            {
                if (!TryGrantResult(inventory, result, resultCount))
                {
                    RefundIngredients(inventory, removedIngredients, refundAll: true);
                    return false;
                }

                AwardSuccessXp(recipe);
                var info = BuildResultInfo(recipe.Id, result, resultCount, true);
                EventBus.Publish(AlchemyEvents.CraftCompleted, info);
                ItemData item = ConfigDatabase.Instance.GetItem(info.ResultItemId);
                PublishToast(
                    SuccessToastKey,
                    string.Format(
                        SuccessToastDefault,
                        item?.DisplayName ?? info.ResultItemId,
                        info.ResultCount));
                PersistChanges();
                return true;
            }

            RefundIngredients(inventory, removedIngredients, refundAll: false);
            if (!TryGrantResult(inventory, result, resultCount))
            {
                result = null;
                resultCount = 0;
            }

            EventBus.Publish(
                AlchemyEvents.CraftFailed,
                BuildResultInfo(recipe.Id, result, resultCount, false));
            PublishToast(FailureToastKey, FailureToastDefault);
            PersistChanges();
            return false;
        }

        public void SetRandomValueProvider(Func<float> provider)
        {
            _randomValueProvider = provider ?? (() => UnityEngine.Random.value);
        }

        public AlchemySaveData CaptureSaveData()
        {
            return new AlchemySaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                Level = Level,
                Xp = Xp
            };
        }

        public void RestoreSaveData(AlchemySaveData data)
        {
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || data.Level < 1
                || data.Level > GetMaximumLevel()
                || data.Xp < 0f
                || !IsFinite(data.Xp)
                || data.Level != ResolveLevel(data.Xp))
            {
                throw new InvalidDataException("Alchemy save data is invalid.");
            }

            Level = data.Level;
            Xp = data.Xp;
        }

        private static CraftRecipeData GetValidRecipe(string recipeId)
        {
            CraftRecipeData recipe = ConfigDatabase.Instance?.GetRecipe(recipeId);
            if (recipe == null
                || recipe.CraftType != CraftType.Alchemy
                || recipe.RequiredCraftLevel < 1
                || recipe.BaseSuccessRate < 0f
                || recipe.BaseSuccessRate > 1f
                || recipe.Ingredients == null
                || recipe.Ingredients.Length == 0
                || !IsValidResult(recipe.SuccessResult))
            {
                return null;
            }

            foreach (CraftIngredient ingredient in recipe.Ingredients)
            {
                if (ingredient == null
                    || ingredient.Count <= 0
                    || ConfigDatabase.Instance.GetItem(ingredient.ItemId) == null)
                {
                    return null;
                }
            }

            return recipe.FailResult == null || IsValidResult(recipe.FailResult)
                ? recipe
                : null;
        }

        private static bool IsValidResult(CraftResult result)
        {
            return result != null
                && result.MinCount > 0
                && result.MaxCount >= result.MinCount
                && ConfigDatabase.Instance?.GetItem(result.ItemId) != null;
        }

        private static bool CanFitResultAfterConsumption(
            IInventoryService inventory,
            IReadOnlyList<CraftIngredient> ingredients,
            CraftResult result)
        {
            if (result == null)
            {
                return true;
            }

            ItemData resultItem = ConfigDatabase.Instance?.GetItem(result.ItemId);
            if (resultItem == null || resultItem.Type == ItemType.Equipment)
            {
                return false;
            }

            int slotCount = inventory.Slots.Count;
            var itemIds = new string[slotCount];
            var counts = new int[slotCount];
            var bound = new bool[slotCount];
            for (int index = 0; index < slotCount; index++)
            {
                InventorySlot slot = inventory.Slots[index];
                if (slot == null || slot.IsEmpty)
                {
                    continue;
                }

                itemIds[index] = slot.ItemId;
                counts[index] = slot.Count;
                bound[index] = slot.Bound;
            }

            foreach (CraftIngredient ingredient in ingredients)
            {
                int remaining = ingredient.Count;
                for (int index = slotCount - 1; index >= 0 && remaining > 0; index--)
                {
                    if (counts[index] <= 0
                        || !string.Equals(
                            itemIds[index],
                            ingredient.ItemId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int removed = Math.Min(counts[index], remaining);
                    counts[index] -= removed;
                    remaining -= removed;
                    if (counts[index] == 0)
                    {
                        itemIds[index] = string.Empty;
                    }
                }
            }

            long capacity = 0;
            int maxStack = Math.Max(1, resultItem.MaxStack);
            for (int index = 0; index < slotCount; index++)
            {
                if (counts[index] <= 0)
                {
                    capacity += maxStack;
                }
                else if (string.Equals(
                             itemIds[index],
                             result.ItemId,
                             StringComparison.Ordinal)
                    && bound[index] == resultItem.IsBound)
                {
                    capacity += Math.Max(0, maxStack - counts[index]);
                }

                if (capacity >= result.MaxCount)
                {
                    return true;
                }
            }

            return capacity >= result.MaxCount;
        }

        private static int ResolveResultCount(CraftResult result)
        {
            if (result == null)
            {
                return 0;
            }

            return result.MinCount == result.MaxCount
                ? result.MinCount
                : UnityEngine.Random.Range(result.MinCount, result.MaxCount + 1);
        }

        private static bool TryGrantResult(
            IInventoryService inventory,
            CraftResult result,
            int count)
        {
            return result == null
                || count == 0
                || inventory.AddItem(
                    result.ItemId,
                    count,
                    AcquireSource.Craft);
        }

        private static CraftResultInfo BuildResultInfo(
            string recipeId,
            CraftResult result,
            int count,
            bool success)
        {
            return new CraftResultInfo
            {
                RecipeId = recipeId,
                ResultItemId = result?.ItemId ?? string.Empty,
                ResultCount = result == null ? 0 : count,
                Success = success
            };
        }

        private static void RefundIngredients(
            IInventoryService inventory,
            IReadOnlyList<CraftIngredient> removed,
            bool refundAll)
        {
            for (int index = 0; index < removed.Count; index++)
            {
                CraftIngredient ingredient = removed[index];
                if (refundAll || !ingredient.ConsumedOnFail)
                {
                    inventory.RestoreItem(ingredient.ItemId, ingredient.Count);
                }
            }
        }

        private static IReadOnlyList<CraftIngredient> GetFailureConsumedIngredients(
            IReadOnlyList<CraftIngredient> ingredients)
        {
            var consumed = new List<CraftIngredient>(ingredients.Count);
            for (int index = 0; index < ingredients.Count; index++)
            {
                if (ingredients[index].ConsumedOnFail)
                {
                    consumed.Add(ingredients[index]);
                }
            }

            return consumed;
        }

        private void AwardSuccessXp(CraftRecipeData recipe)
        {
            Xp += SuccessXpPerRecipeLevel
                * Mathf.Max(1, recipe.RequiredCraftLevel);
            Level = ResolveLevel(Xp);
        }

        private static float GetSuccessBonus(int level)
        {
            CraftLevelEntry entry = GetLevelEntry(level);
            return Mathf.Max(0f, entry?.SuccessBonus ?? 0f);
        }

        private static CraftLevelEntry GetLevelEntry(int level)
        {
            CraftLevelEntry[] entries = ConfigDatabase.Instance?.Craft?.Alchemy;
            if (entries == null)
            {
                return null;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index] != null && entries[index].Level == level)
                {
                    return entries[index];
                }
            }

            return null;
        }

        private static int ResolveLevel(float xp)
        {
            CraftLevelEntry[] entries = ConfigDatabase.Instance?.Craft?.Alchemy;
            int resolved = 1;
            if (entries == null)
            {
                return resolved;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                CraftLevelEntry entry = entries[index];
                if (entry != null && xp + 0.0001f >= entry.XpRequired)
                {
                    resolved = Math.Max(resolved, entry.Level);
                }
            }

            return resolved;
        }

        private static int GetMaximumLevel()
        {
            CraftLevelEntry[] entries = ConfigDatabase.Instance?.Craft?.Alchemy;
            int maximum = 1;
            if (entries == null)
            {
                return maximum;
            }

            for (int index = 0; index < entries.Length; index++)
            {
                maximum = Math.Max(maximum, entries[index]?.Level ?? 1);
            }

            return maximum;
        }

        private bool TryRegisterSaveModule()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
            {
                return false;
            }

            _registeredSaveModule = saveManager.RegisterModule(
                SaveModuleName,
                CaptureSaveData,
                RestoreSaveData,
                ResetProgress);
            if (_registeredSaveModule)
            {
                _registeredSaveManager = saveManager;
            }

            return _registeredSaveModule;
        }

        private void RepairSaveRegistration()
        {
            SaveManager current = SaveManager.Instance;
            if (_registeredSaveManager == current && _registeredSaveModule)
            {
                return;
            }

            _registeredSaveModule = false;
            _registeredSaveManager = null;
            TryRegisterSaveModule();
        }

        private void RepairServiceRegistration()
        {
            if (ServiceLocator.TryGet<IAlchemyService>(out IAlchemyService current))
            {
                _registeredService = ReferenceEquals(current, this);
                return;
            }

            ServiceLocator.Register<IAlchemyService>(this);
            _registeredService = true;
        }

        private void ResetProgress()
        {
            Level = 1;
            Xp = 0f;
        }

        private static void PersistChanges()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || saveManager.ActiveSlot < 0)
            {
                return;
            }

            saveManager.TrySaveModule(InventoryManager.SaveModuleName);
            saveManager.TrySaveModule(SaveModuleName);
        }

        private static void PublishToast(string key, string defaultValue)
        {
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = key,
                    DefaultValue = defaultValue,
                    Duration = 3f
                });
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
