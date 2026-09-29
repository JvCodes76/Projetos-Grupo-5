using System;
using System.Collections.Generic;
using Roguelike.Levels;
using Roguelike.Run;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEngine;

// Catálogo de eventos do MVP (§3.4 do PLANO_REFACTOR_ROGUELIKE.md e ARQUITETURA.md §4).
// Regras: readonly struct, nome no passado, payload imutável. Todo evento novo entra aqui E no catálogo do
// ARQUITETURA.md no mesmo PR. Ordem típica numa fase: ARQUITETURA.md §6.
namespace Roguelike.Events
{
    // ───────────────────────────── Run ─────────────────────────────

    /// <summary>
    /// Uma run nova começou (Menu → CarregandoFase). Emissor: RunManager. Ouvintes: RunUI.
    /// Logo depois o RunManager emite PlayerStatsChanged com o kit base (o reset do PlayerStats é consequência do RunState novo).
    /// </summary>
    public readonly struct RunStarted : IEvent
    {
        public RunStarted(int seed, RunConfig config)
        {
            Seed = seed;
            Config = config;
        }

        /// <summary>Seed da run (ofertas determinísticas; RunConfig.FixedSeed quando UseFixedSeed).</summary>
        public int Seed { get; }

        public RunConfig Config { get; }
    }

    /// <summary>
    /// A fase começou: cena carregada, jogador posicionado, timer pode correr (CarregandoFase → Jogando).
    /// Emissor: RunManager. Ouvintes: LevelTimer, HUD.
    /// Invariante: vem depois do PlayerSpawned e do PlayerStatsChanged da mesma fase.
    /// </summary>
    public readonly struct LevelStarted : IEvent
    {
        public LevelStarted(int levelIndex, LevelDefinition level, float effectiveTimeLimit)
        {
            LevelIndex = levelIndex;
            Level = level;
            EffectiveTimeLimit = effectiveTimeLimit;
        }

        /// <summary>Índice da fase em RunConfig.Levels (base 0).</summary>
        public int LevelIndex { get; }

        public LevelDefinition Level { get; }

        /// <summary>Limite desta fase já com TimeLimitBonus (s). É o valor que o LevelTimer usa para expirar.</summary>
        public float EffectiveTimeLimit { get; }
    }

    // ─────────────────────────── Jogador ───────────────────────────

    /// <summary>
    /// O jogador foi instanciado (ou reaproveitado) e posicionado no spawn point.
    /// Emissor: o spawner (hoje SceneController; na 3.1, o RunManager). Ouvintes: CameraFollow, EnemyAI, LevelTimer,
    /// Minimap e o dono dos stats (que reemite PlayerStatsChanged, ADR-16).
    /// Invariante: Player não nulo, já com Awake/OnEnable executados. Substitui SceneController.OnPlayerSpawned.
    /// </summary>
    public readonly struct PlayerSpawned : IEvent
    {
        public PlayerSpawned(GameObject player)
        {
            Player = player;
        }

        public GameObject Player { get; }
    }

    /// <summary>
    /// O jogador morreu por algo do cenário/inimigo. Emissor: PlayerController (movimento, no tick em que o comando
    /// Die é aplicado; ex.: EnemyBullet → characterMovement.Die(DeathCause.EnemyProjectile)). Ouvintes: RunManager,
    /// LevelTimer (para), feedback, RumbleService.
    /// Invariantes: no máximo um por vida do jogador (Die é idempotente, RF-41); NUNCA emitido por causa de tempo
    /// esgotado (isso é LevelTimeExpired, ADR-10). Payload estendido pelo SPEC §9.1: posição dos pés e tick.
    /// </summary>
    public readonly struct PlayerDied : IEvent
    {
        public PlayerDied(DeathCause cause, Vector2 position = default, long tick = -1)
        {
            Cause = cause;
            Position = position;
            Tick = tick;
        }

        public DeathCause Cause { get; }

        /// <summary>Pés do jogador na morte.</summary>
        public Vector2 Position { get; }

        /// <summary>Tick de simulação da morte (−1 quando não há relógio de simulação).</summary>
        public long Tick { get; }
    }

    /// <summary>
    /// Os stats finais do jogador mudaram ou precisam ser reaplicados. Emissor: RunManager (dono do RunState/PlayerStats),
    /// depois de RunStarted, de UpgradeSelected e de cada PlayerSpawned. Ouvintes: PlayerController (movimento: QueueStats, vale no tick seguinte), HUD.
    /// Invariante: Stats.IsValid. Quem ouve aplica o snapshot inteiro (é idempotente receber o mesmo valor de novo).
    /// </summary>
    public readonly struct PlayerStatsChanged : IEvent
    {
        public PlayerStatsChanged(PlayerStatsSnapshot stats)
        {
            Stats = stats;
        }

        public PlayerStatsSnapshot Stats { get; }
    }

    // ───────────────────────────── Fase ─────────────────────────────

