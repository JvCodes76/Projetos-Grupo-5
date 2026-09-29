using System;

namespace Roguelike.Simulation
{
    /// <summary>
    /// Acumulador de passo fixo a 60 Hz (SPEC §3.2, Q1/DS-02). O frame só decide QUANTOS ticks rodam; a lógica nunca
    /// lê o tempo do frame. Lógica pura: o adaptador (SimulationRunner) chama <see cref="BeginFrame"/> com o tempo
    /// escalado do frame e depois <see cref="NextStep"/> até receber <see cref="StepKind.None"/>.
    /// Invariantes: <see cref="Tick"/> só avança em passo real; freeze consome passos sem tick (fora do timer, RNF-04);
    /// com o freeze desligado a sequência de ticks é idêntica (RF-65); o excesso acima de MaxStepsPerFrame é descartado.
    /// </summary>
    public sealed class SimulationLoop : ISimulationClock
    {
        public const int TickRate = TickMath.DefaultTickRate;
        public const float FixedDt = 1f / TickRate;

        /// <summary>Máximo de passos por frame (evita a "espiral da morte"); o tempo acima disso é descartado.</summary>
        public int MaxStepsPerFrame = 8;

        /// <summary>Menus e pausa (além do timeScale = 0): não acumula nem entrega passos.</summary>
        public bool Paused;

        /// <summary>Opção de acessibilidade: com false, <see cref="RequestFreeze"/> é ignorado.</summary>
        public bool FreezeEnabled = true;

        private float accumulator;
        private int freezeSteps;

        public long Tick { get; private set; }

        public float Dt => FixedDt;

        /// <summary>Passos de freeze ainda pendentes.</summary>
        public int PendingFreezeSteps => freezeSteps;

        /// <summary>Fração do próximo tick já acumulada (interpolação de render). Em freeze, 1 (pose do último tick).</summary>
        public float Alpha => freezeSteps > 0 ? 1f : MathUtil.Clamp(accumulator / FixedDt, 0f, 1f);

        /// <summary>Acumula o tempo escalado do frame (timeScale 0 = nada acumula).</summary>
        public void BeginFrame(float scaledDeltaTime)
        {
            if (Paused || !(scaledDeltaTime > 0f)) return;
            accumulator = Math.Min(accumulator + scaledDeltaTime, FixedDt * MaxStepsPerFrame);
        }

        /// <summary>Entrega o próximo passo do frame: Tick (avança o relógio), Frozen (consumido pelo freeze) ou None.</summary>
        public StepKind NextStep()
        {
            if (Paused || accumulator < FixedDt) return StepKind.None;

            accumulator -= FixedDt;
            if (freezeSteps > 0)
            {
                freezeSteps--;
                return StepKind.Frozen;
            }

            Tick++;
            return StepKind.Tick;
        }

        /// <summary>Pede <paramref name="ticks"/> passos de freeze (hitstop local). Não soma: vale o maior pedido.</summary>
        public void RequestFreeze(int ticks)
        {
            if (!FreezeEnabled || ticks <= 0) return;
            freezeSteps = Math.Max(freezeSteps, ticks);
        }

        /// <summary>Descarta o tempo acumulado e o freeze pendente (troca de cena, testes). Não mexe no relógio.</summary>
        public void ResetAccumulator()
        {
            accumulator = 0f;
            freezeSteps = 0;
        }
    }
}
