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

    [Tooltip("Opcional. Escondido enquanto não há habilidade liberada (ex.: a etiqueta atrás do texto).")]
    [SerializeField] private GameObject abilitiesRoot;

    [Tooltip("Opcional. Se atribuído, o limite (\"/ 00:30.0\") vai aqui e o timeText mostra só o tempo decorrido.")]
    [SerializeField] private TextMeshProUGUI limitText;

    [Tooltip("Opcional. Barra que enche conforme o tempo passa (decorrido / limite) e muda de cor no alerta.")]
    [SerializeField] private UiSlant timeBar;

    [Tooltip("Largura fixa de cada caractere do tempo, em em (0 = desligado). Evita o texto 'tremer' em fontes não tabulares.")]
    [SerializeField] private float digitWidthEm;

    [Header("Cores do tempo")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.red;

    [Tooltip("Quando RemainingSeconds <= este valor, o tempo fica na cor de alerta.")]
    [SerializeField] private float warningSeconds = 5f;

    private int levelCount;
    private Color timeBarNormalColor;
    private PlayerStatsSnapshot lastStats;
    private int grappleUsesLeft;
    private int grappleUsesMax;

    private void Awake()
    {
        if (timeBar != null)
        {
            timeBarNormalColor = timeBar.color;
        }

        if (abilitiesRoot != null)
        {
            abilitiesRoot.SetActive(false);
        }

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
        EventBus<LevelCompleted>.Subscribe(HandleLevelCompleted);
        EventBus<PlayerStatsChanged>.Subscribe(HandlePlayerStatsChanged);
        EventBus<PlayerGrappleUsesChanged>.Subscribe(HandleGrappleUsesChanged);
        EventBus<RunEnded>.Subscribe(HandleRunEnded);
        EventBus<RunAbandoned>.Subscribe(HandleRunAbandoned);
        EventBus<RunEndDismissed>.Subscribe(HandleRunEndDismissed);
    }

    private void OnDisable()
    {
        EventBus<RunStarted>.Unsubscribe(HandleRunStarted);
        EventBus<LevelStarted>.Unsubscribe(HandleLevelStarted);
        EventBus<LevelTimeChanged>.Unsubscribe(HandleLevelTimeChanged);
        EventBus<LevelCompleted>.Unsubscribe(HandleLevelCompleted);
        EventBus<PlayerStatsChanged>.Unsubscribe(HandlePlayerStatsChanged);
        EventBus<PlayerGrappleUsesChanged>.Unsubscribe(HandleGrappleUsesChanged);
        EventBus<RunEnded>.Unsubscribe(HandleRunEnded);
        EventBus<RunAbandoned>.Unsubscribe(HandleRunAbandoned);
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

        ShowTime(0f, evt.EffectiveTimeLimit, false);
    }

    private void HandleLevelTimeChanged(LevelTimeChanged evt)
    {
        ShowTime(evt.ElapsedSeconds, evt.EffectiveTimeLimit, evt.RemainingSeconds <= warningSeconds);
    }

    private void ShowTime(float elapsed, float limit, bool warning)
    {
        if (timeText != null)
        {
            string elapsedText = Monospace(TimeFormat.Format(elapsed));
            timeText.text = limitText != null ? elapsedText : $"{elapsedText} / {TimeFormat.Format(limit)}";
            timeText.color = warning ? warningColor : normalColor;
        }

        if (limitText != null)
        {
            limitText.text = $"/ {TimeFormat.Format(limit)}";
        }

        if (timeBar != null)
        {
            timeBar.Fill = limit > 0f ? elapsed / limit : 0f;
            timeBar.color = warning ? warningColor : timeBarNormalColor;
        }
    }

    private string Monospace(string text)
    {
        return digitWidthEm > 0f
            ? $"<mspace={digitWidthEm.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}em>{text}</mspace>"
            : text;
    }

    private void HandlePlayerStatsChanged(PlayerStatsChanged evt)
    {
        if (!evt.Stats.IsValid)
        {
            return;
        }

        lastStats = evt.Stats;
        RefreshAbilities();
    }

    // Usos do gancho/teia na fase: muda a cada disparo, no início da fase e quando os stats mudam.
    private void HandleGrappleUsesChanged(PlayerGrappleUsesChanged evt)
    {
        grappleUsesLeft = evt.UsesLeft;
        grappleUsesMax = evt.MaxUses;
        RefreshAbilities();
    }

    private void RefreshAbilities()
    {
        if (abilitiesText == null || !lastStats.IsValid)
        {
            return;
        }

        PlayerStatsSnapshot stats = lastStats;
        var parts = new List<string>();

        int maxAirJumps = stats.GetInt(StatType.MaxAirJumps);
        if (maxAirJumps >= 1)
        {
            int totalJumps = maxAirJumps + 1;
            parts.Add(totalJumps > 2 ? $"Pulo ×{totalJumps}" : "Pulo duplo");
        }

        if (stats.HasAbility(AbilityFlags.WallJump))
        {
            parts.Add("Wall jump");
        }

        bool hook = stats.HasAbility(AbilityFlags.GrapplingHook);
        bool web = stats.HasAbility(AbilityFlags.WebSwing);
        if (hook || web)
        {
            string label = web ? (hook ? "Gancho + Teia" : "Teia") : "Gancho";
            parts.Add(grappleUsesMax > 0 ? $"{label} {grappleUsesLeft}/{grappleUsesMax}" : label);
        }

        int maxDashes = stats.GetInt(StatType.MaxDashes);
        if (maxDashes >= 1)
        {
            parts.Add(maxDashes > 1 ? $"Dash ×{maxDashes}" : "Dash");
        }

        abilitiesText.text = string.Join(" · ", parts);

        if (abilitiesRoot != null)
        {
            abilitiesRoot.SetActive(parts.Count > 0);
        }
    }

    // A tela de resultado cobre o jogo: o HUD sai e volta no LevelStarted da próxima fase.
    private void HandleLevelCompleted(LevelCompleted evt)
    {
        SetPanelActive(false);
    }

    private void HandleRunAbandoned(RunAbandoned evt)
    {
        SetPanelActive(false);
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
