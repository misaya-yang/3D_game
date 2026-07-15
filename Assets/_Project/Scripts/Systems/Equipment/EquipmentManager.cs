using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Inventory;

namespace Wendao.Systems.Equipment
{
    public sealed class EquipmentManager : SafeBehaviour, IEquipmentService
    {
        public const string SaveModuleName = "equipment";
        public const string RealmRequiredToastKey = "ui_equipment_realm_required";
        public const string RealmRequiredToastDefault = "当前境界不足，无法装备。";
        public const string InventoryFullToastKey = "ui_inventory_full";
        public const string InventoryFullToastDefault = "背包已满，无法卸下装备。";
        public const string CannotEquipToastKey = "ui_equipment_unavailable";
        public const string CannotEquipToastDefault = "此装备当前无法穿戴。";

        private readonly Dictionary<EquipmentSlot, EquipmentInstance> _worn =
            new Dictionary<EquipmentSlot, EquipmentInstance>();
        private readonly Dictionary<EquipmentSlot, string> _wornItemIds =
            new Dictionary<EquipmentSlot, string>();
        private readonly Dictionary<EquipmentSlot, bool> _wornBound =
            new Dictionary<EquipmentSlot, bool>();

        private ReadOnlyDictionary<EquipmentSlot, EquipmentInstance> _readOnlyWorn;
        private bool _registeredService;
        private bool _registeredSaveModule;

        public IReadOnlyDictionary<EquipmentSlot, EquipmentInstance> Worn =>
            _readOnlyWorn;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IEquipmentService>(out IEquipmentService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            _readOnlyWorn = new ReadOnlyDictionary<EquipmentSlot, EquipmentInstance>(
                _worn);
            ServiceLocator.Register<IEquipmentService>(this);
            _registeredService = true;
            TryRegisterSaveModule();

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            if (!_registeredSaveModule)
            {
                TryRegisterSaveModule();
            }
        }

        private void OnDestroy()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (_registeredSaveModule && saveManager != null)
            {
                saveManager.UnregisterModule(SaveModuleName);
            }

            _registeredSaveModule = false;
            if (_registeredService
                && ServiceLocator.TryGet<IEquipmentService>(out IEquipmentService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IEquipmentService>();
            }

            _registeredService = false;
        }

        public bool EquipFromInventory(int inventorySlot)
        {
            if (!ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory)
                || inventorySlot < 0
                || inventorySlot >= inventory.Slots.Count)
            {
                PublishToast(CannotEquipToastKey, CannotEquipToastDefault);
                return false;
            }

            InventorySlot sourceSlot = inventory.Slots[inventorySlot];
            ItemData item = ConfigDatabase.Instance?.GetItem(sourceSlot?.ItemId);
            EquipmentData equipment = item == null
                ? null
                : ConfigDatabase.Instance?.GetEquipment(item.EquipmentDataId);
            if (sourceSlot == null
                || sourceSlot.IsEmpty
                || item == null
                || item.Type != ItemType.Equipment
                || equipment == null
                || equipment.Slot != EquipmentSlot.Weapon)
            {
                PublishToast(CannotEquipToastKey, CannotEquipToastDefault);
                return false;
            }

            if (!MeetsRealmRequirement(Math.Max(item.RequiredRealm, equipment.RequiredRealm)))
            {
                PublishToast(RealmRequiredToastKey, RealmRequiredToastDefault);
                return false;
            }

            if (!inventory.TryTakeEquipmentAt(
                    inventorySlot,
                    out InventorySlot takenSlot,
                    out EquipmentInstance newInstance))
            {
                PublishToast(CannotEquipToastKey, CannotEquipToastDefault);
                return false;
            }

            _worn.TryGetValue(equipment.Slot, out EquipmentInstance oldInstance);
            string oldItemId = _wornItemIds.TryGetValue(
                equipment.Slot,
                out string storedOldItemId)
                ? storedOldItemId
                : oldInstance?.EquipmentDataId ?? string.Empty;
            bool oldBound = _wornBound.TryGetValue(
                equipment.Slot,
                out bool storedOldBound) && storedOldBound;

