using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems;
using Wendao.Systems.Cultivation;
using Wendao.Systems.Quest;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BlackwindDungeonGate : MonoBehaviour
    {
        public const string LockedLocalizationKey = "ui_blackwind_gate";
        public const string LockedDefaultValue = "金丹方可踏入黑风秘境。";

        private bool _travelRequested;

        public bool MeetsRealmRequirement
        {
            get
            {
                if (ServiceLocator.TryGet<ICultivationService>(
                    out ICultivationService cultivation))
                {
                    return cultivation.Realm >= RealmType.GoldenCore;
                }

                return (SaveManager.Instance?.Profile?.Realm ?? 0)
                    >= (int)RealmType.GoldenCore;
            }
        }

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = Vector3.one;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryEnter(other != null ? other.gameObject : null);
        }

        public bool TryEnter(GameObject traveler)
        {
            if (_travelRequested
                || !WorldActorUtility.IsPlayer(traveler))
            {
                return false;
            }

            if (!MeetsRealmRequirement)
            {
                EventBus.Publish(
                    UiEvents.ToastRequested,
                    new ToastInfo
                    {
                        LocalizationKey = LockedLocalizationKey,
                        DefaultValue = LockedDefaultValue,
                        Duration = 2.5f
                    });
                return false;
            }

            if (!ServiceLocator.TryGet<IMapTravelService>(
                    out IMapTravelService travel)
                || SceneLoader.Instance == null)
            {
                return false;
            }

            travel.UnlockMap(MapContentIds.BlackwindMap);
            int checkpoint = 0;
            SaveWorldData world = SaveManager.Instance?.World;
            if (world?.DungeonCheckpoint != null
                && world.DungeonCheckpoint.TryGetValue(
                    MapContentIds.BlackwindMap,
                    out int savedCheckpoint))
            {
                checkpoint = Mathf.Clamp(savedCheckpoint, 0, 4);
            }

            _travelRequested = SceneLoader.Instance.LoadMap(
                MapContentIds.BlackwindMap,
                MapContentIds.GetBlackwindSpawnId(checkpoint + 1));
            if (_travelRequested)
            {
                if (ServiceLocator.TryGet<IQuestService>(
                        out IQuestService quests))
                {
                    quests.NotifyReach(QuestContentIds.BlackwindEntrance);
                }

                SaveManager save = SaveManager.Instance;
                if (save != null && save.ActiveSlot >= 0)
                {
                    save.SaveGame(save.ActiveSlot);
                }
            }

            return _travelRequested;
        }
    }
}
