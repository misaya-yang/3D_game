using UnityEngine;
using Wendao.Entities.Player;
using Wendao.Core;
using Wendao.Systems.Feedback;

namespace Wendao.Entities.Enemy
{
    public sealed class BossArenaController : MonoBehaviour
    {
        public const float DefaultArenaRadius = 6.5f;

        private EnemyBrain _boss;
        private Vector3 _arenaCenter;
        private float _arenaRadius = DefaultArenaRadius;
        private bool _bossAudioActive;

        public EnemyBrain Boss => _boss;
        public Vector3 ArenaCenter => _arenaCenter;
        public float ArenaRadius => _arenaRadius;
        public bool IsPlayerInside { get; private set; }

        private void Update()
        {
            TickArena();
        }

        public void Configure(
            EnemyBrain boss,
            Vector3 arenaCenter,
            float arenaRadius)
        {
            SetBossAudio(false);
            _boss = boss;
            _arenaCenter = arenaCenter;
            _arenaRadius = Mathf.Max(1f, arenaRadius);
            IsPlayerInside = false;
        }

        public void TickArena()
        {
            if (_boss == null || _boss.IsDead)
            {
                SetBossAudio(false);
                IsPlayerInside = false;
                return;
            }

            PlayerStats player = FindAnyObjectByType<PlayerStats>();
            bool inside = player != null
                && !player.IsDead
                && HorizontalDistance(player.transform.position, _arenaCenter)
                    <= _arenaRadius;

            if (inside)
            {
                SetBossAudio(true);
                if (!IsPlayerInside && _boss.Target == null)
                {
                    _boss.OnAggro(player.gameObject);
                }
            }
            else if (IsPlayerInside
                || _boss.Target != null
                || _boss.CurrentBossPhase > 0)
            {
                _boss.ResetBossEncounter();
            }

            if (!inside)
            {
                SetBossAudio(false);
            }

            IsPlayerInside = inside;
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private void OnDisable()
        {
            SetBossAudio(false);
        }

        private void SetBossAudio(bool active)
        {
            if (_bossAudioActive == active
                && (!active
                    || (ServiceLocator.TryGet<IAudioStateService>(
                            out IAudioStateService existing)
                        && existing.IsBossEncounter)))
            {
                return;
            }

            if (ServiceLocator.TryGet<IAudioStateService>(
                    out IAudioStateService audioState))
            {
                _bossAudioActive = active;
                audioState.SetBossEncounter(
                    active,
                    AudioContentIds.BossStoneGeneral);
            }
            else if (!active)
            {
                _bossAudioActive = false;
            }
        }
    }
}
