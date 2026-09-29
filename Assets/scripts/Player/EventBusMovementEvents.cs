using Roguelike.Events;

/// <summary>Publica os eventos do movimento no EventBus (SPEC §9.2). Síncrono e sem alocação.</summary>
public sealed class EventBusMovementEvents : IMovementEvents
{
    public void Raise<T>(in T evt) where T : struct, IEvent
    {
        EventBus<T>.Raise(evt);
    }
}
