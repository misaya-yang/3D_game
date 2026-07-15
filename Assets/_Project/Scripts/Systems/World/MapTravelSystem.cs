using System;
using System.Collections.Generic;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems;

namespace Wendao.Systems.World
{
    public sealed class MapTravelSystem : SafeBehaviour, IMapTravelService
    {
        public const string TeleportUnlockedLocalizationKey =
            "ui_teleport_unlocked";
        public const string TeleportUnlockedDefaultValue = "传送阵已解锁";
        public const string TeleportLockedLocalizationKey =
            "ui_teleport_locked";
        public const string TeleportLockedDefaultValue = "此传送阵尚未解锁";

        private static readonly IReadOnlyList<string> EmptyIds =
            Array.Empty<string>();

        private static readonly IReadOnlyDictionary<string, TravelDestination>
            Destinations = new Dictionary<string, TravelDestination>(
                StringComparer.Ordinal)
            {
                [MapContentIds.QingshiTownTeleport] = new TravelDestination(
                    MapContentIds.QingshiMap,
                    MapContentIds.QingshiTownTeleport),
                [MapContentIds.CangwuGateTeleport] = new TravelDestination(
                    MapContentIds.CangwuMap,
                    MapContentIds.CangwuGateTeleport)
            };

        private bool _registeredService;

        public IReadOnlyList<string> UnlockedMaps
        {
            get
            {
                EnsureDefaultUnlocks();
                return SaveManager.Instance?.World?.UnlockedMaps ?? EmptyIds;
            }
        }

        public IReadOnlyList<string> UnlockedTeleports
        {
            get
            {
                EnsureDefaultUnlocks();
                return SaveManager.Instance?.World?.UnlockedTeleports ?? EmptyIds;
            }
        }

        private void Awake()
        {
            if (ServiceLocator.TryGet<IMapTravelService>(
                    out IMapTravelService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IMapTravelService>(this);
            _registeredService = true;
            DontDestroyOnLoad(gameObject);
            EnsureDefaultUnlocks();
        }

        private void OnDestroy()
        {
            if (_registeredService
                && ServiceLocator.TryGet<IMapTravelService>(
                    out IMapTravelService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IMapTravelService>();
            }

            _registeredService = false;
        }

        public bool IsMapUnlocked(string mapId)
        {
            EnsureDefaultUnlocks();
            return Contains(SaveManager.Instance?.World?.UnlockedMaps, mapId);
        }

        public bool IsTeleportUnlocked(string teleportId)
        {
            EnsureDefaultUnlocks();
            return Contains(
                SaveManager.Instance?.World?.UnlockedTeleports,
                teleportId);
        }

        public void UnlockMap(string mapId)
        {
            SaveWorldData world = SaveManager.Instance?.World;
            if (world == null || string.IsNullOrWhiteSpace(mapId))
            {
                return;
            }

            AddUnique(world.UnlockedMaps, mapId.Trim());
        }

        public void UnlockTeleport(string teleportId)
        {
            SaveWorldData world = SaveManager.Instance?.World;
            if (world == null || string.IsNullOrWhiteSpace(teleportId))
            {
                return;
            }

            string normalized = teleportId.Trim();
            if (!AddUnique(world.UnlockedTeleports, normalized))
            {
                return;
            }

            if (Destinations.TryGetValue(normalized, out TravelDestination destination))
            {
                AddUnique(world.UnlockedMaps, destination.MapId);
            }

            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = TeleportUnlockedLocalizationKey,
                    DefaultValue = TeleportUnlockedDefaultValue,
                    Duration = 2.5f
                });
            SaveManager.Instance.AutoSave();
        }

        public bool Travel(string teleportId)
        {
            string normalized = teleportId?.Trim() ?? string.Empty;
            if (!IsTeleportUnlocked(normalized)
                || !Destinations.TryGetValue(
                    normalized,
                    out TravelDestination destination)
                || !IsMapUnlocked(destination.MapId))
            {
                PublishLockedToast();
                return false;
            }

            SceneLoader loader = SceneLoader.Instance;
            if (loader == null
                || !loader.LoadMap(destination.MapId, destination.SpawnId))
            {
                return false;
            }

            SaveManager.Instance?.AutoSave();
            return true;
        }

        private static void EnsureDefaultUnlocks()
        {
            SaveWorldData world = SaveManager.Instance?.World;
            if (world == null)
            {
                return;
            }

            AddUnique(world.UnlockedMaps, MapContentIds.QingshiMap);
            AddUnique(
                world.UnlockedTeleports,
                MapContentIds.QingshiTownTeleport);
        }

        private static bool AddUnique(List<string> ids, string id)
        {
            if (ids == null || string.IsNullOrWhiteSpace(id) || ids.Contains(id))
            {
                return false;
            }

            ids.Add(id);
            return true;
        }

        private static bool Contains(List<string> ids, string id)
        {
            return ids != null
                && !string.IsNullOrWhiteSpace(id)
                && ids.Contains(id.Trim());
        }

        private static void PublishLockedToast()
        {
            EventBus.Publish(
                UiEvents.ToastRequested,
                new ToastInfo
                {
                    LocalizationKey = TeleportLockedLocalizationKey,
                    DefaultValue = TeleportLockedDefaultValue,
                    Duration = 2.5f
                });
        }

        private readonly struct TravelDestination
        {
            public TravelDestination(string mapId, string spawnId)
            {
                MapId = mapId;
                SpawnId = spawnId;
            }

            public string MapId { get; }
            public string SpawnId { get; }
        }
    }
}
