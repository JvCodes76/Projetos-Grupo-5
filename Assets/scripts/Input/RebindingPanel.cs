using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tela de rebinding (SPEC §4.4, RF-64) na cena SettingsMenu: linhas Esquerda/Direita/Cima/Baixo (partes dos
/// composites do teclado), Pulo (primária e alternativa), Dash, Gancho e Voltar ao spawn; colunas Teclado e Gamepad
/// (pelos groups dos bindings). Clicar numa célula captura o próximo controle daquele dispositivo (Esc cancela) e grava
/// na hora. No gamepad, as direções são o analógico e o D-pad (fixos). Monta as linhas em runtime com o estilo da tela.
/// </summary>
public sealed class RebindingPanel : MonoBehaviour
{
    [Tooltip("Janela onde as linhas são montadas (vazio = este objeto).")]
    [SerializeField] private RectTransform window;
    [SerializeField] private MenuStyle style = new MenuStyle();
    [Tooltip("Botão da tela que abre o painel (recebe a seleção ao fechar).")]
    [SerializeField] private Selectable opener;

    private struct RowSpec
    {
        public string Label;
        public Func<InputAction> Action;
        public string Part;
        public int Occurrence;
    }

    private sealed class Cell
    {
        public Button Button;
        public TextMeshProUGUI Text;
        public InputAction Action;
        public int BindingIndex;
        public string Fallback;
    }

    private static readonly RowSpec[] Rows =
    {
        new RowSpec { Label = "Esquerda", Action = () => GameInput.Player.Movement, Part = "negative" },
        new RowSpec { Label = "Direita", Action = () => GameInput.Player.Movement, Part = "positive" },
        new RowSpec { Label = "Cima", Action = () => GameInput.Player.Vertical, Part = "positive" },
        new RowSpec { Label = "Baixo", Action = () => GameInput.Player.Vertical, Part = "negative" },
        new RowSpec { Label = "Pulo", Action = () => GameInput.Player.Jump },
        new RowSpec { Label = "Pulo (alternativo)", Action = () => GameInput.Player.Jump, Occurrence = 1 },
        new RowSpec { Label = "Dash", Action = () => GameInput.Player.Dash },
        new RowSpec { Label = "Gancho", Action = () => GameInput.Player.Grapple },
        new RowSpec { Label = "Voltar ao spawn", Action = () => GameInput.Player.Restart },
    };

    private readonly List<Cell> cells = new List<Cell>();
    private TextMeshProUGUI hint;
    private Button firstButton;
    private bool built;

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        Build();
        Refresh();
        if (EventSystem.current != null && firstButton != null) EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
    }

    private void OnDisable()
    {
        RebindService.Cancel();
    }

    /// <summary>Fecha o painel e devolve a seleção ao botão que o abriu.</summary>
    public void Close()
    {
        if (RebindService.IsRebinding) return;
        gameObject.SetActive(false);
        if (EventSystem.current != null && opener != null) EventSystem.current.SetSelectedGameObject(opener.gameObject);
    }

    private void Build()
    {
        if (built) return;
        built = true;
        RectTransform root = window != null ? window : (RectTransform)transform;
        RectTransform column = MenuUi.Column(root, 24f, 8f);

        MenuUi.Weight(MenuUi.Text(column, "CONTROLES", style, style.TitleSize, style.TextColor), 1f).preferredHeight = 48f;

        RectTransform header = MenuUi.Row(column, 30f);
        MenuUi.Weight(MenuUi.Text(header, "", style, style.LabelSize, style.TextColor), 1.2f);
        MenuUi.Weight(MenuUi.Text(header, "TECLADO", style, style.LabelSize, style.TextColor), 1f);
        MenuUi.Weight(MenuUi.Text(header, "GAMEPAD", style, style.LabelSize, style.TextColor), 1f);

        foreach (RowSpec spec in Rows)
        {
            RectTransform row = MenuUi.Row(column, 34f);
            MenuUi.Weight(MenuUi.Text(row, spec.Label, style, style.LabelSize, style.TextColor, TextAlignmentOptions.MidlineLeft), 1.2f);
            AddCell(row, spec, RebindService.KeyboardGroup);
            AddCell(row, spec, RebindService.GamepadGroup);
        }

        hint = MenuUi.Text(column, "", style, style.LabelSize * 0.8f, style.TextColor);
        MenuUi.Weight(hint, 1f).preferredHeight = 28f;

        RectTransform footer = MenuUi.Row(column, 40f, 16f);
        MenuUi.Weight(MenuUi.Button(footer, "RESTAURAR PADRÃO", style, ResetAll, out _), 1f);
        MenuUi.Weight(MenuUi.Button(footer, "VOLTAR", style, Close, out _), 1f);
    }

    private void AddCell(Transform row, RowSpec spec, string group)
    {
        var cell = new Cell { Action = spec.Action() };
        cell.BindingIndex = RebindService.FindBindingIndex(cell.Action, group, spec.Part, spec.Occurrence);
        // Direções no gamepad são eixos (analógico e D-pad), não partes de composite: não são remapeáveis aqui.
        cell.Fallback = spec.Part != null && group == RebindService.GamepadGroup ? "ANALÓGICO / D-PAD" : "—";
        cell.Button = MenuUi.Button(row, "", style, () => StartRebind(cell), out cell.Text);
        cell.Button.interactable = cell.BindingIndex >= 0;
        MenuUi.Weight(cell.Button, 1f);
        cells.Add(cell);
        if (firstButton == null && cell.Button.interactable) firstButton = cell.Button;
    }

    private void StartRebind(Cell cell)
    {
        if (RebindService.IsRebinding || cell.BindingIndex < 0) return;
        cell.Text.text = "...";
        hint.text = "Aperte o novo botão (Esc cancela)";
        RebindService.StartRebind(cell.Action, cell.BindingIndex, _ =>
        {
            Refresh();
            if (EventSystem.current != null && cell.Button != null) EventSystem.current.SetSelectedGameObject(cell.Button.gameObject);
        });
    }

    private void ResetAll()
    {
        RebindService.ResetAll();
        Refresh();
    }

    private void Refresh()
    {
        if (!built) return;
        foreach (Cell cell in cells)
        {
            cell.Text.text = cell.BindingIndex >= 0 ? RebindService.DisplayString(cell.Action, cell.BindingIndex).ToUpperInvariant() : cell.Fallback;
        }

        hint.text = "Clique numa célula para trocar o botão";
    }
}
