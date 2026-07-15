using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Skill;

namespace Wendao.Systems.Inventory
{
    public sealed class ItemUseSystem : SafeBehaviour, IItemUseService
    {
        public const string FullHpToastKey = "ui_item_use_full_hp";
        public const string FullHpToastDefault = "气血已满，无需服丹。";
        public const string FullManaToastKey = "ui_item_use_full_mana";
        public const string FullManaToastDefault = "灵力充盈，无需服丹。";
        public const string CannotUseToastKey = "ui_item_use_unavailable";
        public const string CannotUseToastDefault = "此物品当前无法使用。";

        private bool _registeredService;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IItemUseService>(out IItemUseService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IItemUseService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<IItemUseService>(out IItemUseService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IItemUseService>();
            }

            _registeredService = false;
        }

        public bool CanUse(int slotIndex)
        {
            return TryValidate(
                slotIndex,
                out _,
                out _,
                out _,
                out _);
        }

        public bool Use(int slotIndex)
        {
            if (!TryValidate(
                    slotIndex,
                    out IInventoryService inventory,
                    out InventorySlot slot,
                    out ItemData item,
                    out UseFailure failure))
            {
                PublishFailure(failure);
                return false;
            }

            if (!inventory.RemoveAt(slotIndex, 1))
            {
                PublishFailure(UseFailure.Unavailable);
                return false;
            }

            ApplyEffects(item);

            EventBus.Publish(
                InventoryEvents.ItemUsed,
                new ItemUseInfo
                {
                    ItemId = slot.ItemId,
                    SlotIndex = slotIndex
                });
            return true;
        }

        private static bool TryValidate(
            int slotIndex,
            out IInventoryService inventory,
            out InventorySlot slot,
            out ItemData item,
            out UseFailure failure)
        {
            inventory = null;
            slot = null;
            item = null;
            failure = UseFailure.Unavailable;

            if (!ServiceLocator.TryGet(out inventory)
                || slotIndex < 0
                || slotIndex >= inventory.Slots.Count)
            {
                return false;
            }

            slot = inventory.Slots[slotIndex];
            item = ConfigDatabase.Instance?.GetItem(slot?.ItemId);
            if (slot == null
                || slot.IsEmpty
                || item == null
                || item.Type != ItemType.Consumable
                || item.UseEffects == null
                || item.UseEffects.Length == 0
                || !MeetsRealmRequirement(item.RequiredRealm))
            {
                return false;
            }

            bool hasSupportedEffect = false;
            foreach (UseEffect effect in item.UseEffects)
            {
                if (effect == null || effect.Value <= 0f)
                {
                    return false;
                }

                if (!CanApplyEffect(effect, out failure))
                {
                    return false;
                }

                hasSupportedEffect = true;
            }

            failure = UseFailure.None;
            return hasSupportedEffect;
        }

        private static bool CanApplyEffect(
            UseEffect effect,
            out UseFailure failure)
        {
            failure = UseFailure.Unavailable;
            switch (effect.EffectType)
            {
                case UseEffectType.Heal:
                    if (!ServiceLocator.TryGet<IPlayerHealthService>(
                            out IPlayerHealthService playerHealth)
                        || playerHealth.IsDead)
                    {
                        return false;
                    }

                    if (playerHealth.CurrentHp >= playerHealth.MaxHp - 0.001f)
                    {
                        failure = UseFailure.FullHp;
                        return false;
                    }

                    failure = UseFailure.None;
                    return true;
                case UseEffectType.AddBodyXp:
                    if (!ServiceLocator.TryGet<IBodyRefinementService>(
                            out IBodyRefinementService body)
                        || !body.CanGainXp)
                    {
                        return false;
                    }

                    failure = UseFailure.None;
                    return true;
                case UseEffectType.RestoreMana:
                    if (!ServiceLocator.TryGet<IPlayerResourceService>(
                            out IPlayerResourceService resources))
                    {
                        return false;
                    }

                    if (resources.CurrentMana >= resources.MaxMana - 0.001f)
                    {
                        failure = UseFailure.FullMana;
                        return false;
                    }

                    failure = UseFailure.None;
                    return true;
                case UseEffectType.AddCultivationXp:
                    if (!ServiceLocator.TryGet<ICultivationService>(out _))
                    {
                        return false;
                    }

                    failure = UseFailure.None;
                    return true;
                default:
                    return false;
            }
        }

        private static void ApplyEffects(ItemData item)
        {
            foreach (UseEffect effect in item.UseEffects)
            {
                switch (effect.EffectType)
                {
                    case UseEffectType.Heal:
                        if (ServiceLocator.TryGet<IPlayerHealthService>(
                                out IPlayerHealthService playerHealth))
                        {
                            playerHealth.ApplyHeal(effect.Value, item.Id);
                        }

                        break;
                    case UseEffectType.AddBodyXp:
                        if (ServiceLocator.TryGet<IBodyRefinementService>(
                                out IBodyRefinementService body))
                        {
                            body.AddBodyXpFromPotion(effect.Value);
                        }

                        break;
                    case UseEffectType.RestoreMana:
                        if (ServiceLocator.TryGet<IPlayerResourceService>(
                                out IPlayerResourceService resources))
                        {
                            resources.ApplyManaDelta(effect.Value);
                        }

                        break;
                    case UseEffectType.AddCultivationXp:
                        if (ServiceLocator.TryGet<ICultivationService>(
                                out ICultivationService cultivation))
                        {
                            cultivation.AddXp(effect.Value, XpSourceType.Consume);
                        }

                        break;
                }
            }
        }

        private static bool MeetsRealmRequirement(int requiredRealm)
        {
            if (requiredRealm <= 0)
            {
                return true;
            }

            SaveManager saveManager = SaveManager.Instance;
            int realm = saveManager?.Profile?.Realm
                ?? (int)RealmType.QiCondensation;
            return realm >= requiredRealm;
        }

        private static void PublishFailure(UseFailure failure)
        {
            string key = failure == UseFailure.FullHp
                ? FullHpToastKey
                : failure == UseFailure.FullMana
                    ? FullManaToastKey
                    : CannotUseToastKey;
            string defaultValue = failure == UseFailure.FullHp
                ? FullHpToastDefault
                : failure == UseFailure.FullMana
                    ? FullManaToastDefault
                    : CannotUseToastDefault;
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = key,
                    DefaultValue = defaultValue,
                    Duration = 2.5f
                });
        }

        private enum UseFailure
        {
            None,
            Unavailable,
            FullHp,
            FullMana
        }
    }
}
