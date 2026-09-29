using System.Globalization;
using Roguelike.Events;
using Roguelike.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adaptador do <see cref="LevelClock"/> (tarefa 3.1): conta o tempo da fase e o publica no bus.
/// Fica no prefab RunSystems (DDOL), não nas cenas das fases: o LevelStarted já traz o limite efetivo.
/// Não referencia o RunManager nem a UI.
/// Emite: LevelTimeChanged (0 no LevelStarted; depois só quando o décimo muda) e LevelTimeExpired (no máximo um por
/// fase, depois do último LevelTimeChanged). Nada é emitido depois do fim da fase (LevelGoalReached, PlayerDied,
/// RunEnded ou troca de cena).
/// Usa Time.deltaTime (tempo escalado): com timeScale = 0 o timer congela.
/// </summary>
public class LevelTimer : MonoBehaviour
{
    private readonly LevelClock clock = new LevelClock();

    // Cena ativa quando a fase começou. Se outra cena for carregada no modo Single sem que a fase tenha terminado
    // por evento (ex.: um botão legado que carrega o menu direto), o relógio para em silêncio.
    private Scene levelScene;

    // Incrementado a cada início e fim de fase: um handler que encerre a fase durante o Raise do LevelTimeChanged
    // impede o LevelTimeExpired do mesmo tick.
    private int levelSerial;

    // O LevelStarted chega no frame N em que a cena foi ativada (Awake de cenas grandes). Esse custo aparece no
    // deltaTime do frame N+1 (até Time.maximumDeltaTime), tempo que o jogador não jogou. Os ticks dos frames N e N+1
    // são descartados: no máximo ~2 frames a favor do jogador, dentro do erro de 0,1 s aceito pela ADR-13.
    private int ignoreTicksUntilFrame = -1;

    /// <summary>O relógio está contando (leitura para ferramentas e testes em Play Mode).</summary>
    public bool IsRunning => clock.IsRunning;

    private void OnEnable()
    {
        EventBus<LevelStarted>.Subscribe(HandleLevelStarted);
        EventBus<LevelGoalReached>.Subscribe(HandleLevelGoalReached);
        EventBus<PlayerDied>.Subscribe(HandlePlayerDied);
        EventBus<RunEnded>.Subscribe(HandleRunEnded);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        EventBus<LevelStarted>.Unsubscribe(HandleLevelStarted);
        EventBus<LevelGoalReached>.Unsubscribe(HandleLevelGoalReached);
        EventBus<PlayerDied>.Unsubscribe(HandlePlayerDied);
        EventBus<RunEnded>.Unsubscribe(HandleRunEnded);
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        // Desligado, o timer não ouve o fim da fase: parar aqui garante que ele nunca emita depois dele.
        StopClock();
    }

    private void Update()
    {
        if (!clock.IsRunning) return;

        if (Time.frameCount <= ignoreTicksUntilFrame) return;

        int serial = levelSerial;
        LevelClockTick tick = clock.Tick(Time.deltaTime);

        if (tick.DisplayChanged)
        {
            EventBus<LevelTimeChanged>.Raise(new LevelTimeChanged(tick.DisplayedSeconds, clock.TimeLimit));
        }

        if (tick.Expired && serial == levelSerial)
        {
            Debug.Log($"[LevelTimer] - Tempo esgotado ({FormatSeconds(tick.DisplayedSeconds)}s de {FormatSeconds(clock.TimeLimit)}s)");
            EventBus<LevelTimeExpired>.Raise(new LevelTimeExpired());
        }
    }

    private void HandleLevelStarted(LevelStarted evt)
    {
        levelSerial++;
        clock.Start(evt.EffectiveTimeLimit);
        levelScene = SceneManager.GetActiveScene();
        ignoreTicksUntilFrame = Time.frameCount + 1;

        EventBus<LevelTimeChanged>.Raise(new LevelTimeChanged(clock.DisplayedSeconds, clock.TimeLimit));
    }

    private void HandleLevelGoalReached(LevelGoalReached evt)
    {
        StopClock();
    }

    private void HandlePlayerDied(PlayerDied evt)
    {
        StopClock();
    }

    private void HandleRunEnded(RunEnded evt)
    {
        StopClock();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || !clock.IsRunning || scene == levelScene) return;

        Debug.LogWarning($"[LevelTimer] - Cena '{scene.name}' carregada com a fase em andamento; timer parado");
        StopClock();
    }

    private void StopClock()
    {
        levelSerial++;
        clock.Stop();
    }

    private static string FormatSeconds(float seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture);
}
