using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Opções de feedback e acessibilidade (SPEC §10.4, RF-65/RF-66) na cena SettingsMenu: tremor de tela e vibração
/// (0 / 50 / 100 %), congelar no impacto e reduzir flashes. Cada clique alterna o valor e grava na hora
/// (FeedbackSettings → PlayerPrefs). Monta as linhas em runtime com o estilo da tela (MenuStyle). Abre por um botão da
/// tela (SetActive) e fecha em "Voltar", devolvendo a seleção ao botão que abriu.
/// </summary>
public sealed class FeedbackOptionsPanel : MonoBehaviour
{
    [Tooltip("Janela onde as linhas são montadas (vazio = este objeto).")]
    [SerializeField] private RectTransform window;
    [SerializeField] private MenuStyle style = new MenuStyle();
    [Tooltip("Botão da tela que abre o painel (recebe a seleção ao fechar).")]
    [SerializeField] private Selectable opener;

    private TextMeshProUGUI shakeValue;
    private TextMeshProUGUI rumbleValue;
    private TextMeshProUGUI freezeValue;
    private TextMeshProUGUI flashesValue;
    private Button firstButton;
    private bool built;

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        Build();
        FeedbackSettings.Changed += Refresh;
        Refresh();
        if (EventSystem.current != null && firstButton != null) EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
    }

    private void OnDisable()
    {
        FeedbackSettings.Changed -= Refresh;
    }

    /// <summary>Fecha o painel e devolve a seleção ao botão que o abriu.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
        if (EventSystem.current != null && opener != null) EventSystem.current.SetSelectedGameObject(opener.gameObject);
    }

    private void Build()
    {
        if (built) return;
        built = true;
        RectTransform root = window != null ? window : (RectTransform)transform;
        RectTransform column = MenuUi.Column(root, 28f, 14f);

        TextMeshProUGUI title = MenuUi.Text(column, "ACESSIBILIDADE", style, style.TitleSize, style.TextColor);
        MenuUi.Weight(title, 1f).preferredHeight = 56f;

        firstButton = OptionRow(column, "Tremor de tela", () => FeedbackSettings.ShakeScale = NextStep(FeedbackSettings.ShakeScale), out shakeValue);
        OptionRow(column, "Vibração do controle", () => FeedbackSettings.RumbleScale = NextStep(FeedbackSettings.RumbleScale), out rumbleValue);
        OptionRow(column, "Congelar no impacto", () => FeedbackSettings.FreezeEnabled = !FeedbackSettings.FreezeEnabled, out freezeValue);
        OptionRow(column, "Reduzir flashes", () => FeedbackSettings.ReduceFlashes = !FeedbackSettings.ReduceFlashes, out flashesValue);

        RectTransform footer = MenuUi.Row(column, 44f, 16f);
        MenuUi.Weight(MenuUi.Button(footer, "RESTAURAR PADRÃO", style, FeedbackSettings.ResetToDefaults, out _), 1f);
        MenuUi.Weight(MenuUi.Button(footer, "VOLTAR", style, Close, out _), 1f);
    }

    private Button OptionRow(Transform parent, string label, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI value)
    {
        RectTransform row = MenuUi.Row(parent, 44f);
        MenuUi.Weight(MenuUi.Text(row, label, style, style.LabelSize, style.TextColor, TextAlignmentOptions.MidlineLeft), 1.4f);
        Button button = MenuUi.Button(row, "—", style, onClick, out value);
        MenuUi.Weight(button, 1f);
        return button;
    }

    private void Refresh()
    {
        if (!built) return;
        shakeValue.text = Percent(FeedbackSettings.ShakeScale);
        rumbleValue.text = Percent(FeedbackSettings.RumbleScale);
        freezeValue.text = FeedbackSettings.FreezeEnabled ? "LIGADO" : "DESLIGADO";
        flashesValue.text = FeedbackSettings.ReduceFlashes ? "SIM" : "NÃO";
    }

    private static float NextStep(float current)
    {
        float[] steps = FeedbackSettings.ScaleSteps;
        for (int i = 0; i < steps.Length; i++)
        {
            if (Mathf.Approximately(steps[i], current)) return steps[(i + 1) % steps.Length];
        }

        return steps[0];
    }

    private static string Percent(float scale) => Mathf.RoundToInt(scale * 100f) + " %";
}
