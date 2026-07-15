using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.Equipment
{
    public sealed class RefineSystem : SafeBehaviour, IRefineService
    {
        public const string MaterialItemId = InventoryContentIds.RefineStone;
        public const string NoEquipmentToastKey = "ui_refine_no_equipment";
        public const string NoEquipmentToastDefault = "该槽位没有可精炼装备。";
        public const string MaxLevelToastKey = "ui_refine_max_level";
        public const string MaxLevelToastDefault = "此装备已精炼至最高等级。";
        public const string MaterialMissingToastKey = "ui_refine_material_missing";
        public const string MaterialMissingToastDefault =
            "精炼石不足，需要 {0} 个。";
        public const string SuccessToastKey = "ui_refine_success";
        public const string SuccessToastDefault = "精炼成功，装备提升至 +{0}。";
        public const string FailureToastKey = "ui_refine_fail";
        public const string FailureToastDefault =
            "精炼失败，装备等级保持不变。";

        private bool _registeredService;
        private Func<float> _randomValueProvider = () => UnityEngine.Random.value;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IRefineService>(out IRefineService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IRefineService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            if (!ServiceLocator.TryGet<IRefineService>(out IRefineService current))
            {
                ServiceLocator.Register<IRefineService>(this);
                _registeredService = true;
            }
            else
            {
                _registeredService = ReferenceEquals(current, this);
            }
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<IRefineService>(out IRefineService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IRefineService>();
            }

            _registeredService = false;
        }

        public float GetSuccessRate(int currentLevel)
        {
            return FormulaLibrary.GetRefineSuccessRate(currentLevel);
        }

        public int GetRequiredMaterialCount(int currentLevel)
        {
            return FormulaLibrary.GetRefineMaterialCost(currentLevel);
        }

        public bool CanRefine(EquipmentSlot slot)
        {
            return TryResolveTarget(
                slot,
                out _,
                out IInventoryService inventory,
                out EquipmentInstance instance,
                out _,
                false)
                && inventory.CountItem(MaterialItemId)
                    >= GetRequiredMaterialCount(instance.RefineLevel);
        }

        public bool TryRefine(EquipmentSlot slot)
        {
            if (!TryResolveTarget(
                    slot,
                    out _,
                    out IInventoryService inventory,
                    out EquipmentInstance instance,
                    out EquipmentData equipment,
                    true))
            {
                return false;
            }

            int currentLevel = Math.Max(0, instance.RefineLevel);
            int materialCount = GetRequiredMaterialCount(currentLevel);
            if (inventory.CountItem(MaterialItemId) < materialCount
                || !inventory.RemoveItem(MaterialItemId, materialCount))
            {
                PublishToast(
                    MaterialMissingToastKey,
                    string.Format(MaterialMissingToastDefault, materialCount));
                return false;
            }

            float roll = _randomValueProvider != null
                ? _randomValueProvider.Invoke()
                : UnityEngine.Random.value;
            if (float.IsNaN(roll) || float.IsInfinity(roll))
            {
                roll = 1f;
            }

            bool success = Mathf.Clamp01(roll) < GetSuccessRate(currentLevel);
            if (success)
            {
                instance.RefineLevel = Math.Min(
                    equipment.MaxRefineLevel,
                    currentLevel + 1);
            }

            PersistAttempt(success);
            EventBus.Publish(
                InventoryEvents.EquipmentUpgraded,
                new EquipmentUpgradeInfo
                {
                    Slot = slot,
                    ItemId = equipment.Id ?? string.Empty,
                    NewRefineLevel = instance.RefineLevel,
                    Success = success
                });
            PublishToast(
                success ? SuccessToastKey : FailureToastKey,
                success
                    ? string.Format(SuccessToastDefault, instance.RefineLevel)
                    : FailureToastDefault);
            return success;
        }

        public void SetRandomValueProvider(Func<float> provider)
        {
            _randomValueProvider = provider ?? (() => UnityEngine.Random.value);
        }

        private static bool TryResolveTarget(
            EquipmentSlot slot,
            out IEquipmentService equipmentService,
            out IInventoryService inventory,
            out EquipmentInstance instance,
            out EquipmentData equipment,
            bool publishFailure)
        {
            equipmentService = null;
            inventory = null;
            instance = null;
            equipment = null;
            if (!ServiceLocator.TryGet(out equipmentService)
                || !ServiceLocator.TryGet(out inventory)
                || !equipmentService.Worn.TryGetValue(slot, out instance)
                || instance == null)
            {
                if (publishFailure)
                {
                    PublishToast(NoEquipmentToastKey, NoEquipmentToastDefault);
                }

                return false;
            }

            equipment = ConfigDatabase.Instance?.GetEquipment(
                instance.EquipmentDataId);
            if (equipment == null)
            {
                if (publishFailure)
                {
                    PublishToast(NoEquipmentToastKey, NoEquipmentToastDefault);
                }

                return false;
            }

            if (instance.RefineLevel >= Math.Max(0, equipment.MaxRefineLevel))
            {
                if (publishFailure)
                {
                    PublishToast(MaxLevelToastKey, MaxLevelToastDefault);
                }

                return false;
            }

            return true;
        }

        private static void PersistAttempt(bool equipmentChanged)
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null || saveManager.ActiveSlot < 0)
            {
                return;
            }

            saveManager.TrySaveModule(InventoryManager.SaveModuleName);
            if (equipmentChanged)
            {
                saveManager.TrySaveModule(EquipmentManager.SaveModuleName);
            }
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
    }
}
