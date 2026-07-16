using System;
using UnityEngine;
using Wendao.Core;
using Wendao.Systems.Combat;
using Wendao.Systems.Input;

namespace Wendao.Entities.Player
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerActionBuffer : MonoBehaviour
    {
        private readonly float[] _remainingByAction =
            new float[(int)BufferedActionType.Skill4 + 1];

        private IPlayerInputSource _inputSource;

        public bool IsConsumptionEnabled =>
            _inputSource == null || _inputSource.IsEnabled;

        public int BufferedCount
        {
            get
            {
                int count = 0;
                for (int index = 1; index < _remainingByAction.Length; index++)
                {
                    if (_remainingByAction[index] > 0f)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                HandleGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateInfo>(
                GameManager.GameStateChangedEvent,
                HandleGameStateChanged);
            Clear();
        }

        private void Update()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.State != GameState.Playing)
            {
                if (gameManager.State != GameState.Paused)
                {
                    Clear();
                }

                return;
            }

            if (_inputSource == null)
            {
                ServiceLocator.TryGet(out _inputSource);
            }

            if (_inputSource != null && !_inputSource.IsEnabled)
            {
                Clear();
                return;
            }

            TickBuffer(Time.unscaledDeltaTime);
            CaptureInputSnapshot();
        }

        public void SetInputSource(IPlayerInputSource inputSource)
        {
            _inputSource = inputSource;
        }

        public void EnqueueBufferedAction(BufferedActionType type)
        {
            EnqueueBufferedAction(type, CombatFeelSettings.InputBufferSeconds);
        }

        public void EnqueueBufferedAction(
            BufferedActionType type,
            float durationSeconds)
        {
            if (!IsValid(type)
                || durationSeconds <= 0f
                || float.IsNaN(durationSeconds)
                || float.IsInfinity(durationSeconds))
            {
                return;
            }

            int index = (int)type;
            _remainingByAction[index] = Mathf.Max(
                _remainingByAction[index],
                durationSeconds);
        }

        public bool HasBufferedAction(BufferedActionType type)
        {
            return IsValid(type) && _remainingByAction[(int)type] > 0f;
        }

        public float GetRemaining(BufferedActionType type)
        {
            return IsValid(type)
                ? _remainingByAction[(int)type]
                : 0f;
        }

        public bool TryConsume(BufferedActionType type)
        {
            if (!HasBufferedAction(type))
            {
                return false;
            }

            _remainingByAction[(int)type] = 0f;
            return true;
        }

        public void TickBuffer(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f
                || float.IsNaN(unscaledDeltaTime)
                || float.IsInfinity(unscaledDeltaTime))
            {
                return;
            }

            for (int index = 1; index < _remainingByAction.Length; index++)
            {
                _remainingByAction[index] = Mathf.Max(
                    0f,
                    _remainingByAction[index] - unscaledDeltaTime);
            }
        }

        public void CaptureInputSnapshot()
        {
            if (_inputSource == null || !_inputSource.IsEnabled)
            {
                return;
            }

            if (_inputSource.LightAttackPressedThisFrame)
            {
                EnqueueBufferedAction(BufferedActionType.LightAttack);
            }

            if (_inputSource.HeavyAttackPressedThisFrame)
            {
                EnqueueBufferedAction(BufferedActionType.HeavyAttack);
            }

            if (_inputSource.DodgePressedThisFrame)
            {
                EnqueueBufferedAction(BufferedActionType.Dodge);
            }

            if (_inputSource.Skill1PressedThisFrame)
            {
                EnqueueBufferedAction(BufferedActionType.Skill1);
            }

            if (_inputSource.Skill2PressedThisFrame)
            {
                EnqueueBufferedAction(BufferedActionType.Skill2);
            }

            if (_inputSource.Skill3PressedThisFrame)
            {
                EnqueueBufferedAction(BufferedActionType.Skill3);
            }

            if (_inputSource.Skill4PressedThisFrame)
            {
                EnqueueBufferedAction(BufferedActionType.Skill4);
            }
        }

        public void Clear()
        {
            Array.Clear(_remainingByAction, 0, _remainingByAction.Length);
        }

        private void HandleGameStateChanged(GameStateInfo info)
        {
            if (info.Next == GameState.Dead
                || info.Next == GameState.Dialogue
                || info.Next == GameState.Cutscene
                || info.Next == GameState.Loading
                || info.Next == GameState.MainMenu)
            {
                Clear();
            }
        }

        private static bool IsValid(BufferedActionType type)
        {
            return type > BufferedActionType.None
                && type <= BufferedActionType.Skill4;
        }
    }
}
