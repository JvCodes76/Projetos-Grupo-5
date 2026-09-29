using System.Collections;
using System.Collections.Generic;
using Roguelike.Events;
using Roguelike.Run;
using Roguelike.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tela de fim de run (vitória ou derrota). Só ouve e emite eventos: nenhuma referência ao RunManager.
/// GameObject sempre ativo; o <see cref="panel"/> filho é que liga/desliga.
/// Na derrota o jogo não é pausado (a animação de morte continua), então o atraso antes de mostrar usa
/// tempo real (<see cref="WaitForSecondsRealtime"/>), não Time.timeScale.
/// </summary>
public class RunEndView : MonoBehaviour
{
    [Header("Painel")]
    [Tooltip("Obrigatório. GameObject filho ligado/desligado por esta view; começa escondido.")]
    [SerializeField] private GameObject panel;

    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI reasonText;

    [Tooltip("Fases concluídas N/M e tempo total.")]
    [SerializeField] private TextMeshProUGUI summaryText;

    [Tooltip("Uma linha por fase concluída: \"Fase 1 · 00:14.2 · A\".")]
    [SerializeField] private TextMeshProUGUI levelsText;

    [Tooltip("Upgrades adquiridos agrupados: \"Upgrades: Pulo Duplo Habilitado, Aumento leve no pulo ×2\"; \"Upgrades: nenhum\" se vazio.")]
    [SerializeField] private TextMeshProUGUI upgradesText;

    [Tooltip("Opcional. \"Seed 1234\", para QA.")]
    [SerializeField] private TextMeshProUGUI seedText;

    [Header("Botão")]
    [Tooltip("Obrigatório. Volta ao menu (RunEndDismissed).")]
    [SerializeField] private Button menuButton;

    [Header("Cores e atraso")]
    [SerializeField] private Color victoryColor = Color.white;
    [SerializeField] private Color defeatColor = Color.white;

    [Tooltip("Segundos (tempo real) de espera antes de mostrar a tela na derrota. A vitória mostra na hora.")]
    [SerializeField] private float defeatShowDelay = 1.2f;

