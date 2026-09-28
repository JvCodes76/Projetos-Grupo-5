using System;
using System.Collections.Generic;
using Roguelike.Levels;
using Roguelike.Stats;
using Roguelike.Upgrades;

namespace Roguelike.Run
{
    /// <summary>
    /// Todo o estado de UMA run em memória (§1: tudo zera ao fim da run): config, seed, fase atual, stats do jogador,
    /// upgrades adquiridos e resultados. Criado pelo RunFlow.StartRun e descartado no ReturnToMenu. Tarefa 2.3.
    /// Leitura pública (RunManager, UpgradeEffect, testes); escrita só pelo RunFlow (métodos internal), para que
    /// nenhum adaptador mude a run sem passar pela máquina de estados.
    /// Implementa IUpgradeInventory para o gerador de ofertas consultar stacks.
    /// Invariantes: 0 ≤ CurrentLevelIndex &lt; LevelCount; GetStacks(u) ≤ u.MaxStacks;
    /// AcquiredUpgrades e Stats sempre coerentes (todo stack registrado foi aplicado ao Stats).
    /// </summary>
    public sealed class RunState : IUpgradeInventory
    {
        // Cópia da ordem das fases no início da run: editar o asset no meio da run (Inspector) não muda a run.
        private readonly LevelDefinition[] levels;

        private readonly Dictionary<UpgradeDefinition, int> stacks = new Dictionary<UpgradeDefinition, int>();
        private readonly List<UpgradeDefinition> acquiredUpgrades = new List<UpgradeDefinition>();
        private readonly List<LevelResult> levelResults = new List<LevelResult>();

        private int currentLevelIndex;

        /// <summary>
        /// Nova run na fase 0, com PlayerStats novo a partir de config.BaseStats.
        /// config nulo ou sem fases ⇒ ArgumentException.
        /// </summary>
        public RunState(RunConfig config, int seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            IReadOnlyList<LevelDefinition> configLevels = config.Levels;
            if (configLevels == null || configLevels.Count == 0)
            {
                throw new ArgumentException("RunConfig sem fases.", nameof(config));
            }

            levels = new LevelDefinition[configLevels.Count];
            for (int i = 0; i < levels.Length; i++)
            {
                if (configLevels[i] == null)
                {
                    throw new ArgumentException($"RunConfig com fase nula no índice {i}.", nameof(config));
                }
                levels[i] = configLevels[i];
            }

            if (config.BaseStats == null)
            {
                throw new ArgumentException("RunConfig sem BaseStats.", nameof(config));
            }

            Config = config;
            Seed = seed;
            Stats = new PlayerStats(config.BaseStats);
            currentLevelIndex = 0;
        }

        public RunConfig Config { get; }

        public int Seed { get; }

        /// <summary>Índice (base 0) da fase atual em Config.Levels: a que está carregando, em jogo ou recém-concluída.</summary>
        public int CurrentLevelIndex => currentLevelIndex;

        public int LevelCount => levels.Length;

        public LevelDefinition CurrentLevel => levels[currentLevelIndex];

        public bool IsLastLevel => currentLevelIndex == levels.Length - 1;

        /// <summary>Limite da fase atual com bônus: CurrentLevel.TimeLimit + Stats.Get(TimeLimitBonus).</summary>
        public float EffectiveTimeLimit => CurrentLevel.TimeLimit + Stats.Get(StatType.TimeLimitBonus);

        public PlayerStats Stats { get; }

        /// <summary>Resultados das fases concluídas, em ordem.</summary>
        public IReadOnlyList<LevelResult> LevelResults => levelResults;

        /// <summary>Upgrades em ordem de aquisição; um item por stack.</summary>
        public IReadOnlyList<UpgradeDefinition> AcquiredUpgrades => acquiredUpgrades;

        public int GetStacks(UpgradeDefinition upgrade)
        {
            if (upgrade == null) throw new ArgumentNullException(nameof(upgrade));

            return stacks.TryGetValue(upgrade, out int count) ? count : 0;
        }

        /// <summary>
        /// Adquire UM stack: incrementa a contagem, chama Stats.ApplyUpgrade e depois cada UpgradeEffect.OnAcquired(this).
        /// Stack acima de MaxStacks ⇒ InvalidOperationException (o gerador nunca deveria oferecê-lo).
        /// </summary>
        internal void AcquireUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null) throw new ArgumentNullException(nameof(upgrade));

            int newCount = GetStacks(upgrade) + 1;
            if (newCount > upgrade.MaxStacks)
            {
                // Validado antes de qualquer escrita: a run fica intacta.
                throw new InvalidOperationException(
                    $"Upgrade '{upgrade.Id}' já está no máximo de stacks ({upgrade.MaxStacks}).");
            }

            stacks[upgrade] = newCount;
            acquiredUpgrades.Add(upgrade);
            Stats.ApplyUpgrade(upgrade);

            // Efeitos depois de modificadores e flags, uma vez por stack (ADR-22).
            IReadOnlyList<UpgradeEffect> effects = upgrade.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                UpgradeEffect effect = effects[i];
                if (effect != null)
                {
                    effect.OnAcquired(this);
                }
            }
        }

        /// <summary>Registra o resultado da fase atual.</summary>
        internal void RecordLevelResult(LevelResult result)
        {
            // Um resultado por fase, na ordem: RunSummary.FailedLevelIndex depende de LevelResults.Count == fases concluídas.
            if (result.LevelIndex != currentLevelIndex)
            {
                throw new InvalidOperationException(
                    $"Resultado da fase {result.LevelIndex} registrado com a fase atual em {currentLevelIndex}.");
            }
            if (levelResults.Count != currentLevelIndex)
            {
                throw new InvalidOperationException($"A fase {currentLevelIndex} já tem resultado registrado.");
            }

            levelResults.Add(result);
        }

        /// <summary>Passa para a próxima fase. Chamado na última fase ⇒ InvalidOperationException.</summary>
        internal void AdvanceToNextLevel()
        {
            if (IsLastLevel)
            {
                throw new InvalidOperationException("Não há próxima fase: a run já está na última.");
            }

            currentLevelIndex++;
        }

        /// <summary>Resumo imutável da run para o evento RunEnded.</summary>
        internal RunSummary CreateSummary(RunEndReason endReason, DeathCause deathCause)
        {
            // Invariantes do RunSummary: Victory ⇒ todas as fases concluídas; causa de morte só com PlayerDied.
            if (endReason == RunEndReason.Victory && levelResults.Count != levels.Length)
            {
                throw new InvalidOperationException(
                    $"Vitória com {levelResults.Count} de {levels.Length} fases concluídas.");
            }
            if (endReason != RunEndReason.PlayerDied && deathCause != DeathCause.Unknown)
            {
                throw new ArgumentException($"DeathCause {deathCause} só vale com RunEndReason.PlayerDied.", nameof(deathCause));
            }

            // O RunSummary copia as listas (ADR-20): mudanças posteriores no RunState não o afetam.
            return new RunSummary(Seed, endReason, deathCause, levels.Length, levelResults, acquiredUpgrades);
        }
    }
}
