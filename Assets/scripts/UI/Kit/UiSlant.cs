using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bloco inclinado da UI: um paralelogramo (ou trapézio) desenhado em código, sem sprite, nítido em qualquer
/// resolução. É a peça base do visual: faixas, placas de botão, etiquetas e barras de progresso.
/// Opcionalmente desenha uma "sombra" deslocada (segundo bloco de outra cor) na mesma malha, sem draw call extra.
/// </summary>
[AddComponentMenu("UI/Kit/Slant")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UiSlant : MaskableGraphic
{
    [Tooltip("Quanto o topo da borda esquerda avança em relação à base (px). Positivo inclina para a direita.")]
    [SerializeField] private float skewLeft = 24f;

    [Tooltip("Idem para a borda direita. Igual à esquerda = paralelogramo; diferente = trapézio ou cunha.")]
    [SerializeField] private float skewRight = 24f;

    [Tooltip("Fração preenchida a partir da esquerda (barras de progresso).")]
    [Range(0f, 1f)] [SerializeField] private float fill = 1f;

    [Tooltip("Deslocamento do bloco de sombra (px). Zero = sem sombra.")]
    [SerializeField] private Vector2 shadowOffset;

    [SerializeField] private Color shadowColor = Color.black;

    public float SkewLeft
    {
        get => skewLeft;
        set { if (!Mathf.Approximately(skewLeft, value)) { skewLeft = value; SetVerticesDirty(); } }
    }

    public float SkewRight
    {
        get => skewRight;
        set { if (!Mathf.Approximately(skewRight, value)) { skewRight = value; SetVerticesDirty(); } }
    }

    /// <summary>Define as duas bordas de uma vez (paralelogramo).</summary>
    public float Skew
    {
        set { SkewLeft = value; SkewRight = value; }
    }

    public float Fill
    {
        get => fill;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (!Mathf.Approximately(fill, clamped)) { fill = clamped; SetVerticesDirty(); }
        }
    }

    public Vector2 ShadowOffset
    {
        get => shadowOffset;
        set { if (shadowOffset != value) { shadowOffset = value; SetVerticesDirty(); } }
    }

    public Color ShadowColor
    {
        get => shadowColor;
        set { if (shadowColor != value) { shadowColor = value; SetVerticesDirty(); } }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f || fill <= 0f) return;

        // A inclinação é centrada no retângulo: a base recua metade e o topo avança metade.
        float halfL = skewLeft * 0.5f;
        float halfR = skewRight * 0.5f;
        Vector2 bl = new Vector2(r.xMin - halfL, r.yMin);
        Vector2 tl = new Vector2(r.xMin + halfL, r.yMax);
        Vector2 tr = new Vector2(r.xMax + halfR, r.yMax);
        Vector2 br = new Vector2(r.xMax - halfR, r.yMin);

        if (fill < 1f)
        {
            tr = Vector2.LerpUnclamped(tl, tr, fill);
            br = Vector2.LerpUnclamped(bl, br, fill);
        }

        if (shadowOffset != Vector2.zero && shadowColor.a > 0f)
        {
            Color32 sc = shadowColor;
            sc.a = (byte)(sc.a * color.a);
            AddQuad(vh, bl + shadowOffset, tl + shadowOffset, tr + shadowOffset, br + shadowOffset, sc);
        }

        AddQuad(vh, bl, tl, tr, br, color);
    }

    private static void AddQuad(VertexHelper vh, Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br, Color32 c)
    {
        int start = vh.currentVertCount;
        vh.AddVert(bl, c, Vector2.zero);
        vh.AddVert(tl, c, Vector2.zero);
        vh.AddVert(tr, c, Vector2.zero);
        vh.AddVert(br, c, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
