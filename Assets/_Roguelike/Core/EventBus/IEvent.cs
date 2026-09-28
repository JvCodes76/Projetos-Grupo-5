namespace Roguelike.Events
{
    /// <summary>
    /// Marcador de todo evento que trafega pelo <see cref="EventBus{T}"/>.
    /// Invariantes: implementado só por <c>readonly struct</c>; nome no passado (um fato que já aconteceu,
    /// ex.: <c>PlayerDied</c>, nunca <c>KillPlayer</c>); payload imutável depois de construído.
    /// Catálogo completo em <c>Events/GameEvents.cs</c> e no ARQUITETURA.md (§4).
    /// </summary>
    public interface IEvent { }
}
