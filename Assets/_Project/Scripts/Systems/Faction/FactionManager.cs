using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Data;

namespace Wendao.Systems.Faction
{
    public sealed class FactionManager : SafeBehaviour, IFactionService
    {
        private bool _registeredService;

        private void Awake()
        {
            if (ServiceLocator.TryGet<IFactionService>(out IFactionService existing)
                && !ReferenceEquals(existing, this))
            {
                Destroy(gameObject);
                return;
            }

            ServiceLocator.Register<IFactionService>(this);
            _registeredService = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Update()
        {
            if (!ServiceLocator.TryGet<IFactionService>(out IFactionService current))
            {
                ServiceLocator.Register<IFactionService>(this);
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
                && ServiceLocator.TryGet<IFactionService>(out IFactionService current)
                && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<IFactionService>();
            }

            _registeredService = false;
        }

        public int GetRep(string factionId)
        {
            SaveProfileData profile = SaveManager.Instance?.Profile;
            return IsSupportedFaction(factionId)
                && profile?.FactionReputation != null
                && profile.FactionReputation.TryGetValue(factionId, out int value)
                ? Mathf.Max(0, value)
                : 0;
        }

        public void AddRep(string factionId, int delta)
        {
            if (!IsSupportedFaction(factionId) || delta == 0)
            {
                return;
            }

            SaveProfileData profile = SaveManager.Instance?.Profile;
            if (profile?.FactionReputation == null)
            {
                return;
            }

            bool joined = profile.FactionReputation.ContainsKey(factionId);
            int previous = GetRep(factionId);
            long nextLong = (long)previous + delta;
            int current = (int)Math.Max(0L, Math.Min(int.MaxValue, nextLong));
            if (current == previous && joined)
            {
                return;
            }

            int oldRank = GetRankForRep(previous);
            profile.FactionReputation[factionId] = current;
            int newRank = GetRankForRep(current);
            PersistProfile();
            EventBus.Publish(
                FactionEvents.ReputationChanged,
                new FactionReputationInfo
                {
                    FactionId = factionId,
                    Previous = previous,
                    Current = current,
                    OldRank = oldRank,
                    NewRank = newRank
                });
            if (!joined)
            {
                PublishToast(
                    FactionContentIds.JoinedToastKey,
                    FactionContentIds.JoinedToastDefault);
            }

            if (newRank > oldRank)
            {
                PublishToast(
                    FactionContentIds.RankUpToastKey,
                    string.Format(FactionContentIds.RankUpToastDefault, newRank));
            }
        }

        public int GetRank(string factionId)
        {
            return IsSupportedFaction(factionId)
                ? GetRankForRep(GetRep(factionId))
                : 0;
        }

        public float GetShopDiscount(string factionId)
        {
            return GetRank(factionId) * 0.05f;
        }

        public bool HasJoined(string factionId)
        {
            SaveProfileData profile = SaveManager.Instance?.Profile;
            return IsSupportedFaction(factionId)
                && profile?.FactionReputation != null
                && profile.FactionReputation.ContainsKey(factionId);
        }

        public bool Join(string factionId)
        {
            if (!IsSupportedFaction(factionId))
            {
                return false;
            }

            if (HasJoined(factionId))
            {
                return true;
            }

            SaveProfileData profile = SaveManager.Instance?.Profile;
            if (profile?.FactionReputation == null)
            {
                return false;
            }

            profile.FactionReputation[factionId] = 0;
            PersistProfile();
            PublishToast(
                FactionContentIds.JoinedToastKey,
                FactionContentIds.JoinedToastDefault);
            return true;
        }

        private static int GetRankForRep(int reputation)
        {
            int rank = 0;
            for (int index = 1;
                 index < FactionContentIds.RankThresholds.Length;
                 index++)
            {
                if (reputation < FactionContentIds.RankThresholds[index])
                {
                    break;
                }

                rank = index;
            }

            return rank;
        }

        private static bool IsSupportedFaction(string factionId)
        {
            return string.Equals(
                factionId,
                FactionContentIds.Danding,
                StringComparison.Ordinal);
        }

        private static void PersistProfile()
        {
            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null && saveManager.ActiveSlot >= 0)
            {
                saveManager.TrySaveModule("profile");
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
                    Duration = 2.5f
                });
        }
    }
}
