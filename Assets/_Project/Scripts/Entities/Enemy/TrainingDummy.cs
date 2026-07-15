using UnityEngine;
using Wendao.Core;
using Wendao.Data;
using Wendao.Systems.Combat;

namespace Wendao.Entities.Enemy
{
    public sealed class TrainingDummy : SafeBehaviour,
        IDamageable,
        ICombatStatsProvider,
        ICombatTeamProvider,
        ILockOnTarget,
        IHitstunReceiver,
        ICombatDeathHandler
    {
        public const float DefaultMaxHp = 30f;
        public const float DefaultLockOnDisengageRange = 14f;

        [SerializeField, Min(1f)] private float _maxHp = DefaultMaxHp;
        [SerializeField, Min(0f)] private float _defense;

        private ICombatService _combatService;
        private bool _registeredActor;
        private bool _deathHandled;
        private float _hitstunRemaining;

        public string EnemyId => CombatContentIds.TrainingDummyEnemyId;
        public float CurrentHp { get; private set; }
        public float MaxHp => _maxHp;
        public bool IsDead => CurrentHp <= 0f;
        public float Attack => 0f;
        public float Defense => _defense;
        public float CritRate => 0f;
        public float CritDamage => 1.5f;
        public float HitstunMultiplier => 1f;
        public float HitstunRemaining => _hitstunRemaining;
        public CombatTeam Team => CombatTeam.Enemy;
        public bool CanLockOn => isActiveAndEnabled && !IsDead;
        public Transform LockOnTransform => transform;
        public float LockOnDisengageRange => DefaultLockOnDisengageRange;

        private void Awake()
        {
            CurrentHp = _maxHp;
        }

        private void OnEnable()
        {
            TryRegisterActor();
        }

        private void Update()
        {
            if (!_registeredActor)
            {
                TryRegisterActor();
            }

            TickHitstun(Time.deltaTime);
        }

        private void OnDisable()
        {
            UnregisterActor();
        }

        protected override void SafeStart()
        {
            TryRegisterActor();
        }

        public void ConfigureStats(float maxHp, float defense, bool refillHp = true)
        {
            _maxHp = Mathf.Max(1f, maxHp);
            _defense = Mathf.Max(0f, defense);
            CurrentHp = refillHp
                ? _maxHp
                : Mathf.Clamp(CurrentHp, 0f, _maxHp);
            _deathHandled = CurrentHp <= 0f;
            _hitstunRemaining = 0f;
            SetColliderEnabled(!IsDead);
        }

        public void ResetDummy()
        {
            CurrentHp = _maxHp;
            _deathHandled = false;
            _hitstunRemaining = 0f;
            SetColliderEnabled(true);
            SetVisualColor(new Color(0.53f, 0.36f, 0.2f, 1f));
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f)
            {
                return;
            }

            CurrentHp = Mathf.Max(0f, CurrentHp - info.Amount);
            if (!IsDead)
            {
                return;
            }

            SetColliderEnabled(false);
            SetVisualColor(new Color(0.24f, 0.24f, 0.22f, 1f));
        }

        public void ApplyHitstun(float seconds)
        {
            if (IsDead
                || seconds <= 0f
                || float.IsNaN(seconds)
                || float.IsInfinity(seconds))
            {
                return;
            }

            _hitstunRemaining = Mathf.Max(_hitstunRemaining, seconds);
        }

        public void TickHitstun(float deltaTime)
        {
            if (deltaTime <= 0f
                || float.IsNaN(deltaTime)
                || float.IsInfinity(deltaTime))
            {
                return;
            }

            _hitstunRemaining = Mathf.Max(
                0f,
                _hitstunRemaining - deltaTime);
        }

        public void HandleDeath(DamageInfo killingBlow)
        {
            if (!IsDead || _deathHandled)
            {
                return;
            }

            _deathHandled = true;
            EventBus.Publish(
                CombatEvents.EnemyKilled,
                new EnemyDeathInfo
                {
                    EnemyId = EnemyId,
                    Rank = EnemyRank.Normal,
                    Victim = gameObject,
                    Killer = killingBlow.Source,
                    Position = transform.position
                });
        }

        public void ApplyHeal(float amount, string sourceId)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            CurrentHp = Mathf.Min(_maxHp, CurrentHp + amount);
        }

        private bool TryRegisterActor()
        {
            if (_registeredActor)
            {
                return true;
            }

            if (!ServiceLocator.TryGet(out _combatService))
            {
                return false;
            }

            _combatService.RegisterActor(this);
            _registeredActor = true;
            return true;
        }

        private void UnregisterActor()
        {
            if (!_registeredActor)
            {
                return;
            }

            _combatService?.UnregisterActor(this);
            _combatService = null;
            _registeredActor = false;
        }

        private void SetColliderEnabled(bool enabled)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = enabled;
            }
        }

        private void SetVisualColor(Color color)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].material != null)
                {
                    renderers[i].material.color = color;
                }
            }
        }
    }
}
