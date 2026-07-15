using System;
using UnityEngine;

namespace Wendao.Systems.World
{
    /// <summary>
    /// Progress accumulator for one scene-load sequence. Values can only move forward.
    /// </summary>
    public sealed class SceneLoadProgress
    {
        public event Action<float> Changed;

        public float Value { get; private set; }
        public int Sequence { get; private set; }

        public void Begin()
        {
            Sequence++;
            Value = 0f;
            Notify(Value);
        }

        public float Report(float candidate)
        {
            if (float.IsNaN(candidate))
            {
                return Value;
            }

            float next = Mathf.Clamp01(candidate);
            if (next <= Value)
            {
                return Value;
            }

            Value = next;
            Notify(Value);
            return Value;
        }

        private void Notify(float value)
        {
            Action<float> handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<float>)subscriber).Invoke(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
