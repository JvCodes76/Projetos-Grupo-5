using System;
using System.Collections.Generic;
using Roguelike.Upgrades;

namespace Roguelike.Run
{
    /// <summary>
    /// Resumo imutável de uma run encerrada. Criado pelo RunState.CreateSummary (via RunFlow) ao entrar em
    /// Victory/Defeat; levado pelo evento RunEnded; lido pelo RunEndView e pela telemetria (4.3).
    /// É classe (e não struct) para não copiar as listas a cada passagem e para nunca existir "vazio por default".
    /// Invariantes: listas nunca nulas e copiadas na construção; Victory ⇒ LevelsCompleted == LevelCount;
    /// DeathCause ≠ Unknown só quando EndReason == PlayerDied.
    /// </summary>
    public sealed class RunSummary
    {
        private readonly LevelResult[] levelResults;
        private readonly UpgradeDefinition[] acquiredUpgrades;

        public RunSummary(
            int seed,
            RunEndReason endReason,
            DeathCause deathCause,
            int levelCount,
            IEnumerable<LevelResult> levelResults,
            IEnumerable<UpgradeDefinition> acquiredUpgrades)
        {
            if (levelResults == null) throw new ArgumentNullException(nameof(levelResults));
            if (acquiredUpgrades == null) throw new ArgumentNullException(nameof(acquiredUpgrades));

            Seed = seed;
            EndReason = endReason;
            DeathCause = deathCause;
            LevelCount = levelCount;
            this.levelResults = new List<LevelResult>(levelResults).ToArray();
            this.acquiredUpgrades = new List<UpgradeDefinition>(acquiredUpgrades).ToArray();

            float total = 0f;
            foreach (LevelResult result in this.levelResults)
            {
                total += result.ElapsedSeconds;
            }
            TotalTimeSeconds = total;
        }

        public int Seed { get; }
        public RunEndReason EndReason { get; }
        public DeathCause DeathCause { get; }

        /// <summary>Quantidade de fases da run (RunConfig.Levels.Count).</summary>
        public int LevelCount { get; }

        public bool IsVictory => EndReason == RunEndReason.Victory;

        /// <summary>Resultados das fases concluídas, em ordem.</summary>
        public IReadOnlyList<LevelResult> LevelResults => levelResults;

        public int LevelsCompleted => levelResults.Length;

        /// <summary>Índice da fase em que a run foi perdida; −1 na vitória.</summary>
        public int FailedLevelIndex => IsVictory ? -1 : levelResults.Length;

        /// <summary>Soma do tempo das fases concluídas (s). Não inclui a fase em que houve derrota.</summary>
        public float TotalTimeSeconds { get; }

        /// <summary>Upgrades adquiridos em ordem de aquisição; um item por stack (repetições = stacks).</summary>
        public IReadOnlyList<UpgradeDefinition> AcquiredUpgrades => acquiredUpgrades;
    }
}
