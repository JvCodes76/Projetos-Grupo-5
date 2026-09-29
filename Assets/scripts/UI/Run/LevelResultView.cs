using Roguelike.Events;
using Roguelike.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tela de resultado da fase (tempo, alvo, limite e nota). Só ouve e emite eventos: nenhuma referência
/// ao RunManager. GameObject sempre ativo; o <see cref="panel"/> filho é que liga/desliga.
/// </summary>
public class LevelResultView : MonoBehaviour
{
    [Header("Painel")]
    [Tooltip("Obrigatório. GameObject filho ligado/desligado por esta view; começa escondido.")]
    [SerializeField] private GameObject panel;

    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI targetText;
    [SerializeField] private TextMeshProUGUI limitText;

    [Tooltip("Letra grande da nota (C/B/A/S).")]
    [SerializeField] private TextMeshProUGUI gradeText;

    [Tooltip("Cores na ordem C, B, A, S (mesma ordem de PerformanceGrade).")]
    [SerializeField] private Color[] gradeColors = new Color[4];

    [Header("Botão")]
    [Tooltip("Obrigatório. Avança do resultado da fase (LevelResultDismissed).")]
    [SerializeField] private Button continueButton;

    [Tooltip("Opcional. Texto do botão: \"Continuar\" ou \"Ver resultado\" na última fase.")]
    [SerializeField] private TextMeshProUGUI continueLabel;

    private int levelCount;

    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(HandleContinueClicked);
        }
    }

    private void OnEnable()
    {
        EventBus<RunStarted>.Subscribe(HandleRunStarted);
        EventBus<LevelCompleted>.Subscribe(HandleLevelCompleted);
        EventBus<LevelStarted>.Subscribe(HandleLevelStarted);
        EventBus<RunEnded>.Subscribe(HandleRunEnded);
    }

    private void OnDisable()
    {
        EventBus<RunStarted>.Unsubscribe(HandleRunStarted);
        EventBus<LevelCompleted>.Unsubscribe(HandleLevelCompleted);
        EventBus<LevelStarted>.Unsubscribe(HandleLevelStarted);
        EventBus<RunEnded>.Unsubscribe(HandleRunEnded);
    }

    private void HandleRunStarted(RunStarted evt)
    {
        levelCount = evt.Config != null ? evt.Config.Levels.Count : 0;
        SetPanelActive(false);
    }

    private void HandleLevelCompleted(LevelCompleted evt)
    {
        LevelResult result = evt.Result;

        if (titleText != null && result.Level != null)
        {
            titleText.text = result.Level.DisplayName;
        }

        if (timeText != null)
        {
            timeText.text = TimeFormat.Format(result.ElapsedSeconds);
        }

        if (targetText != null)
        {
            targetText.text = $"Alvo {TimeFormat.Format(result.TargetTime)}";
        }

        if (limitText != null)
        {
            limitText.text = $"Limite {TimeFormat.Format(result.EffectiveTimeLimit)}";
        }

        if (gradeText != null)
        {
            gradeText.text = result.Grade.ToString();
            int gradeIndex = (int)result.Grade;
            if (gradeColors != null && gradeIndex >= 0 && gradeIndex < gradeColors.Length)
            {
                gradeText.color = gradeColors[gradeIndex];
            }
        }

        if (continueLabel != null)
        {
            bool isLastLevel = levelCount > 0 && result.LevelIndex >= levelCount - 1;
            continueLabel.text = isLastLevel ? "Ver resultado" : "Continuar";
        }

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }

        SetPanelActive(true);

        // Sem ?. : EventSystem é UnityEngine.Object e o operador ignoraria a checagem de objeto destruído.
        if (continueButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }
    }

    private void HandleLevelStarted(LevelStarted evt)
    {
        SetPanelActive(false);
    }

    private void HandleRunEnded(RunEnded evt)
    {
        SetPanelActive(false);
    }

    private void HandleContinueClicked()
    {
        if (continueButton != null)
        {
            continueButton.interactable = false;
        }

        SetPanelActive(false);

        Debug.Log("[LevelResultView] - Continuar clicado, emitindo LevelResultDismissed");
        EventBus<LevelResultDismissed>.Raise(new LevelResultDismissed());
    }

    private void SetPanelActive(bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