            if (oldInstance != null
                && !inventory.TryStoreEquipment(
                    oldItemId,
                    oldInstance,
                    oldBound,
                    inventorySlot))
            {
                inventory.TryStoreEquipment(
                    takenSlot.ItemId,
                    newInstance,
                    takenSlot.Bound,
                    inventorySlot);
                PublishToast(InventoryFullToastKey, InventoryFullToastDefault);
                return false;
            }

            _worn[equipment.Slot] = newInstance;
            _wornItemIds[equipment.Slot] = item.Id;
            _wornBound[equipment.Slot] = takenSlot.Bound;
            PublishEquipmentChanged(equipment.Slot, oldItemId, item.Id);
            return true;
        }

        public bool Unequip(EquipmentSlot slot)
        {
            if (!_worn.TryGetValue(slot, out EquipmentInstance instance)
                || !ServiceLocator.TryGet<IInventoryService>(
                    out IInventoryService inventory))
            {
                return false;
            }

            string itemId = _wornItemIds.TryGetValue(slot, out string storedItemId)
                ? storedItemId
                : instance.EquipmentDataId;
            bool bound = _wornBound.TryGetValue(slot, out bool storedBound)
                && storedBound;
            if (!inventory.TryStoreEquipment(itemId, instance, bound))
            {
                PublishToast(InventoryFullToastKey, InventoryFullToastDefault);
                return false;
            }

            _worn.Remove(slot);
            _wornItemIds.Remove(slot);
            _wornBound.Remove(slot);
            PublishEquipmentChanged(slot, itemId, string.Empty);
            return true;
        }

        public int GetRefineLevel(EquipmentSlot slot)
        {
            return _worn.TryGetValue(slot, out EquipmentInstance instance)
                ? Math.Max(0, instance.RefineLevel)
                : 0;
        }

        public StatBlock GetEquipmentStats()
        {
            var total = new StatBlock { CritDamage = 0f };
            foreach (EquipmentInstance instance in _worn.Values)
            {
                EquipmentData equipment = ConfigDatabase.Instance?.GetEquipment(
                    instance.EquipmentDataId);
                if (equipment?.BaseStats == null)
                {
                    continue;
                }

                float multiplier = FormulaLibrary.GetRefineStatMultiplier(
                    Math.Max(0, instance.RefineLevel));
                total += equipment.BaseStats.Multiply(multiplier);
            }

            return total;
        }

