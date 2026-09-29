using System;
using System.Collections.Generic;
using Roguelike.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Relógio único de simulação a 60 Hz (SPEC §3.2, DS-02): acumulador próprio no Update, sem mudar o
/// Time.fixedDeltaTime do projeto. Avança, em ordem de TickOrder, o jogador (0), a câmera (100) e quem mais se
/// registrar. DDOL, criado sob demanda (funciona no "Play direto" e no gym). Freeze e pausa não avançam o relógio.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class SimulationRunner : MonoBehaviour
{
    private static SimulationRunner instance;

    private readonly SimulationLoop loop = new SimulationLoop();
    private readonly List<ITickable> tickables = new List<ITickable>(8);
    private ITickable[] ordered = Array.Empty<ITickable>();
    private bool orderDirty;
    private bool wasPaused;

    /// <summary>Instância ativa; criada na primeira leitura em Play Mode.</summary>
    public static SimulationRunner Instance
    {
        get
        {
            if (instance == null && Application.isPlaying) Create();
            return instance;
        }
    }

    public static bool Exists => instance != null;

    /// <summary>Fração do próximo tick (interpolação de render). 1 sem runner.</summary>
    public static float Alpha => instance != null ? instance.loop.Alpha : 1f;

    /// <summary>Relógio de simulação (ticks).</summary>
    public static ISimulationClock Clock => Instance != null ? instance.loop : null;

    public SimulationLoop Loop => loop;

    /// <summary>Pausa lógica (menus), além do timeScale = 0.</summary>
    public static bool Paused
    {
        get => instance != null && instance.loop.Paused;
        set
        {
            if (Instance != null) instance.loop.Paused = value;
        }
    }

    public static void Register(ITickable tickable)
    {
        if (tickable == null || Instance == null) return;
        if (instance.tickables.Contains(tickable)) return;
        instance.tickables.Add(tickable);
        instance.orderDirty = true;
    }

    public static void Unregister(ITickable tickable)
    {
        if (tickable == null || instance == null) return;
        if (instance.tickables.Remove(tickable)) instance.orderDirty = true;
    }

    /// <summary>Hitstop local em ticks (nunca Time.timeScale). Ignorado com a opção de freeze desligada.</summary>
    public static void RequestFreeze(int ticks)
    {
        if (instance == null) return;
        instance.loop.FreezeEnabled = FeedbackSettings.FreezeEnabled;
        instance.loop.RequestFreeze(ticks);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null) Create();
    }

    private static void Create()
    {
        var go = new GameObject(nameof(SimulationRunner));
        instance = go.AddComponent<SimulationRunner>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // O frame depois de carregar uma cena traz o custo do carregamento no deltaTime: sem isto ele viraria uma
    // rajada de ticks de recuperação (até MaxStepsPerFrame) no primeiro frame da fase.
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        loop.ResetAccumulator();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (orderDirty) RebuildOrder();
        ITickable[] current = ordered;

        bool paused = Time.timeScale <= 0f || loop.Paused;
        if (wasPaused && !paused)
        {
            for (int i = 0; i < current.Length; i++) current[i].OnResume();
        }

        wasPaused = paused;

        for (int i = 0; i < current.Length; i++) current[i].OnFrameStart();

        loop.FreezeEnabled = FeedbackSettings.FreezeEnabled;
        loop.BeginFrame(Time.deltaTime);

        bool synced = false;
        StepKind kind;
        while ((kind = loop.NextStep()) != StepKind.None)
        {
            if (kind != StepKind.Tick) continue;

            // Inimigos andam por transform: a física precisa ver as poses do frame antes das consultas do tick.
            if (!synced)
            {
                Physics2D.SyncTransforms();
                synced = true;
            }

            if (orderDirty) RebuildOrder();
            current = ordered;
            var ctx = new TickContext(loop.Tick, SimulationLoop.FixedDt);
            for (int i = 0; i < current.Length; i++) current[i].Tick(ctx);
        }
    }

    private void RebuildOrder()
    {
        orderDirty = false;
        tickables.Sort((a, b) => a.TickOrder.CompareTo(b.TickOrder));
        ordered = tickables.ToArray();
    }
}
