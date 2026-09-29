using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Trigger detectado pela varredura do tick (DS-12), e não por OnTriggerEnter2D: o tempo da fase fica
    /// determinístico (o fato acontece no tick exato em que o corpo entra). Implementado pelo EndGoal.
    /// Chamado uma vez por entrada (o TickTriggerScanner guarda quem já está sobreposto).
    /// </summary>
    public interface IPlayerTickTrigger
    {
        void OnPlayerTickEnter(Component player, long tick);
    }
}
