using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Pele" de um botão do kit: em vez da troca de cor padrão da Unity, o item selecionado inverte as cores, cresce,
/// avança e inclina, com transição suave. Seleção por teclado/gamepad e por mouse são a mesma coisa: passar o mouse
/// por cima seleciona, então só existe um cursor na tela.
///
/// Todos os alvos são opcionais; o que estiver vazio é ignorado. O botão (Selectable) fica com Transition = None.
/// Só anima enquanto a seleção está mudando; parado, o custo é uma comparação por frame.
/// </summary>
[AddComponentMenu("UI/Kit/Slant Button")]
[DisallowMultipleComponent]
public sealed class UiSlantButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [Header("Alvos (todos opcionais)")]
    [Tooltip("Botão, toggle ou slider observado (vazio = o deste objeto).")]
    [SerializeField] private Selectable selectable;

    [Tooltip("Filho que cresce/avança/inclina. Nunca o objeto posicionado por LayoutGroup.")]
    [SerializeField] private RectTransform visual;

    [Tooltip("Placa de fundo: troca de cor entre normal e selecionado.")]
    [SerializeField] private Graphic plate;

    [Tooltip("Texto: troca de cor entre normal e selecionado.")]
    [SerializeField] private Graphic label;

    [Tooltip("Aparece só no item selecionado (moldura, seta, faixa).")]
    [SerializeField] private Graphic highlight;

    [Header("Cores")]
    [SerializeField] private Color plateNormal = new Color(0.04f, 0.04f, 0.07f, 1f);
    [SerializeField] private Color plateSelected = new Color(1f, 0.18f, 0.53f, 1f);
    [SerializeField] private Color labelNormal = new Color(0.96f, 0.96f, 0.94f, 1f);
    [SerializeField] private Color labelSelected = new Color(0.04f, 0.04f, 0.07f, 1f);

    [Header("Movimento ao selecionar")]
    [SerializeField] private float selectedScale = 1.08f;
    [SerializeField] private Vector2 selectedShift = new Vector2(22f, 0f);
    [SerializeField] private float selectedAngle = -2f;

    [Tooltip("Velocidade da transição (maior = mais seca).")]
    [Min(1f)] [SerializeField] private float sharpness = 16f;

    [Tooltip("Opacidade do conjunto quando o botão não está interagível.")]
    [Range(0f, 1f)] [SerializeField] private float disabledAlpha = 0.4f;

    private Vector2 restPosition;
    private Vector3 restScale;
    private float restAngle;
    private float highlightAlpha = 1f;
    private bool captured;
    private bool selected;
    private bool lastInteractable = true;
    private float blend;      // 0 = normal, 1 = selecionado
    private float applied = -1f;

    /// <summary>Liga os alvos por código (usado pelo construtor de telas e pelo MenuUi).</summary>
    public void Bind(Selectable target, RectTransform visualRoot, Graphic plateGraphic, Graphic labelGraphic, Graphic highlightGraphic)
    {
        selectable = target;
        visual = visualRoot;
        plate = plateGraphic;
        label = labelGraphic;
        highlight = highlightGraphic;
    }

    public void SetColors(Color plateOff, Color plateOn, Color labelOff, Color labelOn)
    {
        plateNormal = plateOff;
        plateSelected = plateOn;
        labelNormal = labelOff;
        labelSelected = labelOn;
    }

    public void SetMotion(float scale, Vector2 shift, float angle)
    {
        selectedScale = scale;
        selectedShift = shift;
        selectedAngle = angle;
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;

        if (selectable == null) selectable = GetComponent<Selectable>();
        if (visual != null)
        {
            restPosition = visual.anchoredPosition;
            restScale = visual.localScale;
            restAngle = visual.localEulerAngles.z;
        }

        if (highlight != null) highlightAlpha = highlight.color.a;
    }

    private void OnEnable()
    {
        Capture();

        // O painel pode ter sido reaberto com este botão já selecionado pelo EventSystem.
        selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        blend = selected ? 1f : 0f;
        applied = -1f;
        Apply();
    }

    private void OnDisable()
    {
        selected = false;
        blend = 0f;
        if (captured) Apply();
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable == null || !selectable.IsInteractable() || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    private void Update()
    {
        bool interactable = selectable == null || selectable.IsInteractable();
        float target = selected && interactable ? 1f : 0f;

        if (blend != target)
        {
            blend = Mathf.MoveTowards(blend, target, Mathf.Max(0.02f, Mathf.Abs(target - blend)) * sharpness * Time.unscaledDeltaTime);
        }

        if (blend != applied || interactable != lastInteractable)
        {
            lastInteractable = interactable;
            Apply();
        }
    }

    private void Apply()
    {
        applied = blend;
        float k = blend * blend * (3f - 2f * blend);
        float alpha = lastInteractable ? 1f : disabledAlpha;

        if (plate != null)
        {
            Color c = Color.LerpUnclamped(plateNormal, plateSelected, k);
            c.a *= alpha;
            plate.color = c;
        }

        if (label != null)
        {
            Color c = Color.LerpUnclamped(labelNormal, labelSelected, k);
            c.a *= alpha;
            label.color = c;
        }

        if (highlight != null)
        {
            Color c = highlight.color;
            c.a = highlightAlpha * k;
            highlight.color = c;
        }

        if (visual != null)
        {
            visual.anchoredPosition = restPosition + selectedShift * k;
            visual.localScale = restScale * Mathf.LerpUnclamped(1f, selectedScale, k);
            visual.localRotation = Quaternion.Euler(0f, 0f, restAngle + selectedAngle * k);
        }
    }
}
