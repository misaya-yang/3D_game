using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wendao.Core;
using Wendao.Data;
using Wendao.Entities.Enemy;
using Wendao.Entities.Player;
using Wendao.Systems.Combat;

namespace Wendao.Entities.Visuals
{
    public enum CharacterVisualRole
    {
        Player,
        Npc,
        HumanEnemy,
        Creature,
        Boss
    }

    /// <summary>
    /// Plays animation clips embedded in the imported CC0 FBX files without
    /// requiring hand-authored Animator Controllers. Gameplay roots, movement
    /// and hit timing remain authoritative; the graph only drives the model.
    /// </summary>
    public sealed class RuntimeCharacterAnimator : MonoBehaviour
    {
        private enum MotionState
        {
            Idle,
            Move,
            Attack,
            Dodge,
            Hit,
            Death,
            Skill
        }

        private readonly Dictionary<string, AnimationClip> _clips =
            new Dictionary<string, AnimationClip>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Playable[] _clipSlots = new Playable[2];

        private GameObject _actorRoot;
        private string _resourcePath = string.Empty;
        private CharacterVisualRole _role;
        private Animator _animator;
        private Animation _legacyAnimation;
        private PlayerController _player;
        private PlayerCombatController _playerCombat;
        private PlayerStats _playerStats;
        private EnemyBrain _enemy;
        private Transform _armatureRoot;
        private Vector3 _armatureLocalPosition;
        private Quaternion _armatureLocalRotation;
        private Vector3 _armatureLocalScale;
        private Transform _skeletonRoot;
        private Vector3 _skeletonLocalPosition;
        private Quaternion _skeletonLocalRotation;
        private Vector3 _skeletonLocalScale;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private MotionState _motionState = MotionState.Idle;
        private int _activeSlot = -1;
        private int _previousSlot = -1;
        private float _fadeElapsed;
        private float _fadeDuration;
        private bool _activeLoop;
        private float _hitReactRemaining;
        private bool _configured;

        public string CurrentClipName { get; private set; } = string.Empty;
        public bool IsGraphValid => _legacyAnimation != null
            || _graph.IsValid();
        public int ImportedClipCount => _clips.Count;
        public bool IsCombatAction => _motionState == MotionState.Attack
            || _motionState == MotionState.Skill;

        public void Configure(
            GameObject actorRoot,
            string resourcePath,
            CharacterVisualRole role)
        {
            if (_configured)
            {
                return;
            }

            _actorRoot = actorRoot;
            _resourcePath = resourcePath ?? string.Empty;
            _role = role;
            _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<Animator>();
            }

            _animator.applyRootMotion = false;
            _player = actorRoot != null
                ? actorRoot.GetComponent<PlayerController>()
                : null;
            _playerCombat = actorRoot != null
                ? actorRoot.GetComponent<PlayerCombatController>()
                : null;
            _playerStats = actorRoot != null
                ? actorRoot.GetComponent<PlayerStats>()
                : null;
            _enemy = actorRoot != null
                ? actorRoot.GetComponent<EnemyBrain>()
                : null;
            CaptureArmatureRoot();
            LoadClips();
            if (_clips.Count == 0)
            {
                Debug.LogWarning(
                    $"No embedded clips found for character art {_resourcePath}.");
                enabled = false;
                return;
            }

            if (UsesLegacyClips())
            {
                BuildLegacyAnimation();
            }
            else
            {
                BuildGraph();
            }
            _configured = true;
            SwitchMotion(MotionState.Idle, true);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
        }

