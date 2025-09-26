using System;
using System.Collections.Generic;
using App.Ports;
using UnityEngine;

namespace Infra.EventBus
{
    public class MemoryEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
        private readonly object _lock = new();

        public void Publish<T>(T @event)
        {
            List<Delegate> handlersCopy;
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
                // hacemos copia para iterar sin problemas si alguien subscribe/unsubscribe en caliente
                handlersCopy = new List<Delegate>(list);
            }

            foreach (var d in handlersCopy)
            {
                if (d is Action<T> handler)
                {
                    try
                    {
                        handler(@event);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex); // evita que un handler rompa el resto
                    }
                }
            }
        }

        public void Subscribe<T>(Action<T> handler)
        {
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(typeof(T), out var list))
                {
                    list = new List<Delegate>();
                    _subscribers[typeof(T)] = list;
                }

                list.Add(handler);
            }
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(typeof(T), out var list)) return;
                list.Remove(handler);
                if (list.Count == 0) _subscribers.Remove(typeof(T));
            }
        }
    }
}