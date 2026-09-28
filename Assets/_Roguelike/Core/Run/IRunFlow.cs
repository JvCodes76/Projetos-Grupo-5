using System.Collections.Generic;
using Roguelike.Levels;
using Roguelike.Upgrades;

namespace Roguelike.Run
{
    /// <summary>
    /// Máquina de estados pura da run (diagrama da §1). Quem chama: o RunManager (adaptador DDOL, tarefa 3.1), que
    /// traduz eventos do bus em comandos e, depois de cada comando aceito, lê <see cref="Phase"/> para carregar cenas
    /// e emitir RunStarted/LevelStarted/LevelCompleted/UpgradeOffersGenerated/RunEnded. O RunFlow NÃO usa o EventBus.
    ///
    /// Regras de todos os comandos:
    /// - Retornam true se a transição aconteceu. Comando fora da fase válida retorna false e não muda NADA
    ///   (sem exceção): assim eventos duplicados ou concorrentes (ex.: LevelGoalReached e LevelTimeExpired no
    ///   mesmo frame, PlayerDied depois de LevelTimeExpired) são ignorados com segurança.
    /// - Argumento inválido (nulo, negativo, config sem fases) lança exceção: é erro de programação, não de fluxo.
    ///
    /// Transições (fase atual → comando → nova fase):
    ///   Menu             → StartRun           → LoadingLevel (fase 0)
    ///   LoadingLevel     → StartLevel         → Playing
    ///   Playing          → CompleteLevel      → LevelResult
    ///   Playing          → FailByTimeout      → Defeat
    ///   Playing          → FailByDeath        → Defeat
    ///   LevelResult      → ContinueFromResult → Victory (era a última fase)
    ///                                         | UpgradeSelection (≥ 1 oferta não vazia)
    ///                                         | LoadingLevel (próxima fase; todas as ofertas vazias, sem escolha)
    ///   UpgradeSelection → SelectUpgrade      → LoadingLevel (próxima fase)
    ///   Victory | Defeat → ReturnToMenu       → Menu
    /// </summary>
    public interface IRunFlow
    {
        RunPhase Phase { get; }

        /// <summary>Estado da run ativa; null em Menu.</summary>
        RunState State { get; }

        /// <summary>Ofertas da rodada atual (tamanho = RunConfig.OfferCount); lista vazia fora de UpgradeSelection.</summary>
        IReadOnlyList<UpgradeOffer> CurrentOffers { get; }

        /// <summary>Último resultado registrado (payload de LevelCompleted); default(LevelResult) se nenhum.</summary>
        LevelResult LastLevelResult { get; }

        /// <summary>Resumo da run (payload de RunEnded); não nulo só em Victory e Defeat.</summary>
        RunSummary Summary { get; }

        /// <summary>Menu → LoadingLevel. Cria um RunState novo na fase 0 (stats do kit base).</summary>
        bool StartRun(RunConfig config, int seed);

        /// <summary>LoadingLevel → Playing. Chamado quando a cena carregou e o jogador foi posicionado.</summary>
        bool StartLevel();

        /// <summary>
        /// Playing → LevelResult. Calcula desempenho (limite base) e nota (RunConfig.GradeThresholds), registra o
        /// LevelResult. <paramref name="elapsedSeconds"/> &lt; 0 ⇒ ArgumentOutOfRangeException.
        /// </summary>
        bool CompleteLevel(float elapsedSeconds);

        /// <summary>Playing → Defeat com RunEndReason.TimeExpired.</summary>
        bool FailByTimeout();

        /// <summary>Playing → Defeat com RunEndReason.PlayerDied e a causa informada.</summary>
        bool FailByDeath(DeathCause cause);

        /// <summary>
        /// LevelResult → Victory | UpgradeSelection | LoadingLevel. Gera as ofertas com o desempenho da fase
        /// recém-concluída (seed da run + índice dessa fase).
        /// </summary>
        bool ContinueFromResult();

        /// <summary>
        /// UpgradeSelection → LoadingLevel. <paramref name="upgrade"/> precisa ser uma das ofertas não vazias,
        /// senão retorna false. Adquire um stack (RunState.AcquireUpgrade) e avança para a próxima fase.
        /// </summary>
        bool SelectUpgrade(UpgradeDefinition upgrade);

        /// <summary>Victory | Defeat → Menu. Descarta o RunState e o Summary.</summary>
        bool ReturnToMenu();
    }
}
