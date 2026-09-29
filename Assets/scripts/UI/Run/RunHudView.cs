using System.Collections.Generic;
using Roguelike.Events;
using Roguelike.Run;
using Roguelike.Stats;
using Roguelike.Upgrades;
using TMPro;
using UnityEngine;

/// <summary>
/// HUD da fase: fase atual, tempo decorrido/limite, alvo e habilidades liberadas.
/// Só ouve e emite eventos (§3 do PLANO_REFACTOR_ROGUELIKE.md): nenhuma referência ao RunManager.
/// O GameObject desta view fica sempre ativo; quem liga/desliga é o <see cref="panel"/> filho
/// (senão o OnEnable não rodaria de novo e a view pararia de ouvir).
/// </summary>
public class RunHudView : MonoBehaviour
{
    [Header("Painel")]
    [Tooltip("Obrigatório. GameObject filho ligado/desligado por esta view; começa escondido.")]
    [SerializeField] private GameObject panel;

    [Header("Textos")]
    [Tooltip("Obrigatório. Ex.: \"Fase 2 · 2/3\" (nome da fase · posição na run).")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("Obrigatório. Ex.: \"00:18.4 / 00:30.0\".")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Tooltip("Opcional. Ex.: \"Alvo 00:18.0\".")]
    [SerializeField] private TextMeshProUGUI targetText;

    [Tooltip("Opcional. Lista as habilidades liberadas (\"Pulo duplo · Gancho\"); vazio no kit base.")]
    [SerializeField] private TextMeshProUGUI abilitiesText;

    [Header("Cores do tempo")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.red;

    [Tooltip("Quando RemainingSeconds <= este valor, o tempo fica na cor de alerta.")]
    [SerializeField] private float warningSeconds = 5f;

    private int levelCount;

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        EventBus<RunStarted>.Subscribe(HandleRunStarted);
        EventBus<LevelStarted>.Subscribe(HandleLevelStarted);
        EventBus<LevelTimeChanged>.Subscribe(HandleLevelTimeChanged);
        EventBus<PlayerStatsChanged>.Subscribe(HandlePlayerStatsChanged);
        EventBus<RunEnded>.Subscribe(HandleRunEnded);
        EventBus<RunEndDismissed>.Subscribe(HandleRunEndDismissed);
    }

    private void OnDisable()
    {
        EventBus<RunStarted>.Unsubscribe(HandleRunStarted);
        EventBus<LevelStarted>.Unsubscribe(HandleLevelStarted);
        EventBus<LevelTimeChanged>.Unsubscribe(HandleLevelTimeChanged);
        EventBus<PlayerStatsChanged>.Unsubscribe(HandlePlayerStatsChanged);
        EventBus<RunEnded>.Unsubscribe(HandleRunEnded);
        EventBus<RunEndDismissed>.Unsubscribe(HandleRunEndDismissed);
    }

    private void HandleRunStarted(RunStarted evt)
    {
        levelCount = evt.Config != null ? evt.Config.Levels.Count : 0;
        SetPanelActive(false);
    }

    private void HandleLevelStarted(LevelStarted evt)
    {
        SetPanelActive(true);

        if (levelText != null && evt.Level != null)
        {
            levelText.text = $"{evt.Level.DisplayName} · {evt.LevelIndex + 1}/{levelCount}";
        }

        if (targetText != null && evt.Level != null)
        {
            targetText.text = $"Alvo {TimeFormat.Format(evt.Level.TargetTime)}";
        }

        if (timeText != null)
        {
            timeText.text = $"{TimeFormat.Format(0f)} / {TimeFormat.Format(evt.EffectiveTimeLimit)}";
            timeText.color = normalColor;
        }
    }

    private void HandleLevelTimeChanged(LevelTimeChanged evt)
    {
        if (timeText == null)
        {
            return;
        }

        timeText.text = $"{TimeFormat.Format(evt.ElapsedSeconds)} / {TimeFormat.Format(evt.EffectiveTimeLimit)}";
        timeText.color = evt.RemainingSeconds <= warningSeconds ? warningColor : normalColor;
    }

    private void HandlePlayerStatsChanged(PlayerStatsChanged evt)
    {
        if (abilitiesText == null || !evt.Stats.IsValid)
        {
            return;
        }

        var parts = new List<string>();

        int maxAirJumps = evt.Stats.GetInt(StatType.MaxAirJumps);
        if (maxAirJumps >= 1)
        {
            int totalJumps = maxAirJumps + 1;
            parts.Add(totalJumps > 2 ? $"Pulo ×{totalJumps}" : "Pulo duplo");
        }

        if (evt.Stats.HasAbility(AbilityFlags.WallJump))
        {
            parts.Add("Wall jump");
        }

        if (evt.Stats.HasAbility(AbilityFlags.GrapplingHook))
        {
            parts.Add("Gancho");
        }

        int maxDashes = evt.Stats.GetInt(StatType.MaxDashes);
        if (maxDashes >= 1)
        {
            parts.Add(maxDashes > 1 ? $"Dash ×{maxDashes}" : "Dash");
        }

        abilitiesText.text = string.Join(" · ", parts);
    }

    private void HandleRunEnded(RunEnded evt)
    {
        SetPanelActive(false);
    }

    private void HandleRunEndDismissed(RunEndDismissed evt)
    {
        SetPanelActive(false);
    }

    private void SetPanelActive(bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
