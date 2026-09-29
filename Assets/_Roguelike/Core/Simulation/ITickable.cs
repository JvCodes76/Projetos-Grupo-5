namespace Roguelike.Simulation
{
    /// <summary>Contexto de um tick da simulação (SPEC §2.3).</summary>
    public readonly struct TickContext
    {
        public TickContext(long tick, float dt)
        {
            Tick = tick;
            Dt = dt;
        }

        /// <summary>Índice do tick (monotônico; freeze e pausa não avançam).</summary>
        public readonly long Tick;

        /// <summary>Duração do tick (s): 1/60.</summary>
        public readonly float Dt;
    }

    /// <summary>
    /// Participante do relógio de simulação (SPEC §3.2). O SimulationRunner chama, em ordem de <see cref="TickOrder"/>:
    /// jogador 0 · câmera 100 · LevelTimer 200. Registra-se em OnEnable e sai em OnDisable.
    /// </summary>
    public interface ITickable
    {
        /// <summary>Ordem dentro do tick (menor roda antes).</summary>
        int TickOrder { get; }

        /// <summary>Uma vez por frame, antes dos ticks do frame (leitura de input).</summary>
        void OnFrameStart();

        /// <summary>Um passo de simulação.</summary>
        void Tick(in TickContext ctx);

        /// <summary>Saída de pausa: re-arma o input (RF-63).</summary>
        void OnResume();
    }

    /// <summary>Resultado de <see cref="SimulationLoop.NextStep"/>.</summary>
    public enum StepKind
    {
        /// <summary>Não há mais passos neste frame.</summary>
        None = 0,
        /// <summary>Um tick de simulação: rode os ITickables.</summary>
        Tick = 1,
        /// <summary>Passo consumido pelo freeze: nenhum ITickable roda e o relógio não avança.</summary>
        Frozen = 2,
    }
}
