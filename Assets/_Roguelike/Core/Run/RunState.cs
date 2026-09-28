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
        /// <summary>
        /// Nova run na fase 0, com PlayerStats novo a partir de config.BaseStats.
        /// config nulo ou sem fases ⇒ ArgumentException.
        /// </summary>
        public RunState(RunConfig config, int seed)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public RunConfig Config => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public int Seed => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        /// <summary>Índice (base 0) da fase atual em Config.Levels: a que está carregando, em jogo ou recém-concluída.</summary>
        public int CurrentLevelIndex => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public int LevelCount => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public LevelDefinition CurrentLevel => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public bool IsLastLevel => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        /// <summary>Limite da fase atual com bônus: CurrentLevel.TimeLimit + Stats.Get(TimeLimitBonus).</summary>
        public float EffectiveTimeLimit => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public PlayerStats Stats => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        /// <summary>Resultados das fases concluídas, em ordem.</summary>
        public IReadOnlyList<LevelResult> LevelResults => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        /// <summary>Upgrades em ordem de aquisição; um item por stack.</summary>
        public IReadOnlyList<UpgradeDefinition> AcquiredUpgrades => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public int GetStacks(UpgradeDefinition upgrade)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>
        /// Adquire UM stack: incrementa a contagem, chama Stats.ApplyUpgrade e depois cada UpgradeEffect.OnAcquired(this).
        /// Stack acima de MaxStacks ⇒ InvalidOperationException (o gerador nunca deveria oferecê-lo).
        /// </summary>
        internal void AcquireUpgrade(UpgradeDefinition upgrade)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Registra o resultado da fase atual.</summary>
        internal void RecordLevelResult(LevelResult result)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Passa para a próxima fase. Chamado na última fase ⇒ InvalidOperationException.</summary>
        internal void AdvanceToNextLevel()
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        /// <summary>Resumo imutável da run para o evento RunEnded.</summary>
        internal RunSummary CreateSummary(RunEndReason endReason, DeathCause deathCause)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }
    }
}