        private void Update()
        {
            if (!_configured
                || (_legacyAnimation == null && !_graph.IsValid()))
            {
                return;
            }

            _hitReactRemaining = Mathf.Max(
                0f,
                _hitReactRemaining - Time.deltaTime);
            MotionState desired = ResolveMotionState();
            if (desired != _motionState
                || (desired == MotionState.Attack
                    && ShouldRefreshAttackClip()))
            {
                SwitchMotion(desired, false);
            }

            if (_legacyAnimation == null)
            {
                TickCrossFade(Time.deltaTime);
                TickLoopingClip();
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DamageInfo>(
                CombatEvents.DamageApplied,
                HandleDamageApplied);
        }

        private void LateUpdate()
        {
            if (!_configured || _armatureRoot == null)
            {
                return;
            }

            // Imported clips contain authoring-space root curves. Gameplay
            // movement stays on the actor root, so keep the visual armature
            // anchored while allowing all child bones to animate.
            _armatureRoot.localPosition = _armatureLocalPosition;
            _armatureRoot.localRotation = _armatureLocalRotation;
            _armatureRoot.localScale = _armatureLocalScale;
            if (_skeletonRoot != null)
            {
                _skeletonRoot.localPosition = _skeletonLocalPosition;
                _skeletonRoot.localRotation = _skeletonLocalRotation;
                _skeletonRoot.localScale = _skeletonLocalScale;
            }
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        private void LoadClips()
        {
            _clips.Clear();
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(
                _resourcePath);
            foreach (AnimationClip clip in clips)
            {
                if (clip == null || string.IsNullOrWhiteSpace(clip.name))
                {
                    continue;
                }

                _clips[clip.name] = clip;
                int separator = clip.name.LastIndexOf('|');
                if (separator >= 0 && separator + 1 < clip.name.Length)
                {
                    _clips[clip.name.Substring(separator + 1)] = clip;
                }
            }
        }

        private void CaptureArmatureRoot()
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate == null
                    || candidate == transform
                    || candidate.name.IndexOf(
                        "Armature",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                _armatureRoot = candidate;
                _armatureLocalPosition = candidate.localPosition;
                _armatureLocalRotation = candidate.localRotation;
                _armatureLocalScale = candidate.localScale;
                CaptureSkeletonRoot(candidate);
                return;
            }
        }

        private void CaptureSkeletonRoot(Transform armature)
        {
            foreach (Transform candidate in
                armature.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == null
                    || candidate == armature
                    || !string.Equals(
                        candidate.name,
                        "Root",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                _skeletonRoot = candidate;
                _skeletonLocalPosition = candidate.localPosition;
                _skeletonLocalRotation = candidate.localRotation;
                _skeletonLocalScale = candidate.localScale;
                return;
            }
        }

        private void BuildGraph()
        {
            _graph = PlayableGraph.Create(
                $"WendaoCharacter_{gameObject.name}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                _graph,
                "Character",
                _animator);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
        }

        private bool UsesLegacyClips()
        {
            foreach (AnimationClip clip in _clips.Values)
            {
                if (clip != null && clip.legacy)
                {
                    return true;
                }
            }
            return false;
        }

        private void BuildLegacyAnimation()
        {
            if (_animator != null)
            {
                _animator.enabled = false;
            }

            _legacyAnimation = GetComponent<Animation>();
            if (_legacyAnimation == null)
            {
                _legacyAnimation = gameObject.AddComponent<Animation>();
            }
            _legacyAnimation.playAutomatically = false;
            _legacyAnimation.animatePhysics = false;
            _legacyAnimation.cullingType =
                AnimationCullingType.AlwaysAnimate;

            var installed = new HashSet<AnimationClip>();
            foreach (AnimationClip clip in _clips.Values)
            {
                if (clip == null || !clip.legacy || !installed.Add(clip))
                {
                    continue;
                }
                _legacyAnimation.AddClip(clip, clip.name);
            }
        }

        private MotionState ResolveMotionState()
        {
            if (IsDead())
            {
                return MotionState.Death;
            }
            if (_hitReactRemaining > 0f)
            {
                return MotionState.Hit;
            }

            if (_role == CharacterVisualRole.Player && _player != null)
            {
                if (_player.State == PlayerState.Dodge)
                {
                    return MotionState.Dodge;
                }
                if (_player.State == PlayerState.SkillCast)
                {
                    return MotionState.Skill;
                }
                if ((_playerCombat != null && _playerCombat.IsAttacking)
                    || _player.State == PlayerState.LightAttack
                    || _player.State == PlayerState.HeavyAttack)
                {
                    return MotionState.Attack;
                }
                if (_player.HorizontalVelocity.sqrMagnitude > 0.05f)
                {
                    return MotionState.Move;
                }

                return MotionState.Idle;
            }

            if (_enemy != null)
            {
                switch (_enemy.State)
                {
                    case EnemyBrainState.Attack:
                        return MotionState.Attack;
                    case EnemyBrainState.Skill:
                        return MotionState.Skill;
                    case EnemyBrainState.Patrol:
                    case EnemyBrainState.Chase:
                    case EnemyBrainState.Return:
                        return MotionState.Move;
                    default:
                        return MotionState.Idle;
                }
            }

            return MotionState.Idle;
        }

        private bool IsDead()
        {
            if (_playerStats != null && _playerStats.IsDead)
            {
                return true;
            }
            if (_player != null && _player.State == PlayerState.Dead)
            {
                return true;
            }
            return _enemy != null && _enemy.IsDead;
        }

        private void SwitchMotion(MotionState motion, bool immediate)
        {
            AnimationClip clip = ResolveClip(motion);
            if (clip == null)
            {
                return;
            }

            bool loop = motion == MotionState.Idle
                || motion == MotionState.Move
                || (_enemy != null
                    && (motion == MotionState.Attack
                        || motion == MotionState.Skill));
            float speed = ResolvePlaybackSpeed(motion, clip);
            if (_legacyAnimation != null)
            {
                PlayLegacyClip(
                    clip,
                    motion,
                    loop,
                    speed,
                    immediate);
                return;
            }

            int nextSlot = _activeSlot < 0 ? 0 : 1 - _activeSlot;
            DestroySlot(nextSlot);

            AnimationClipPlayable clipPlayable =
                AnimationClipPlayable.Create(_graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetApplyPlayableIK(false);
            clipPlayable.SetTime(0d);
            clipPlayable.SetSpeed(speed);
            _graph.Connect(clipPlayable, 0, _mixer, nextSlot);
            _clipSlots[nextSlot] = clipPlayable;

            _previousSlot = _activeSlot;
            _activeSlot = nextSlot;
            _activeLoop = loop;
            _motionState = motion;
            CurrentClipName = clip.name;
            _fadeElapsed = 0f;
            _fadeDuration = immediate
                ? 0f
                : motion == MotionState.Attack
                    || motion == MotionState.Dodge
                    || motion == MotionState.Hit
                    ? 0.045f
                    : 0.12f;

            if (_previousSlot < 0 || _fadeDuration <= 0f)
            {
                _mixer.SetInputWeight(_activeSlot, 1f);
                if (_previousSlot >= 0)
                {
                    DestroySlot(_previousSlot);
                    _previousSlot = -1;
                }
            }
            else
            {
                _mixer.SetInputWeight(_activeSlot, 0f);
                _mixer.SetInputWeight(_previousSlot, 1f);
            }
        }

        private void PlayLegacyClip(
            AnimationClip clip,
            MotionState motion,
            bool loop,
            float speed,
            bool immediate)
        {
            AnimationState state = _legacyAnimation[clip.name];
            if (state == null)
            {
                Debug.LogWarning(
                    $"Legacy animation state missing: {clip.name}");
                return;
            }

            state.wrapMode = loop
                ? WrapMode.Loop
                : WrapMode.ClampForever;
            state.speed = speed;
            state.time = 0f;
            _motionState = motion;
            _activeLoop = loop;
            CurrentClipName = clip.name;
            if (immediate)
            {
                _legacyAnimation.Play(clip.name);
            }
            else
            {
                float fade = motion == MotionState.Attack
                    || motion == MotionState.Dodge
                    || motion == MotionState.Hit
                    ? 0.045f
                    : 0.12f;
                _legacyAnimation.CrossFade(
                    clip.name,
                    fade,
                    PlayMode.StopSameLayer);
            }
        }

        private AnimationClip ResolveClip(MotionState motion)
        {
            switch (motion)
            {
                case MotionState.Move:
                    return FindClip(
                        IsWolf() ? "Gallop" : null,
                        IsStoneGeneral() ? "Fast_Flying" : null,
                        "Run_Weapon",
                        "Run",
                        "Walk");
                case MotionState.Attack:
                    if (IsWolf())
                    {
                        return FindClip("Attack");
                    }
                    if (IsStoneGeneral())
                    {
                        return FindClip("Punch", "Headbutt");
                    }
                    if (_role == CharacterVisualRole.HumanEnemy)
                    {
                        return FindClip(
                            "Dagger_Attack",
                            "Dagger_Attack2",
                            "Punch");
                    }
                    return _playerCombat != null
                        && (_playerCombat.AttackType == PlayerAttackType.Heavy
                            || _playerCombat.CurrentComboStep >= 3)
                        ? FindClip(
                            "Sword_Attack",
                            "Attack2",
                            "Attack",
                            "Punch")
                        : FindClip(
                            "Sword_AttackFast",
                            "Attack",
                            "Punch");
                case MotionState.Dodge:
                    return FindClip("Roll");
                case MotionState.Hit:
                    return FindClip(
                        IsWolf() ? "Idle_HitReact_Left" : null,
                        IsStoneGeneral() ? "HitReact" : null,
                        "RecieveHit",
                        "RecieveHit_2",
                        "RecieveHit_Attacking",
                        "Idle");
                case MotionState.Death:
                    return FindClip("Death", "Idle");
                case MotionState.Skill:
                    return FindClip(
                        IsStoneGeneral() ? "Punch" : null,
                        IsWolf() ? "Attack" : null,
                        "Sword_Attack",
                        "Spell1",
                        "Attack2",
                        "Attack");
                default:
                    return FindClip(
                        IsStoneGeneral() ? "Flying_Idle" : null,
                        "Idle",
                        "Idle_Weapon",
                        "Idle_Attacking");
            }
        }

        private AnimationClip FindClip(params string[] names)
        {
            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                if (_clips.TryGetValue(name, out AnimationClip exact))
                {
                    return exact;
                }
            }

            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                foreach (KeyValuePair<string, AnimationClip> pair in _clips)
                {
                    if (pair.Key.EndsWith(
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }
            }

            foreach (AnimationClip fallback in _clips.Values)
            {
                return fallback;
            }
            return null;
        }

        private float ResolvePlaybackSpeed(
            MotionState motion,
            AnimationClip clip)
        {
            float desiredDuration = clip.length;
            if (_player != null)
            {
                if (motion == MotionState.Dodge)
                {
                    desiredDuration = PlayerController.DodgeDuration;
                }
                else if (motion == MotionState.Attack && _playerCombat != null)
                {
                    desiredDuration = _playerCombat.CurrentWindup
                        + _playerCombat.CurrentRecovery;
                }
            }
            else if (_enemy != null && motion == MotionState.Attack)
            {
                desiredDuration = _enemy.EffectiveAttackInterval;
            }

            return desiredDuration > 0.01f
                ? Mathf.Clamp(clip.length / desiredDuration, 0.45f, 3.5f)
                : 1f;
        }

        private void TickCrossFade(float deltaTime)
        {
            if (_previousSlot < 0 || _fadeDuration <= 0f)
            {
                return;
            }

            _fadeElapsed += Mathf.Max(0f, deltaTime);
            float progress = Mathf.Clamp01(_fadeElapsed / _fadeDuration);
            _mixer.SetInputWeight(_activeSlot, progress);
            _mixer.SetInputWeight(_previousSlot, 1f - progress);
            if (progress >= 1f)
            {
                DestroySlot(_previousSlot);
                _previousSlot = -1;
            }
        }

        private void TickLoopingClip()
        {
            if (!_activeLoop
                || _activeSlot < 0
                || !_clipSlots[_activeSlot].IsValid())
            {
                return;
            }

            Playable playable = _clipSlots[_activeSlot];
            double duration = playable.GetDuration();
            if (duration <= 0d || double.IsInfinity(duration))
            {
                AnimationClip clip = ResolveClip(_motionState);
                duration = clip != null ? clip.length : 0d;
            }
            if (duration > 0d && playable.GetTime() >= duration)
            {
                playable.SetTime(playable.GetTime() % duration);
            }
        }

        private bool ShouldRefreshAttackClip()
        {
            if (_playerCombat == null || !_playerCombat.IsAttacking)
            {
                return false;
            }

            AnimationClip expected = ResolveClip(MotionState.Attack);
            return expected != null
                && !string.Equals(
                    expected.name,
                    CurrentClipName,
                    StringComparison.Ordinal);
        }

        private void DestroySlot(int slot)
        {
            if (slot < 0 || slot >= _clipSlots.Length)
            {
                return;
            }

            Playable playable = _clipSlots[slot];
            if (playable.IsValid())
            {
                _mixer.DisconnectInput(slot);
                _graph.DestroyPlayable(playable);
            }
            _clipSlots[slot] = Playable.Null;
            _mixer.SetInputWeight(slot, 0f);
        }

        private void HandleDamageApplied(DamageInfo info)
        {
            if (_actorRoot == null
                || info.Target == null
                || info.Amount <= 0f)
            {
                return;
            }

            Transform target = info.Target.transform;
            Transform actor = _actorRoot.transform;
            if (target == actor
                || target.IsChildOf(actor)
                || actor.IsChildOf(target))
            {
                _hitReactRemaining = Mathf.Max(
                    _hitReactRemaining,
                    info.IsKillingBlow ? 0f : 0.16f);
            }
        }

        private bool IsWolf()
        {
            return _resourcePath.EndsWith(
                "/Wolf",
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsStoneGeneral()
        {
            return _resourcePath.EndsWith(
                "/StoneGeneral",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
