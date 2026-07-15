using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems;

namespace Wendao.Systems.World
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CangwuPathGate : MonoBehaviour
    {
        public const string LockedLocalizationKey = "ui_cangwu_path_locked";
        public const string LockedDefaultValue = "秘径尚未开启，先完成当前主线";

        private bool _travelRequested;

        public bool IsOpen
        {
            get
            {
                SaveWorldData world = SaveManager.Instance?.World;
                return world?.QuestFlags != null
                    && world.QuestFlags.TryGetValue(
                        MapContentIds.CangwuPathOpenFlag,
                        out bool isOpen)
                    && isOpen;
            }
        }

        private void Awake()
        {
            BoxCollider trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3f, 3f, 1.5f);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryEnter(other != null ? other.gameObject : null);
        }

        public bool TryEnter(GameObject traveler)
        {
            if (_travelRequested
                || traveler == null
                || !WorldActorUtility.IsPlayer(traveler))
            {
                return false;
            }

            if (!IsOpen)
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

            travel.UnlockMap(MapContentIds.CangwuMap);
            _travelRequested = SceneLoader.Instance.LoadMap(
                MapContentIds.CangwuMap,
                MapContentIds.CangwuGateTeleport);
            if (_travelRequested)
            {
                SaveManager.Instance?.AutoSave();
            }

            return _travelRequested;
        }

    }
}
