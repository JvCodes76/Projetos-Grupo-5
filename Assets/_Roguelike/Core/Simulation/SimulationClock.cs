namespace Roguelike.Simulation
{
    /// <summary>
    /// Relógio de simulação (SPEC §3.4): o tick atual e a duração do tick. Freeze e pausa não o avançam.
    /// Implementado pelo <see cref="SimulationLoop"/>; lido pelo LevelTimer e pela telemetria.
    /// </summary>
    public interface ISimulationClock
    {
        long Tick { get; }
        float Dt { get; }
    }
}
