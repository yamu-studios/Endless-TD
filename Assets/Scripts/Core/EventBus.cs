// ============================================================================
// ETD.Core - EventBus.cs
// Lightweight, type-safe event system with zero-allocation for frequent events
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ETD.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                return;

            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
            {
                // Avoid duplicate subscriptions from UI panels/managers that are opened,
                // closed, and reopened during the same run.
                existing = Delegate.Remove(existing, handler);
                _handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[type] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    _handlers.Remove(type);
                else
                    _handlers[type] = result;
            }
        }

        public static void Publish<T>(T evt) where T : struct
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var handler) || handler == null)
                return;

            // Invoke each subscriber separately. A stale/destroyed run-scene listener
            // from Retry must never prevent later listeners such as GameOverUI from
            // receiving the event.
            var calls = handler.GetInvocationList();
            for (int i = 0; i < calls.Length; i++)
            {
                var del = calls[i];
                if (del is not Action<T> action)
                    continue;

                if (action.Target is UnityEngine.Object unityTarget && unityTarget == null)
                {
                    Unsubscribe(action);
                    continue;
                }

                try
                {
                    action(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static int DebugEventTypeCount => _handlers.Count;

        public static int DebugSubscriberCountTotal()
        {
            int total = 0;
            foreach (var kvp in _handlers)
            {
                if (kvp.Value == null)
                    continue;

                total += kvp.Value.GetInvocationList().Length;
            }
            return total;
        }

        public static string DebugBuildSubscriberSummary()
        {
            var sb = new System.Text.StringBuilder(512);
            foreach (var kvp in _handlers)
            {
                int count = kvp.Value != null ? kvp.Value.GetInvocationList().Length : 0;
                if (count <= 0)
                    continue;

                if (sb.Length > 0)
                    sb.Append(", ");

                sb.Append(kvp.Key.Name).Append('=').Append(count);
            }
            return sb.ToString();
        }
#endif

        public static void Clear()
        {
            _handlers.Clear();
        }
    }
}
