using System;
using System.Globalization;
using Roguelike.Events;
using Roguelike.Levels;
using Roguelike.Run;
using Roguelike.Upgrades;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adaptador DDOL do <see cref="RunFlow"/> (tarefa 3.1; substitui o SceneController). Ouve os fatos do bus, chama os
/// comandos do RunFlow, carrega as cenas (SceneLoader), instancia o jogador de cada fase e emite os eventos da run
/// na ordem do ARQUITETURA.md §7. É o dono do RunState/PlayerStats (ADR-02): emite PlayerStatsChanged depois de
/// RunStarted, de UpgradeSelected e de cada PlayerSpawned (ADR-16).
/// Comando do RunFlow que retorna false (evento fora de fase) só é logado; exceção de comando é bug e não é
/// capturada (ADR-15).
/// </summary>
public class RunManager : MonoBehaviour
{
    /// <summary>Instância ativa (a primeira criada). Leitura para ferramentas; as views não usam.</summary>
    public static RunManager Instance { get; private set; }

    [Header("Dados")]
    [SerializeField] private RunConfig runConfig;
    [Tooltip("Prefab do jogador (Cyborg.prefab). Cada fase instancia o seu; não é DDOL.")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Dependências")]
    [SerializeField] private SceneLoader sceneLoader;

    [Header("Cenas")]
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private string spawnPointTag = "SpawnPoint";

    [Header("Fluxo")]
    [Tooltip("Pausa o jogo (timeScale = 0) ao concluir a fase, enquanto o resultado e as ofertas estão na tela.")]
    [SerializeField] private bool pauseOnLevelResult = true;

    [Header("Debug")]
    [Tooltip("Liga EventBusRegistry.LogRaises: loga todo Raise do bus.")]
    [SerializeField] private bool logEventBus; // [DEBUG]

    private RunFlow flow;

    // Tempo oficial da fase: último LevelTimeChanged.ElapsedSeconds (ADR-13).
    private float lastElapsedSeconds;

    // O próximo carregamento do menu foi pedido por este RunManager (não é abandono da run).
    private bool menuLoadRequested;

    /// <summary>Fase atual da run. Leitura para ferramentas e para o smoke test da 4.2; as views não usam.</summary>
    public RunPhase Phase => flow != null ? flow.Phase : RunPhase.Menu;

    /// <summary>Estado da run ativa; null em Menu. Leitura para ferramentas; as views não usam.</summary>
    public RunState State => flow?.State;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // SetActive(false) dispara o OnDisable das views filhas agora, antes de ficarem inscritas por um frame.
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        flow = new RunFlow(new UpgradeOfferGenerator());

        if (logEventBus) // [DEBUG]
        {
            EventBusRegistry.LogRaises = true; // [DEBUG]
        }

        if (runConfig == null || playerPrefab == null || sceneLoader == null)
        {
            Debug.LogError("[RunManager] - RunConfig, prefab do jogador ou SceneLoader não atribuído; RunManager desligado");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        EventBus<NewRunRequested>.Subscribe(HandleNewRunRequested);
        EventBus<LevelTimeChanged>.Subscribe(HandleLevelTimeChanged);
        EventBus<LevelGoalReached>.Subscribe(HandleLevelGoalReached);
        EventBus<LevelTimeExpired>.Subscribe(HandleLevelTimeExpired);
        EventBus<PlayerDied>.Subscribe(HandlePlayerDied);
        EventBus<LevelResultDismissed>.Subscribe(HandleLevelResultDismissed);
        EventBus<UpgradeSelected>.Subscribe(HandleUpgradeSelected);
        EventBus<RunEndDismissed>.Subscribe(HandleRunEndDismissed);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        EventBus<NewRunRequested>.Unsubscribe(HandleNewRunRequested);
        EventBus<LevelTimeChanged>.Unsubscribe(HandleLevelTimeChanged);
        EventBus<LevelGoalReached>.Unsubscribe(HandleLevelGoalReached);
        EventBus<LevelTimeExpired>.Unsubscribe(HandleLevelTimeExpired);
        EventBus<PlayerDied>.Unsubscribe(HandlePlayerDied);
        EventBus<LevelResultDismissed>.Unsubscribe(HandleLevelResultDismissed);
        EventBus<UpgradeSelected>.Unsubscribe(HandleUpgradeSelected);
        EventBus<RunEndDismissed>.Unsubscribe(HandleRunEndDismissed);
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Instance = null;
        Time.timeScale = 1f;
    }

    // ───────────────────────────── Início da run e das fases ─────────────────────────────

    private void HandleNewRunRequested(NewRunRequested evt)
    {
        int seed = runConfig.UseFixedSeed ? runConfig.FixedSeed : Environment.TickCount; // ADR-21

        if (!flow.StartRun(runConfig, seed))
        {
            LogIgnored(nameof(NewRunRequested));
            return;
        }

        RunState state = flow.State;
        Debug.Log($"[RunManager] - Run iniciada (seed {seed}, {state.LevelCount} fases)");

        EventBus<RunStarted>.Raise(new RunStarted(seed, runConfig));
        EventBus<PlayerStatsChanged>.Raise(new PlayerStatsChanged(state.Stats.CreateSnapshot()));
        LoadCurrentLevel();
    }

    private void LoadCurrentLevel()
    {
        // O timeScale só volta a 1 quando a cena nova existe (OnLevelSceneLoaded): se voltasse aqui, a fase antiga
        // despausaria durante o carregamento assíncrono e um inimigo poderia matar o jogador parado na meta.
        lastElapsedSeconds = 0f;

        RunState state = flow.State;
        LevelDefinition level = state.CurrentLevel;
        Debug.Log($"[RunManager] - Carregando a fase {state.CurrentLevelIndex + 1}/{state.LevelCount} ('{level.SceneName}')");

        if (!sceneLoader.Load(level.SceneName, OnLevelSceneLoaded))
        {
            Time.timeScale = 1f;
            Debug.LogError($"[RunManager] - Não foi possível carregar a cena '{level.SceneName}'; a run ficou em {flow.Phase}");
        }
    }

    private void OnLevelSceneLoaded()
    {
        Time.timeScale = 1f;

        if (flow.Phase != RunPhase.LoadingLevel)
        {
            Debug.Log($"[RunManager] - Cena de fase carregada fora de LoadingLevel (fase {flow.Phase}); ignorada");
            return;
        }

        RunState state = flow.State;

        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        GameObject spawnPoint = GameObject.FindWithTag(spawnPointTag);
        if (spawnPoint != null)
        {
            position = spawnPoint.transform.position;
            rotation = spawnPoint.transform.rotation;
        }
        else
        {
            Debug.LogWarning($"[RunManager] - Nenhum objeto com a tag '{spawnPointTag}' na cena '{SceneManager.GetActiveScene().name}'; jogador na origem");
        }

        // O jogador não é DDOL: cada fase instancia o seu e a cena antiga destrói o anterior.
        GameObject player = Instantiate(playerPrefab, position, rotation);
        EventBus<PlayerSpawned>.Raise(new PlayerSpawned(player));
        EventBus<PlayerStatsChanged>.Raise(new PlayerStatsChanged(state.Stats.CreateSnapshot())); // ADR-16

        if (!flow.StartLevel())
        {
            LogIgnored("StartLevel");
            return;
        }

        LevelDefinition level = state.CurrentLevel;
        float effectiveTimeLimit = state.EffectiveTimeLimit;
        Debug.Log($"[RunManager] - Fase {state.CurrentLevelIndex + 1} iniciada ('{level.DisplayName}', limite {FormatSeconds(effectiveTimeLimit)}s)");

        EventBus<LevelStarted>.Raise(new LevelStarted(state.CurrentLevelIndex, level, effectiveTimeLimit));
    }

    // ───────────────────────────── Durante a fase ─────────────────────────────

    private void HandleLevelTimeChanged(LevelTimeChanged evt)
    {
        lastElapsedSeconds = evt.ElapsedSeconds; // ADR-13
    }

    private void HandleLevelGoalReached(LevelGoalReached evt)
    {
        if (!flow.CompleteLevel(lastElapsedSeconds))
        {
            LogIgnored(nameof(LevelGoalReached));
            return;
        }

        if (pauseOnLevelResult)
        {
            Time.timeScale = 0f;
        }

        LevelResult result = flow.LastLevelResult;
        Debug.Log($"[RunManager] - Fase {result.LevelIndex + 1} concluída em {FormatSeconds(result.ElapsedSeconds)}s (nota {result.Grade})");

        EventBus<LevelCompleted>.Raise(new LevelCompleted(result));
    }

    private void HandleLevelTimeExpired(LevelTimeExpired evt)
    {
        if (!flow.FailByTimeout())
        {
            LogIgnored(nameof(LevelTimeExpired));
            return;
        }

        // Derrota não pausa: a animação de morte continua e a RunEndView aparece com atraso em tempo real.
        Debug.Log($"[RunManager] - Tempo esgotado na fase {flow.State.CurrentLevelIndex + 1}; fim da run");
        EventBus<RunEnded>.Raise(new RunEnded(flow.Summary));
    }

    private void HandlePlayerDied(PlayerDied evt)
    {
        if (!flow.FailByDeath(evt.Cause))
        {
            LogIgnored(nameof(PlayerDied));
            return;
        }

        Debug.Log($"[RunManager] - Jogador morreu ({evt.Cause}) na fase {flow.State.CurrentLevelIndex + 1}; fim da run");
        EventBus<RunEnded>.Raise(new RunEnded(flow.Summary));
    }

    // ───────────────────────────── Decisões do jogador ─────────────────────────────

    private void HandleLevelResultDismissed(LevelResultDismissed evt)
    {
        if (!flow.ContinueFromResult())
        {
            LogIgnored(nameof(LevelResultDismissed));
            return;
        }

        switch (flow.Phase)
        {
            case RunPhase.Victory:
                // O jogo continua pausado até o RunEndDismissed.
                Debug.Log($"[RunManager] - Vitória! Run concluída em {FormatSeconds(flow.Summary.TotalTimeSeconds)}s");
                EventBus<RunEnded>.Raise(new RunEnded(flow.Summary));
                break;

            case RunPhase.UpgradeSelection:
                EventBus<UpgradeOffersGenerated>.Raise(
                    new UpgradeOffersGenerated(flow.LastLevelResult.LevelIndex, flow.CurrentOffers));
                break;

            case RunPhase.LoadingLevel:
                Debug.Log("[RunManager] - Nenhuma oferta elegível; seguindo direto para a próxima fase");
                LoadCurrentLevel();
                break;

            default:
                Debug.LogError($"[RunManager] - Fase inesperada depois de ContinueFromResult: {flow.Phase}");
                break;
        }
    }

    private void HandleUpgradeSelected(UpgradeSelected evt)
    {
        if (!flow.SelectUpgrade(evt.Upgrade))
        {
            LogIgnored(nameof(UpgradeSelected));
            return;
        }

        Debug.Log($"[RunManager] - Upgrade '{evt.Upgrade.Id}' adquirido");
        EventBus<PlayerStatsChanged>.Raise(new PlayerStatsChanged(flow.State.Stats.CreateSnapshot()));
        LoadCurrentLevel();
    }

    private void HandleRunEndDismissed(RunEndDismissed evt)
    {
        if (!flow.ReturnToMenu())
        {
            LogIgnored(nameof(RunEndDismissed));
            return;
        }

        Debug.Log("[RunManager] - Voltando ao menu");

        // Como nas fases, o timeScale só volta a 1 com o menu carregado (a vitória fica pausada até lá).
        menuLoadRequested = true;
        if (!sceneLoader.Load(menuSceneName, OnMenuSceneLoaded))
        {
            menuLoadRequested = false;
            Time.timeScale = 1f;
            Debug.LogError($"[RunManager] - Não foi possível carregar o menu '{menuSceneName}'");
        }
    }

    private void OnMenuSceneLoaded()
    {
        Time.timeScale = 1f;
    }

    // ───────────────────────────── Abandono ─────────────────────────────

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != menuSceneName) return;

        if (menuLoadRequested)
        {
            menuLoadRequested = false;
            return;
        }

        if (flow.Phase == RunPhase.Menu) return;

        // Menu carregado por fora do fluxo (ex.: botão legado até a 4.1): a run é descartada sem eventos.
        Debug.LogWarning($"[RunManager] - Menu carregado fora do fluxo da run (fase {flow.Phase}); run descartada");
        flow = new RunFlow(new UpgradeOfferGenerator());
        lastElapsedSeconds = 0f;
        Time.timeScale = 1f;
    }

    // ───────────────────────────── Utilitários ─────────────────────────────

    private void LogIgnored(string command)
    {
        Debug.Log($"[RunManager] - {command} ignorado na fase {flow.Phase}");
    }

    private static string FormatSeconds(float seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture);
}
