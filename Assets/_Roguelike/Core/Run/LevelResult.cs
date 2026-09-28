using Roguelike.Levels;

namespace Roguelike.Run
{
    /// <summary>
    /// Resultado de uma fase concluída. Criado pelo RunFlow.CompleteLevel, guardado no RunState.LevelResults,
    /// levado pelo evento LevelCompleted e resumido no RunSummary. Imutável.
    /// Invariantes: Performance ∈ [0, 1]; Grade coerente com Performance e os GradeThresholds do RunConfig;
    /// ElapsedSeconds ≥ 0. default(LevelResult) tem Level = null (sem resultado).
    /// </summary>
    public readonly struct LevelResult
    {
        public LevelResult(
            int levelIndex,
            LevelDefinition level,
            float elapsedSeconds,
            float effectiveTimeLimit,
            float targetTime,
            float performance,
            PerformanceGrade grade)
        {
            LevelIndex = levelIndex;
            Level = level;
            ElapsedSeconds = elapsedSeconds;
            EffectiveTimeLimit = effectiveTimeLimit;
            TargetTime = targetTime;
            Performance = performance;
            Grade = grade;
        }

        /// <summary>Índice da fase em RunConfig.Levels (base 0).</summary>
        public int LevelIndex { get; }

        public LevelDefinition Level { get; }

        /// <summary>Tempo oficial da fase (s): o último LevelTimeChanged, com granularidade de 0,1 s (ADR-13).</summary>
        public float ElapsedSeconds { get; }

        /// <summary>Limite que valeu nesta fase (base + TimeLimitBonus).</summary>
        public float EffectiveTimeLimit { get; }

        /// <summary>Tempo-alvo da fase (LevelDefinition.TargetTime).</summary>
        public float TargetTime { get; }

        /// <summary>Desempenho p ∈ [0, 1] (PerformanceEvaluator.Evaluate com o limite base).</summary>
        public float Performance { get; }

        public PerformanceGrade Grade { get; }
    }
}