    private Coroutine pendingShow;

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (menuButton != null)
        {
            menuButton.onClick.AddListener(HandleMenuClicked);
        }
    }

    private void OnEnable()
    {
        EventBus<RunEnded>.Subscribe(HandleRunEnded);
        EventBus<RunStarted>.Subscribe(HandleRunStarted);
    }

    private void OnDisable()
    {
        EventBus<RunEnded>.Unsubscribe(HandleRunEnded);
        EventBus<RunStarted>.Unsubscribe(HandleRunStarted);
    }

    private void HandleRunStarted(RunStarted evt)
    {
        if (pendingShow != null)
        {
            StopCoroutine(pendingShow);
            pendingShow = null;
        }

        SetPanelActive(false);
    }

    private void HandleRunEnded(RunEnded evt)
    {
        RunSummary summary = evt.Summary;
        if (summary == null)
        {
            Debug.LogWarning("[RunEndView] - RunEnded sem Summary; ignorando");
            return;
        }

        if (evt.IsVictory)
        {
            ShowSummary(summary, true);
        }
        else
        {
            if (pendingShow != null)
            {
                StopCoroutine(pendingShow);
            }
            pendingShow = StartCoroutine(ShowAfterDelay(summary));
        }
    }

    private IEnumerator ShowAfterDelay(RunSummary summary)
    {
        yield return new WaitForSecondsRealtime(defeatShowDelay);
        pendingShow = null;
        ShowSummary(summary, false);
    }

    private void ShowSummary(RunSummary summary, bool isVictory)
    {
        Color titleColor = isVictory ? victoryColor : defeatColor;

        if (titleText != null)
        {
            titleText.text = isVictory ? "Vitória!" : "Fim da run";
            titleText.color = titleColor;
        }

        if (reasonText != null)
        {
            reasonText.text = isVictory ? "Todas as fases concluídas!" : BuildReasonText(summary);
        }

        if (summaryText != null)
        {
            summaryText.text = $"{summary.LevelsCompleted}/{summary.LevelCount} fases · {TimeFormat.Format(summary.TotalTimeSeconds)}";
        }

        if (levelsText != null)
        {
            levelsText.text = BuildLevelsText(summary);
        }

        if (upgradesText != null)
        {
            upgradesText.text = BuildUpgradesText(summary);
        }

        if (seedText != null)
        {
            seedText.text = $"Seed {summary.Seed}";
        }

        if (menuButton != null)
        {
            menuButton.interactable = true;
        }

        SetPanelActive(true);

        // Sem ?. : EventSystem é UnityEngine.Object e o operador ignoraria a checagem de objeto destruído.
        if (menuButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(menuButton.gameObject);
        }
    }

    private static string BuildReasonText(RunSummary summary)
    {
        int failedLevelNumber = summary.FailedLevelIndex + 1;

        switch (summary.EndReason)
        {
            case RunEndReason.TimeExpired:
                return $"Tempo esgotado na fase {failedLevelNumber}";

            case RunEndReason.PlayerDied:
                string cause = DeathCauseText(summary.DeathCause);
                return string.IsNullOrEmpty(cause)
                    ? $"Você morreu na fase {failedLevelNumber}"
                    : $"Você morreu na fase {failedLevelNumber}, {cause}";

            default:
                return string.Empty;
        }
    }

    private static string DeathCauseText(DeathCause cause)
    {
        switch (cause)
        {
            case DeathCause.EnemyProjectile:
                return "atingido por um tiro";
            case DeathCause.EnemyContact:
                return "tocou um inimigo";
            case DeathCause.Hazard:
                return "caiu numa armadilha";
            case DeathCause.OutOfBounds:
                return "caiu do mapa";
            default:
                return string.Empty;
        }
    }

    private static string BuildLevelsText(RunSummary summary)
    {
        IReadOnlyList<LevelResult> results = summary.LevelResults;
        if (results.Count == 0)
        {
            return string.Empty;
        }

        var lines = new string[results.Count];
        for (int i = 0; i < results.Count; i++)
        {
            LevelResult result = results[i];
            lines[i] = $"Fase {result.LevelIndex + 1} · {TimeFormat.Format(result.ElapsedSeconds)} · {result.Grade}";
        }

        return string.Join("\n", lines);
    }

    private static string BuildUpgradesText(RunSummary summary)
    {
        IReadOnlyList<UpgradeDefinition> acquired = summary.AcquiredUpgrades;
        if (acquired.Count == 0)
        {
            return "Upgrades: nenhum";
        }

        var order = new List<UpgradeDefinition>();
        var counts = new Dictionary<UpgradeDefinition, int>();

        foreach (UpgradeDefinition upgrade in acquired)
        {
            if (upgrade == null)
            {
                continue;
            }

            if (!counts.ContainsKey(upgrade))
            {
                counts[upgrade] = 0;
                order.Add(upgrade);
            }

            counts[upgrade]++;
        }

        if (order.Count == 0)
        {
            return "Upgrades: nenhum";
        }

        var parts = new List<string>(order.Count);
        foreach (UpgradeDefinition upgrade in order)
        {
            int count = counts[upgrade];
            parts.Add(count > 1 ? $"{upgrade.DisplayName} ×{count}" : upgrade.DisplayName);
        }

        return "Upgrades: " + string.Join(", ", parts);
    }

    private void HandleMenuClicked()
    {
        if (menuButton != null)
        {
            menuButton.interactable = false;
        }

        SetPanelActive(false);

        Debug.Log("[RunEndView] - Menu clicado, emitindo RunEndDismissed");
        EventBus<RunEndDismissed>.Raise(new RunEndDismissed());
    }

    private void SetPanelActive(bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
