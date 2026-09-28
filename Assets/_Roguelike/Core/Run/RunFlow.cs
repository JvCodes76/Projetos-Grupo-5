using System;
using System.Collections.Generic;
using Roguelike.Levels;
using Roguelike.Upgrades;

namespace Roguelike.Run
{
    /// <summary>
    /// Implementação padrão do <see cref="IRunFlow"/> (tarefa 2.3). Contrato completo na interface.
    /// Dependências: IUpgradeOfferGenerator (injetado; nos testes, um gerador falso), PerformanceEvaluator (estático)
    /// e RunState. Começa em RunPhase.Menu.
    /// </summary>
    public sealed class RunFlow : IRunFlow
    {
        /// <param name="offerGenerator">Gerador de ofertas; nulo ⇒ ArgumentNullException.</param>
        public RunFlow(IUpgradeOfferGenerator offerGenerator)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public RunPhase Phase => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public RunState State => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public IReadOnlyList<UpgradeOffer> CurrentOffers => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public LevelResult LastLevelResult => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public RunSummary Summary => throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");

        public bool StartRun(RunConfig config, int seed)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public bool StartLevel()
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public bool CompleteLevel(float elapsedSeconds)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public bool FailByTimeout()
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public bool FailByDeath(DeathCause cause)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public bool ContinueFromResult()
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public bool SelectUpgrade(UpgradeDefinition upgrade)
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }

        public bool ReturnToMenu()
        {
            throw new NotImplementedException("Tarefa 2.3 do PLANO_REFACTOR_ROGUELIKE.md");
        }
    }
}
