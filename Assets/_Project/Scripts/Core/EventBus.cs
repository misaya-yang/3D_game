using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wendao.Core
{
    /// <summary>
    /// Synchronous, process-local event dispatch used for cross-system notifications.
    /// Subscribers are invoked in registration order from a stable snapshot.
    /// </summary>
    public static class EventBus
    {
        private readonly struct EventKey : IEquatable<EventKey>
        {
            public EventKey(string eventName, Type payloadType)
            {
                EventName = eventName;
                PayloadType = payloadType;
            }

            private string EventName { get; }
            private Type PayloadType { get; }

            public bool Equals(EventKey other)
            {
                return StringComparer.Ordinal.Equals(EventName, other.EventName)
                    && PayloadType == other.PayloadType;
            }

            public override bool Equals(object obj)
            {
                return obj is EventKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (StringComparer.Ordinal.GetHashCode(EventName) * 397)
                        ^ PayloadType.GetHashCode();
                }
            }
        }

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<EventKey, Delegate> TypedHandlers =
            new Dictionary<EventKey, Delegate>();
        private static readonly Dictionary<string, Action> ParameterlessHandlers =
            new Dictionary<string, Action>(StringComparer.Ordinal);

        public static void Subscribe<T>(string eventName, Action<T> handler)
        {
            ValidateEventName(eventName);
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var key = new EventKey(eventName, typeof(T));
            lock (SyncRoot)
            {
                TypedHandlers.TryGetValue(key, out var current);
                TypedHandlers[key] = Delegate.Combine(current, handler);
            }
        }

        public static void Unsubscribe<T>(string eventName, Action<T> handler)
        {
            ValidateEventName(eventName);
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var key = new EventKey(eventName, typeof(T));
            lock (SyncRoot)
            {
                if (!TypedHandlers.TryGetValue(key, out var current))
                {
                    return;
                }

                var remaining = Delegate.Remove(current, handler);
                if (remaining == null)
                {
                    TypedHandlers.Remove(key);
                }
                else
                {
                    TypedHandlers[key] = remaining;
                }
            }
        }

        public static void Publish<T>(string eventName, T args)
        {
            ValidateEventName(eventName);

            Delegate snapshot;
            lock (SyncRoot)
            {
                if (!TypedHandlers.TryGetValue(new EventKey(eventName, typeof(T)), out snapshot))
                {
                    return;
                }
            }

            foreach (var subscriber in snapshot.GetInvocationList())
            {
                try
                {
                    ((Action<T>)subscriber).Invoke(args);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public static void Subscribe(string eventName, Action handler)
        {
            ValidateEventName(eventName);
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (SyncRoot)
            {
                ParameterlessHandlers.TryGetValue(eventName, out var current);
                ParameterlessHandlers[eventName] = current + handler;
            }
        }

        public static void Unsubscribe(string eventName, Action handler)
        {
            ValidateEventName(eventName);
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (SyncRoot)
            {
                if (!ParameterlessHandlers.TryGetValue(eventName, out var current))
                {
                    return;
                }

                current -= handler;
                if (current == null)
                {
                    ParameterlessHandlers.Remove(eventName);
                }
                else
                {
                    ParameterlessHandlers[eventName] = current;
                }
            }
        }

        public static void Publish(string eventName)
        {
            ValidateEventName(eventName);

            Action snapshot;
            lock (SyncRoot)
            {
                if (!ParameterlessHandlers.TryGetValue(eventName, out snapshot))
                {
                    return;
                }
            }

            foreach (var subscriber in snapshot.GetInvocationList())
            {
                try
                {
                    ((Action)subscriber).Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                TypedHandlers.Clear();
                ParameterlessHandlers.Clear();
            }
        }

        private static void ValidateEventName(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Event name cannot be null, empty, or whitespace.", nameof(eventName));
            }
        }
    }
}