    /// <summary>
    /// O tempo da fase mudou de décimo de segundo. Emissor: LevelTimer (conta para cima). Ouvintes: HUD e RunManager
    /// (que guarda o último valor como tempo oficial da fase, ADR-13).
    /// Invariantes: ElapsedSeconds = floor(tempo × 10) / 10; emitido com 0 no LevelStarted e depois só quando o
    /// valor muda; nunca depois de LevelGoalReached, LevelTimeExpired ou PlayerDied na mesma fase.
    /// </summary>
    public readonly struct LevelTimeChanged : IEvent
    {
        public LevelTimeChanged(float elapsedSeconds, float effectiveTimeLimit)
        {
            ElapsedSeconds = elapsedSeconds;
            EffectiveTimeLimit = effectiveTimeLimit;
        }

        /// <summary>Tempo decorrido na fase (s), múltiplo de 0,1.</summary>
        public float ElapsedSeconds { get; }

        /// <summary>Limite efetivo da fase (s), o mesmo do LevelStarted.</summary>
        public float EffectiveTimeLimit { get; }

        /// <summary>Quanto falta para o limite (s), nunca negativo.</summary>
        public float RemainingSeconds => Math.Max(0f, EffectiveTimeLimit - ElapsedSeconds);
    }

    /// <summary>
    /// O tempo da fase chegou ao limite efetivo (Jogando → Derrota). Emissor: LevelTimer.
    /// Ouvintes: RunManager, PlayerController (trava o jogador, SEM emitir PlayerDied, ADR-10).
    /// Invariante: no máximo um por fase; não é emitido se LevelGoalReached ou PlayerDied vieram antes.
    /// </summary>
    public readonly struct LevelTimeExpired : IEvent { }

    /// <summary>
    /// O jogador tocou o objetivo da fase (Jogando → Resultado). Emissor: EndGoal. Ouvintes: LevelTimer (para), RunManager.
    /// Invariante: no máximo um por fase (EndGoal desliga o próprio trigger).
    /// </summary>
    public readonly struct LevelGoalReached : IEvent { }

    /// <summary>
    /// Resultado da fase calculado (entrada em Resultado). Emissor: RunManager, com RunFlow.LastLevelResult.
    /// Ouvintes: LevelResultView, telemetria.
    /// </summary>
    public readonly struct LevelCompleted : IEvent
    {
        public LevelCompleted(LevelResult result)
        {
            Result = result;
        }

        /// <summary>Índice, tempo, limite efetivo, tempo-alvo, desempenho 0–1 e nota.</summary>
        public LevelResult Result { get; }
    }

    // ─────────────────────────── Upgrades ───────────────────────────

    /// <summary>
    /// Ofertas prontas para escolha (entrada em EscolhaUpgrade). Emissor: RunManager, com RunFlow.CurrentOffers.
    /// Ouvintes: UpgradeSelectionView.
    /// Invariantes: Offers.Count == RunConfig.OfferCount; slots vazios têm IsEmpty (a view os esconde); ao menos um
    /// não vazio (se todos fossem vazios o RunFlow pula a escolha e este evento não é emitido). Lista copiada: imutável.
    /// </summary>
    public readonly struct UpgradeOffersGenerated : IEvent
    {
        public UpgradeOffersGenerated(int levelIndex, IReadOnlyList<UpgradeOffer> offers)
        {
            if (offers == null) throw new ArgumentNullException(nameof(offers));

            LevelIndex = levelIndex;
            var copy = new UpgradeOffer[offers.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = offers[i];
            }
            Offers = copy;
        }

        /// <summary>Índice da fase recém-concluída que gerou estas ofertas.</summary>
        public int LevelIndex { get; }

        public IReadOnlyList<UpgradeOffer> Offers { get; }
    }

    /// <summary>
    /// O jogador escolheu um upgrade (EscolhaUpgrade → CarregandoFase). Emissor: UpgradeSelectionView.
    /// Ouvintes: RunManager (RunFlow.SelectUpgrade, que aplica ao PlayerStats; depois emite PlayerStatsChanged).
    /// Invariante: Upgrade é uma das ofertas não vazias do último UpgradeOffersGenerated (senão o RunFlow ignora).
    /// </summary>
    public readonly struct UpgradeSelected : IEvent
    {
        public UpgradeSelected(UpgradeDefinition upgrade)
        {
            Upgrade = upgrade;
        }

        public UpgradeDefinition Upgrade { get; }
    }

    /// <summary>
    /// A run acabou (entrada em Vitoria ou Derrota). Emissor: RunManager, com RunFlow.Summary.
    /// Ouvintes: RunEndView, telemetria.
    /// </summary>
    public readonly struct RunEnded : IEvent
    {
        public RunEnded(RunSummary summary)
        {
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        }

        public RunSummary Summary { get; }

        /// <summary>Vitória ou derrota (derivado de Summary.EndReason).</summary>
        public bool IsVictory => Summary != null && Summary.IsVictory;
    }

    // ──────────── Decisões do jogador na UI (extensão da §3.4, ADR-14) ────────────

    /// <summary>
    /// O jogador pediu uma run nova no menu principal. Emissor: MainMenu (botão "Nova Run").
    /// Ouvintes: RunManager (RunFlow.StartRun). Ignorado fora do estado Menu.
    /// </summary>
    public readonly struct NewRunRequested : IEvent { }

    /// <summary>
    /// O jogador fechou a tela de resultado da fase. Emissor: LevelResultView. Ouvintes: RunManager
    /// (RunFlow.ContinueFromResult → EscolhaUpgrade ou Vitoria).
    /// </summary>
    public readonly struct LevelResultDismissed : IEvent { }

    /// <summary>
    /// O jogador fechou a tela de fim de run (vitória ou derrota). Emissor: RunEndView. Ouvintes: RunManager
    /// (RunFlow.ReturnToMenu e carregar o menu).
    /// </summary>
    public readonly struct RunEndDismissed : IEvent { }
}
