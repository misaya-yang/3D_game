using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Equipment;

namespace Wendao.Systems.Inventory
{
    public sealed class InventoryManager : SafeBehaviour, IInventoryService
    {
        public const int Capacity = 50;
        public const string SaveModuleName = "inventory";

        private readonly List<InventorySlot> _slots = new List<InventorySlot>(Capacity);
        private readonly Dictionary<string, EquipmentInstance> _equipmentInstances =
            new Dictionary<string, EquipmentInstance>(StringComparer.Ordinal);

        private ReadOnlyCollection<InventorySlot> _readOnlySlots;
        private bool _registeredService;
        private bool _registeredSaveModule;

        public IReadOnlyList<InventorySlot> Slots => _readOnlySlots;
        public int SpiritStones { get; private set; }

        private void Awake()
        {
            if (ServiceLocator.TryGet<IInventoryService>(out IInventoryService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ResetInventory();
            _readOnlySlots = _slots.AsReadOnly();
            ServiceLocator.Register<IInventoryService>(this);
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
                && ServiceLocator.TryGet<IInventoryService>(out IInventoryService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IInventoryService>();
            }

            _registeredService = false;
        }

        public bool CanAdd(string itemId, int count)
        {
            ItemData item = GetItem(itemId);
            if (item == null || count <= 0)
            {
                return false;
            }

            if (item.Type == ItemType.Equipment)
            {
                return CountEmptySlots() >= count;
            }

            int remaining = count;
            int maxStack = Math.Max(1, item.MaxStack);
            foreach (InventorySlot slot in _slots)
            {
                if (!slot.IsEmpty
                    && string.Equals(slot.ItemId, itemId, StringComparison.Ordinal)
                    && slot.Bound == item.IsBound)
                {
                    remaining -= Math.Max(0, maxStack - slot.Count);
                    if (remaining <= 0)
                    {
                        return true;
                    }
                }
            }

            long emptyCapacity = (long)CountEmptySlots() * maxStack;
            return emptyCapacity >= remaining;
        }

        public bool AddItem(
            string itemId,
            int count,
            AcquireSource source,
            string instanceId = null)
        {
            ItemData item = GetItem(itemId);
            if (item == null
                || count <= 0
                || (item.Type != ItemType.Equipment && !string.IsNullOrEmpty(instanceId))
                || !CanAdd(itemId, count))
            {
                return false;
            }

            if (item.Type == ItemType.Equipment)
            {
                if (count > 1 && !string.IsNullOrEmpty(instanceId))
                {
                    return false;
                }

                if (!CanUseEquipmentInstance(item, instanceId))
                {
                    return false;
                }

                for (int index = 0; index < count; index++)
                {
                    EquipmentInstance instance = index == 0
                        ? ResolveOrCreateEquipmentInstance(item, instanceId)
                        : ResolveOrCreateEquipmentInstance(item, null);
                    if (instance == null || !TryPlaceEquipment(item, instance, -1))
                    {
                        return false;
                    }
                }
            }
            else
            {
                AddStackable(item, count);
            }

            EventBus.Publish(
                InventoryEvents.ItemAcquired,
                new ItemAcquireInfo
                {
                    ItemId = itemId,
                    Count = count,
                    Source = source
                });
            return true;
        }

        public bool RemoveItem(string itemId, int count)
        {
            if (count <= 0 || CountItem(itemId) < count)
            {
                return false;
            }

            int remaining = count;
            for (int index = _slots.Count - 1; index >= 0 && remaining > 0; index--)
            {
                InventorySlot slot = _slots[index];
                if (slot.IsEmpty
                    || !string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                int removed = Math.Min(slot.Count, remaining);
                RemoveAtInternal(index, removed);
                remaining -= removed;
            }

            return remaining == 0;
        }

        public bool RestoreItem(string itemId, int count)
        {
            ItemData item = GetItem(itemId);
            if (item == null
                || item.Type == ItemType.Equipment
                || count <= 0
                || !CanAdd(itemId, count))
            {
                return false;
            }

            AddStackable(item, count);
            return true;
        }

        public bool RemoveAt(int slotIndex, int count)
        {
            if (!IsValidSlot(slotIndex)
                || count <= 0
                || _slots[slotIndex].IsEmpty
                || count > _slots[slotIndex].Count)
            {
                return false;
            }

            RemoveAtInternal(slotIndex, count);
            return true;
        }

        public int CountItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            int count = 0;
            foreach (InventorySlot slot in _slots)
            {
                if (!slot.IsEmpty
                    && string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                {
                    count += slot.Count;
                }
            }

            return count;
        }

        public void Sort()
        {
            List<InventorySlot> occupied = _slots
                .Where(slot => slot != null && !slot.IsEmpty)
                .OrderBy(slot => GetItem(slot.ItemId)?.Type ?? ItemType.Currency)
                .ThenBy(slot => GetItem(slot.ItemId)?.Rarity ?? ItemRarity.Common)
                .ThenBy(slot => slot.ItemId, StringComparer.Ordinal)
                .ToList();

            _slots.Clear();
            _slots.AddRange(occupied);
            while (_slots.Count < Capacity)
            {
                _slots.Add(CreateEmptySlot());
            }
        }

        public bool AddSpiritStones(int delta)
        {
            int previous = SpiritStones;
            long next = (long)SpiritStones + delta;
            if (next < 0 || next > int.MaxValue)
            {
                return false;
            }

            SpiritStones = (int)next;
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                saveManager.Profile.SpiritStones = SpiritStones;
            }

            if (SpiritStones != previous)
            {
                EventBus.Publish(
                    InventoryEvents.CurrencyChanged,
                    new CurrencyChangeInfo
                    {
                        Previous = previous,
                        Current = SpiritStones,
                        Delta = SpiritStones - previous
                    });
            }

            return true;
        }

        public EquipmentInstance GetEquipmentInstance(string instanceId)
        {
            return !string.IsNullOrEmpty(instanceId)
                && _equipmentInstances.TryGetValue(instanceId, out EquipmentInstance instance)
                ? instance
                : null;
        }

        public EquipmentInstance CreateEquipmentInstance(string equipmentDataId)
        {
            EquipmentData equipment = ConfigDatabase.Instance?.GetEquipment(equipmentDataId);
            if (equipment == null)
            {
                return null;
            }

            string instanceId;
            do
            {
                instanceId = Guid.NewGuid().ToString("N");
            }
            while (_equipmentInstances.ContainsKey(instanceId)
                || IsEquippedInstanceId(instanceId));

            var instance = new EquipmentInstance
            {
                InstanceId = instanceId,
                EquipmentDataId = equipment.Id,
                RefineLevel = 0,
                Durability = Math.Max(0, equipment.MaxDurability),
                GemIds = Array.Empty<string>()
            };
            _equipmentInstances.Add(instance.InstanceId, instance);
            return instance;
        }

        public bool TryTakeEquipmentAt(
            int slotIndex,
            out InventorySlot slot,
            out EquipmentInstance instance)
        {
            slot = null;
            instance = null;
            if (!IsValidSlot(slotIndex))
            {
                return false;
            }

            InventorySlot current = _slots[slotIndex];
            ItemData item = GetItem(current.ItemId);
            if (current.IsEmpty
                || item == null
                || item.Type != ItemType.Equipment
                || string.IsNullOrEmpty(current.InstanceId)
                || !_equipmentInstances.TryGetValue(
                    current.InstanceId,
                    out EquipmentInstance currentInstance)
                || !string.Equals(
                    item.EquipmentDataId,
                    currentInstance.EquipmentDataId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            slot = CloneSlot(current);
            instance = currentInstance;
            _equipmentInstances.Remove(current.InstanceId);
            _slots[slotIndex] = CreateEmptySlot();
            return true;
        }

        public bool TryStoreEquipment(
            string itemId,
            EquipmentInstance instance,
            bool bound,
            int preferredSlot = -1)
        {
            ItemData item = GetItem(itemId);
            EquipmentData equipment = instance == null
                ? null
                : ConfigDatabase.Instance?.GetEquipment(instance.EquipmentDataId);
            if (item == null
                || item.Type != ItemType.Equipment
                || instance == null
                || equipment == null
                || string.IsNullOrEmpty(instance.InstanceId)
                || string.IsNullOrEmpty(instance.EquipmentDataId)
                || !string.Equals(
                    item.EquipmentDataId,
                    instance.EquipmentDataId,
                    StringComparison.Ordinal)
                || _equipmentInstances.ContainsKey(instance.InstanceId)
                || instance.RefineLevel < 0
                || instance.RefineLevel > equipment.MaxRefineLevel
                || instance.Durability < 0
                || instance.Durability > Math.Max(0, equipment.MaxDurability)
                || instance.GemIds == null
                || instance.GemIds.Length > Math.Max(0, equipment.MaxGemSockets))
            {
                return false;
            }

            int target = IsValidSlot(preferredSlot) && _slots[preferredSlot].IsEmpty
                ? preferredSlot
                : FindEmptySlot();
            if (target < 0)
            {
                return false;
            }

            _equipmentInstances.Add(instance.InstanceId, instance);
            _slots[target] = new InventorySlot
            {
                ItemId = itemId,
                Count = 1,
                Bound = bound,
                InstanceId = instance.InstanceId,
                ExtraJson = string.Empty
            };
            return true;
        }

        public InventorySaveData CaptureSaveData()
        {
            return new InventorySaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                Slots = _slots.Select(CloneSlot).ToList(),
                SpiritStones = SpiritStones,
                EquipmentInstances = _equipmentInstances.Values
                    .OrderBy(instance => instance.InstanceId, StringComparer.Ordinal)
                    .Select(CloneEquipmentInstance)
                    .ToList()
            };
        }

        public void RestoreSaveData(InventorySaveData data)
        {
            if (data == null
                || data.SchemaVersion != SaveSchema.CurrentVersion
                || data.Slots == null
                || data.EquipmentInstances == null
                || data.Slots.Count > Capacity
                || data.SpiritStones < 0)
            {
                throw new InvalidDataException("Inventory save data is invalid.");
            }

            var restoredInstances = new Dictionary<string, EquipmentInstance>(
                StringComparer.Ordinal);
            foreach (EquipmentInstance savedInstance in data.EquipmentInstances)
            {
                if (!IsValidEquipmentInstance(savedInstance)
                    || restoredInstances.ContainsKey(savedInstance.InstanceId))
                {
                    throw new InvalidDataException(
                        "Inventory save contains an invalid or duplicate equipment instance.");
                }

                restoredInstances.Add(
                    savedInstance.InstanceId,
                    CloneEquipmentInstance(savedInstance));
            }

            var referencedInstances = new HashSet<string>(StringComparer.Ordinal);
            var restoredSlots = new List<InventorySlot>(Capacity);
            foreach (InventorySlot savedSlot in data.Slots)
            {
                if (savedSlot == null || savedSlot.IsEmpty)
                {
                    restoredSlots.Add(CreateEmptySlot());
                    continue;
                }

                ItemData item = GetItem(savedSlot.ItemId);
                if (item == null
                    || savedSlot.Count <= 0
                    || savedSlot.Count > Math.Max(1, item.MaxStack))
                {
                    throw new InvalidDataException("Inventory save contains an invalid slot.");
                }

                if (item.Type == ItemType.Equipment)
                {
                    if (savedSlot.Count != 1
                        || string.IsNullOrEmpty(savedSlot.InstanceId)
                        || !restoredInstances.TryGetValue(
                            savedSlot.InstanceId,
                            out EquipmentInstance instance)
                        || !string.Equals(
                            item.EquipmentDataId,
                            instance.EquipmentDataId,
                            StringComparison.Ordinal)
                        || !referencedInstances.Add(savedSlot.InstanceId))
                    {
                        throw new InvalidDataException(
                            "Inventory equipment slot does not match its instance.");
                    }
                }
                else if (!string.IsNullOrEmpty(savedSlot.InstanceId))
                {
                    throw new InvalidDataException(
                        "A stackable inventory slot cannot reference equipment.");
                }

                restoredSlots.Add(CloneSlot(savedSlot));
            }

            while (restoredSlots.Count < Capacity)
            {
                restoredSlots.Add(CreateEmptySlot());
            }

            _slots.Clear();
            _slots.AddRange(restoredSlots);
            _equipmentInstances.Clear();
            foreach (KeyValuePair<string, EquipmentInstance> pair in restoredInstances)
            {
                _equipmentInstances.Add(pair.Key, pair.Value);
            }

            SpiritStones = data.SpiritStones;
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null)
            {
                saveManager.Profile.SpiritStones = SpiritStones;
            }
        }

        private void AddStackable(ItemData item, int count)
        {
            int remaining = count;
            int maxStack = Math.Max(1, item.MaxStack);
            foreach (InventorySlot slot in _slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (slot.IsEmpty
                    || !string.Equals(slot.ItemId, item.Id, StringComparison.Ordinal)
                    || slot.Bound != item.IsBound
                    || slot.Count >= maxStack)
                {
                    continue;
                }

                int added = Math.Min(remaining, maxStack - slot.Count);
                slot.Count += added;
                remaining -= added;
            }

            while (remaining > 0)
            {
                int target = FindEmptySlot();
                int added = Math.Min(remaining, maxStack);
                _slots[target] = new InventorySlot
                {
                    ItemId = item.Id,
                    Count = added,
                    Bound = item.IsBound,
                    InstanceId = string.Empty,
                    ExtraJson = string.Empty
                };
                remaining -= added;
            }
        }

        private bool CanUseEquipmentInstance(ItemData item, string instanceId)
        {
            if (string.IsNullOrEmpty(item.EquipmentDataId)
                || ConfigDatabase.Instance?.GetEquipment(item.EquipmentDataId) == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(instanceId))
            {
                return true;
            }

            if (IsEquippedInstanceId(instanceId))
            {
                return false;
            }

            if (!_equipmentInstances.TryGetValue(instanceId, out EquipmentInstance instance))
            {
                return !IsInstanceInSlot(instanceId);
            }

            return !IsInstanceInSlot(instanceId)
                && string.Equals(
                    instance.EquipmentDataId,
                    item.EquipmentDataId,
                    StringComparison.Ordinal);
        }

        private EquipmentInstance ResolveOrCreateEquipmentInstance(
            ItemData item,
            string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return CreateEquipmentInstance(item.EquipmentDataId);
            }

            if (_equipmentInstances.TryGetValue(instanceId, out EquipmentInstance existing))
            {
                return existing;
            }

            EquipmentData equipment = ConfigDatabase.Instance?.GetEquipment(
                item.EquipmentDataId);
            if (equipment == null)
            {
                return null;
            }

            var created = new EquipmentInstance
            {
                InstanceId = instanceId,
                EquipmentDataId = equipment.Id,
                RefineLevel = 0,
                Durability = Math.Max(0, equipment.MaxDurability),
                GemIds = Array.Empty<string>()
            };
            _equipmentInstances.Add(instanceId, created);
            return created;
        }

        private bool TryPlaceEquipment(
            ItemData item,
            EquipmentInstance instance,
            int preferredSlot)
        {
            int target = IsValidSlot(preferredSlot) && _slots[preferredSlot].IsEmpty
                ? preferredSlot
                : FindEmptySlot();
            if (target < 0 || instance == null)
            {
                return false;
            }

            _slots[target] = new InventorySlot
            {
                ItemId = item.Id,
                Count = 1,
                Bound = item.IsBound,
                InstanceId = instance.InstanceId,
                ExtraJson = string.Empty
            };
            return true;
        }

        private void RemoveAtInternal(int slotIndex, int count)
        {
            InventorySlot slot = _slots[slotIndex];
            slot.Count -= count;
            if (slot.Count > 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(slot.InstanceId))
            {
                _equipmentInstances.Remove(slot.InstanceId);
            }

            _slots[slotIndex] = CreateEmptySlot();
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
            ResetInventory();
            SaveManager saveManager = SaveManager.Instance;
            SpiritStones = Math.Max(0, saveManager?.Profile?.SpiritStones ?? 0);
        }

        private void ResetInventory()
        {
            _slots.Clear();
            for (int index = 0; index < Capacity; index++)
            {
                _slots.Add(CreateEmptySlot());
            }

            _equipmentInstances.Clear();
            SpiritStones = 0;
        }

        private int CountEmptySlots()
        {
            int count = 0;
            foreach (InventorySlot slot in _slots)
            {
                if (slot.IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        private int FindEmptySlot()
        {
            for (int index = 0; index < _slots.Count; index++)
            {
                if (_slots[index].IsEmpty)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool IsInstanceInSlot(string instanceId)
        {
            return _slots.Any(slot => !slot.IsEmpty
                && string.Equals(slot.InstanceId, instanceId, StringComparison.Ordinal));
        }

        private static ItemData GetItem(string itemId)
        {
            return ConfigDatabase.Instance?.GetItem(itemId);
        }

        private bool IsValidEquipmentInstance(EquipmentInstance instance)
        {
            if (instance == null
                || string.IsNullOrEmpty(instance.InstanceId)
                || string.IsNullOrEmpty(instance.EquipmentDataId)
                || IsEquippedInstanceId(instance.InstanceId))
            {
                return false;
            }

            EquipmentData equipment = ConfigDatabase.Instance?.GetEquipment(
                instance.EquipmentDataId);
            return equipment != null
                && instance.RefineLevel >= 0
                && instance.RefineLevel <= equipment.MaxRefineLevel
                && instance.Durability >= 0
                && instance.Durability <= Math.Max(0, equipment.MaxDurability)
                && instance.GemIds != null
                && instance.GemIds.Length <= Math.Max(0, equipment.MaxGemSockets);
        }

        private static bool IsEquippedInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)
                || !ServiceLocator.TryGet<IEquipmentService>(
                    out IEquipmentService equipment))
            {
                return false;
            }

            return equipment.Worn.Values.Any(instance => instance != null
                && string.Equals(
                    instance.InstanceId,
                    instanceId,
                    StringComparison.Ordinal));
        }

        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < Capacity;
        }

        private static InventorySlot CreateEmptySlot()
        {
            return new InventorySlot();
        }

        private static InventorySlot CloneSlot(InventorySlot source)
        {
            return source == null
                ? CreateEmptySlot()
                : new InventorySlot
                {
                    ItemId = source.ItemId ?? string.Empty,
                    Count = source.Count,
                    Bound = source.Bound,
                    InstanceId = source.InstanceId ?? string.Empty,
                    ExtraJson = source.ExtraJson ?? string.Empty
                };
        }

        internal static EquipmentInstance CloneEquipmentInstance(
            EquipmentInstance source)
        {
            return source == null
                ? null
                : new EquipmentInstance
                {
                    InstanceId = source.InstanceId ?? string.Empty,
                    EquipmentDataId = source.EquipmentDataId ?? string.Empty,
                    RefineLevel = source.RefineLevel,
                    Durability = source.Durability,
                    GemIds = source.GemIds == null
                        ? Array.Empty<string>()
                        : (string[])source.GemIds.Clone()
                };
        }
    }
}