        public EquipmentSaveData CaptureSaveData()
        {
            var records = new List<EquippedItemRecord>();
            foreach (KeyValuePair<EquipmentSlot, EquipmentInstance> pair in _worn
                         .OrderBy(entry => entry.Key))
            {
                records.Add(new EquippedItemRecord
                {
                    Slot = pair.Key,
                    ItemId = _wornItemIds.TryGetValue(pair.Key, out string itemId)
                        ? itemId
                        : pair.Value.EquipmentDataId,
                    Bound = _wornBound.TryGetValue(pair.Key, out bool bound) && bound,
                    Instance = InventoryManager.CloneEquipmentInstance(pair.Value)
                });
            }

            return new EquipmentSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                Worn = records
            };
        }

        public void RestoreSaveData(EquipmentSaveData data)
        {
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || data.Worn == null)
            {
                throw new InvalidDataException("Equipment save data is invalid.");
            }

            var restored = new Dictionary<EquipmentSlot, EquipmentInstance>();
            var restoredIds = new Dictionary<EquipmentSlot, string>();
            var restoredBound = new Dictionary<EquipmentSlot, bool>();
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EquippedItemRecord record in data.Worn)
            {
                EquipmentInstance instance = record?.Instance;
                string itemId = string.IsNullOrEmpty(record?.ItemId)
                    ? instance?.EquipmentDataId
                    : record.ItemId;
                ItemData item = ConfigDatabase.Instance?.GetItem(itemId);
                EquipmentData equipment = instance == null
                    ? null
                    : ConfigDatabase.Instance?.GetEquipment(instance.EquipmentDataId);
                if (record == null
                    || instance == null
                    || string.IsNullOrEmpty(instance.InstanceId)
                    || !instanceIds.Add(instance.InstanceId)
                    || equipment == null
                    || item == null
                    || item.Type != ItemType.Equipment
                    || !string.Equals(
                        item.EquipmentDataId,
                        instance.EquipmentDataId,
                        StringComparison.Ordinal)
                    || equipment.Slot != record.Slot
                    || record.Slot != EquipmentSlot.Weapon
                    || restored.ContainsKey(record.Slot)
                    || instance.RefineLevel < 0
                    || instance.RefineLevel > equipment.MaxRefineLevel
                    || instance.Durability < 0
                    || instance.Durability > Math.Max(0, equipment.MaxDurability)
                    || instance.GemIds == null
                    || instance.GemIds.Length > Math.Max(0, equipment.MaxGemSockets))
                {
                    throw new InvalidDataException(
                        "Equipment save contains an invalid worn item.");
                }

                restored.Add(
                    record.Slot,
                    InventoryManager.CloneEquipmentInstance(instance));
                restoredIds.Add(record.Slot, itemId);
                restoredBound.Add(record.Slot, record.Bound);
            }

            string oldWeapon = _wornItemIds.TryGetValue(
                EquipmentSlot.Weapon,
                out string previousWeapon)
                ? previousWeapon
                : string.Empty;
            _worn.Clear();
            _wornItemIds.Clear();
            _wornBound.Clear();
            foreach (KeyValuePair<EquipmentSlot, EquipmentInstance> pair in restored)
            {
                _worn.Add(pair.Key, pair.Value);
                _wornItemIds.Add(pair.Key, restoredIds[pair.Key]);
                _wornBound.Add(pair.Key, restoredBound[pair.Key]);
            }

            string newWeapon = _wornItemIds.TryGetValue(
                EquipmentSlot.Weapon,
                out string restoredWeapon)
                ? restoredWeapon
                : string.Empty;
            // Restore can change instance/refine data while retaining the same item ID,
            // so listeners must always recompute their aggregated stats.
            PublishEquipmentChanged(
                EquipmentSlot.Weapon,
                oldWeapon,
                newWeapon);
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
                ResetForLoad);
            return _registeredSaveModule;
        }

        private void ResetForLoad()
        {
            string oldWeapon = _wornItemIds.TryGetValue(
                EquipmentSlot.Weapon,
                out string previousWeapon)
                ? previousWeapon
                : string.Empty;
            _worn.Clear();
            _wornItemIds.Clear();
            _wornBound.Clear();
            PublishEquipmentChanged(
                EquipmentSlot.Weapon,
                oldWeapon,
                string.Empty);
        }

        private static bool MeetsRealmRequirement(int requiredRealm)
        {
            if (requiredRealm <= 0)
            {
                return true;
            }

            int realm = SaveManager.Instance?.Profile?.Realm
                ?? (int)RealmType.QiCondensation;
            return realm >= requiredRealm;
        }

        private static void PublishEquipmentChanged(
            EquipmentSlot slot,
            string oldItemId,
            string newItemId)
        {
            EventBus.Publish(
                InventoryEvents.EquipmentChanged,
                new EquipmentChangeInfo
                {
                    Slot = slot,
                    OldItemId = oldItemId ?? string.Empty,
                    NewItemId = newItemId ?? string.Empty
                });
        }

        private static void PublishToast(string key, string defaultValue)
        {
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = key,
                    DefaultValue = defaultValue,
                    Duration = 2.5f
                });
        }
    }
}
