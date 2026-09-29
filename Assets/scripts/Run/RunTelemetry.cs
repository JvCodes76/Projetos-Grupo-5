using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Roguelike.Events;
using Roguelike.Run;
using UnityEngine;

/// <summary>
/// Adaptador do <see cref="RunTelemetryRecorder"/> (tarefa 4.3): ouve os fatos da run no bus e grava uma linha de
/// CSV por fase jogada, para o time analisar depois (tarefa 4.4). Fica no prefab RunSystems (DDOL), ao lado do
/// RunManager; não o referencia nem é referenciado por ele.
/// A telemetria nunca pode quebrar o jogo: qualquer erro de IO vira aviso no Console e o jogo segue sem gravar.
/// </summary>
public class RunTelemetry : MonoBehaviour
{
    [Header("Telemetria")]
    [Tooltip("Desliga a gravação sem remover o componente.")]
    [SerializeField] private bool enableTelemetry = true;

    [Tooltip("Nome do arquivo CSV dentro de Application.persistentDataPath.")]
    [SerializeField] private string fileName = "telemetria_runs.csv";

    [Tooltip("Loga o caminho completo do arquivo uma vez, ao iniciar.")]
    [SerializeField] private bool logPathOnStart = true;

    private RunTelemetryRecorder recorder;
    private string filePath;

    private void Awake()
    {
        recorder = new RunTelemetryRecorder(Application.version);
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        if (logPathOnStart)
        {
            Debug.Log($"[RunTelemetry] - Gravando em {filePath}");
        }
    }

    private void OnEnable()
    {
        EventBus<RunStarted>.Subscribe(HandleRunStarted);
        EventBus<LevelStarted>.Subscribe(HandleLevelStarted);
        EventBus<LevelTimeChanged>.Subscribe(HandleLevelTimeChanged);
        EventBus<LevelCompleted>.Subscribe(HandleLevelCompleted);
        EventBus<UpgradeOffersGenerated>.Subscribe(HandleUpgradeOffersGenerated);
        EventBus<UpgradeSelected>.Subscribe(HandleUpgradeSelected);
        EventBus<RunEnded>.Subscribe(HandleRunEnded);
    }

    private void OnDisable()
    {
        EventBus<RunStarted>.Unsubscribe(HandleRunStarted);
        EventBus<LevelStarted>.Unsubscribe(HandleLevelStarted);
        EventBus<LevelTimeChanged>.Unsubscribe(HandleLevelTimeChanged);
        EventBus<LevelCompleted>.Unsubscribe(HandleLevelCompleted);
        EventBus<UpgradeOffersGenerated>.Unsubscribe(HandleUpgradeOffersGenerated);
        EventBus<UpgradeSelected>.Unsubscribe(HandleUpgradeSelected);
        EventBus<RunEnded>.Unsubscribe(HandleRunEnded);
    }

    private void HandleRunStarted(RunStarted evt)
    {
        if (!enableTelemetry) return;

        recorder.BeginRun(evt.Seed, DateTime.UtcNow);
    }

    private void HandleLevelStarted(LevelStarted evt)
    {
        if (!enableTelemetry) return;

        WriteLines(recorder.LevelStarted(evt.LevelIndex, evt.Level, evt.EffectiveTimeLimit));
    }

    private void HandleLevelTimeChanged(LevelTimeChanged evt)
    {
        if (!enableTelemetry) return;

        recorder.TimeChanged(evt.ElapsedSeconds);
    }

    private void HandleLevelCompleted(LevelCompleted evt)
    {
        if (!enableTelemetry) return;

        recorder.LevelCompleted(evt.Result);
    }

    private void HandleUpgradeOffersGenerated(UpgradeOffersGenerated evt)
    {
        if (!enableTelemetry) return;

        recorder.OffersGenerated(evt.Offers);
    }

    private void HandleUpgradeSelected(UpgradeSelected evt)
    {
        if (!enableTelemetry) return;

        WriteLines(recorder.UpgradeSelected(evt.Upgrade));
    }

    private void HandleRunEnded(RunEnded evt)
    {
        if (!enableTelemetry) return;

        WriteLines(recorder.RunEnded(evt.Summary));
    }

    // ───────────────────────────── Gravação em disco ─────────────────────────────

    private void WriteLines(IReadOnlyList<string> lines)
    {
        if (lines == null || lines.Count == 0) return;

        try
        {
            EnsureFileWithHeader();

            var sb = new StringBuilder();
            foreach (string line in lines)
            {
                sb.Append(line).Append('\n');
            }

            File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception e)
        {
            // Telemetria não pode derrubar a run: erro de IO (arquivo bloqueado, sem permissão, disco cheio...) é só aviso.
            Debug.LogWarning($"[RunTelemetry] - Falha ao gravar em '{filePath}': {e.Message}");
        }
    }

    private void EnsureFileWithHeader()
    {
        // Checado a cada escrita (não só na primeira) para tentar de novo se uma falha anterior impediu a criação.
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, RunTelemetryRecorder.Header + "\n", Encoding.UTF8);
        }
    }
}
