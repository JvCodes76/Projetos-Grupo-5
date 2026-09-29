using System;
using System.Collections.Generic;
using Roguelike.Events;
using UnityEngine;

/// <summary>
/// Alternativa ao EventBus (SPEC §9.2): eventos C# no próprio prefab, assinados pelas views do mesmo objeto.
/// A troca para EventBusMovementEvents é uma linha no PlayerController (useLocalEvents). Lookup sem alocação.
/// </summary>
public sealed class LocalMovementEvents : MonoBehaviour, IMovementEvents
{
    private readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

    public void Subscribe<T>(Action<T> handler) where T : struct, IEvent
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        handlers.TryGetValue(typeof(T), out Delegate current);
        handlers[typeof(T)] = Delegate.Combine(current, handler);
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct, IEvent
    {
        if (handler == null) return;
        if (!handlers.TryGetValue(typeof(T), out Delegate current)) return;
        Delegate next = Delegate.Remove(current, handler);
        if (next == null) handlers.Remove(typeof(T));
        else handlers[typeof(T)] = next;
    }

    public void Raise<T>(in T evt) where T : struct, IEvent
    {
        if (handlers.TryGetValue(typeof(T), out Delegate d)) ((Action<T>)d).Invoke(evt);
    }
}
