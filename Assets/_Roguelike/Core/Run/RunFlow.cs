using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private static readonly IReadOnlyList<UpgradeOffer> NoOffers = Array.AsReadOnly(Array.Empty<UpgradeOffer>());

        private readonly IUpgradeOfferGenerator offerGenerator;

        private RunPhase phase = RunPhase.Menu;
        private RunState state;
        private IReadOnlyList<UpgradeOffer> currentOffers = NoOffers;
        private LevelResult lastLevelResult;
        private RunSummary summary;

        /// <param name="offerGenerator">Gerador de ofertas; nulo ⇒ ArgumentNullException.</param>
        public RunFlow(IUpgradeOfferGenerator offerGenerator)
        {
            this.offerGenerator = offerGenerator ?? throw new ArgumentNullException(nameof(offerGenerator));
        }

        public RunPhase Phase => phase;

        public RunState State => state;

        public IReadOnlyList<UpgradeOffer> CurrentOffers => currentOffers;

        public LevelResult LastLevelResult => lastLevelResult;

        public RunSummary Summary => summary;

        // Ordem em todos os comandos: argumento inválido lança (bug, em qualquer fase); depois, fora de fase ⇒ false
        // sem tocar em nada (ADR-15); só então há escrita.

        public bool StartRun(RunConfig config, int seed)
        {
            // O construtor do RunState valida a config (nula, sem fases, fase nula, sem BaseStats) e lança antes de
            // qualquer mudança no RunFlow; criado antes do teste de fase para config inválida lançar em qualquer fase.
            var newState = new RunState(config, seed);

            if (phase != RunPhase.Menu) return false;

            state = newState;
            currentOffers = NoOffers;
            lastLevelResult = default;
            summary = null;
            phase = RunPhase.LoadingLevel;
            return true;
        }

        public bool StartLevel()
        {
            if (phase != RunPhase.LoadingLevel) return false;

            phase = RunPhase.Playing;
            return true;
        }

        public bool CompleteLevel(float elapsedSeconds)
        {
            if (elapsedSeconds < 0f || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds,
                    "Tempo da fase deve ser finito e ≥ 0.");
            }

            if (phase != RunPhase.Playing) return false;

            LevelDefinition level = state.CurrentLevel;

            // Desempenho com o limite BASE (ADR-12); o limite efetivo só vai para o resultado, como informação.
            float performance = PerformanceEvaluator.Evaluate(elapsedSeconds, level.TimeLimit, level.TargetTime);
            PerformanceGrade grade = PerformanceEvaluator.GetGrade(performance, state.Config.GradeThresholds);

            var result = new LevelResult(
                state.CurrentLevelIndex,
                level,
                elapsedSeconds,
                state.EffectiveTimeLimit,
                level.TargetTime,
                performance,
                grade);

            state.RecordLevelResult(result);
            lastLevelResult = result;
            phase = RunPhase.LevelResult;
            return true;
        }

        public bool FailByTimeout()
        {
            if (phase != RunPhase.Playing) return false;

            summary = state.CreateSummary(RunEndReason.TimeExpired, DeathCause.Unknown);
            phase = RunPhase.Defeat;
            return true;
        }

        public bool FailByDeath(DeathCause cause)
        {
            if (!Enum.IsDefined(typeof(DeathCause), cause))
            {
                throw new ArgumentOutOfRangeException(nameof(cause), cause, "DeathCause desconhecida.");
            }

            if (phase != RunPhase.Playing) return false;

            summary = state.CreateSummary(RunEndReason.PlayerDied, cause);
            phase = RunPhase.Defeat;
            return true;
        }

        public bool ContinueFromResult()
        {
            if (phase != RunPhase.LevelResult) return false;

            if (state.IsLastLevel)
            {
                summary = state.CreateSummary(RunEndReason.Victory, DeathCause.Unknown);
                phase = RunPhase.Victory;
                return true;
            }

            RunConfig config = state.Config;
            var request = new UpgradeOfferRequest(
                state.Seed,
                state.CurrentLevelIndex,
                lastLevelResult.Performance,
                config.OfferCount,
                config.UpgradePool,
                config.RarityTable,
                state);

            IReadOnlyList<UpgradeOffer> generated = offerGenerator.Generate(request);
            if (generated == null)
            {
                throw new InvalidOperationException("O gerador de ofertas devolveu null.");
            }
            if (generated.Count != request.OfferCount)
            {
                throw new InvalidOperationException(
                    $"O gerador devolveu {generated.Count} ofertas; o esperado é {request.OfferCount}.");
            }

            if (!HasAnyNonEmpty(generated))
            {
                // Pool esgotado: pula a escolha para a run não travar em UpgradeSelection (ADR-06).
                state.AdvanceToNextLevel();
                phase = RunPhase.LoadingLevel;
                return true;
            }

            // Cópia própria: o gerador (ou quem guardou a lista dele) não consegue mudar as ofertas depois.
            var copy = new UpgradeOffer[generated.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = generated[i];
            }

            currentOffers = new ReadOnlyCollection<UpgradeOffer>(copy);
            phase = RunPhase.UpgradeSelection;
            return true;
        }

        public bool SelectUpgrade(UpgradeDefinition upgrade)
        {
            if (upgrade == null) throw new ArgumentNullException(nameof(upgrade));

            if (phase != RunPhase.UpgradeSelection) return false;

            // Upgrade que não está entre as ofertas não vazias: ignorado (ADR-15), como um evento fora de fase.
            if (!IsOffered(upgrade)) return false;

            state.AcquireUpgrade(upgrade);
            state.AdvanceToNextLevel();
            currentOffers = NoOffers;
            phase = RunPhase.LoadingLevel;
            return true;
        }

        public bool ReturnToMenu()
        {
            if (phase != RunPhase.Victory && phase != RunPhase.Defeat) return false;

            state = null;
            summary = null;
            currentOffers = NoOffers;
            lastLevelResult = default;
            phase = RunPhase.Menu;
            return true;
        }

        private static bool HasAnyNonEmpty(IReadOnlyList<UpgradeOffer> offers)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (!offers[i].IsEmpty) return true;
            }

            return false;
        }

        private bool IsOffered(UpgradeDefinition upgrade)
        {
            for (int i = 0; i < currentOffers.Count; i++)
            {
                UpgradeOffer offer = currentOffers[i];
                if (!offer.IsEmpty && offer.Upgrade == upgrade) return true;
            }

            return false;
        }
    }
}
