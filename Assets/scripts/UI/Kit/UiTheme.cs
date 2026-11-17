using TMPro;
using UnityEngine;

/// <summary>
/// Tema da UI: fontes, cores e inclinação usados pelo construtor de telas (Tools → UI → Reconstruir). Para mudar a
/// cara do jogo todo, edite o asset Assets/UI/UiTheme e reconstrua; nenhuma tela guarda cor ou fonte "na mão".
///
/// Linguagem visual: tipografia pesada em caixa alta, blocos pretos e brancos cortados em diagonal, uma cor de
/// destaque forte e tudo levemente fora do eixo. Contraste alto primeiro; a cor entra para apontar o que importa.
/// </summary>
[CreateAssetMenu(menuName = "Roguelike/UI Theme", fileName = "UiTheme")]
public sealed class UiTheme : ScriptableObject
{
    [Header("Fontes")]
    [Tooltip("Títulos, números e botões: pesada e condensada.")]
    public TMP_FontAsset displayFont;

    [Tooltip("Textos corridos e rótulos pequenos.")]
    public TMP_FontAsset bodyFont;

    [Tooltip("Nome do jogo na tela de título.")]
    public TMP_FontAsset logoFont;

    [Tooltip("Variante da fonte de títulos com sombra dura deslocada (gerada pelo construtor).")]
    public Material displayShadowMaterial;

    [Header("Cores")]
    [Tooltip("Preto da UI: blocos e texto sobre claro.")]
    public Color ink = new Color32(0x0B, 0x0B, 0x12, 0xFF);

    [Tooltip("Branco da UI: blocos e texto sobre escuro.")]
    public Color paper = new Color32(0xF5, 0xF5, 0xF0, 0xFF);

    [Tooltip("Destaque principal: seleção, cursor, sombras de bloco.")]
    public Color accent = new Color32(0xFF, 0x2E, 0x88, 0xFF);

    [Tooltip("Destaque secundário: informação e progresso.")]
    public Color accentAlt = new Color32(0x19, 0xE3, 0xFF, 0xFF);

    [Tooltip("Avisos e metas.")]
    public Color highlight = new Color32(0xFF, 0xE6, 0x00, 0xFF);

    [Tooltip("Véu escuro atrás das telas que cobrem o jogo.")]
    public Color dim = new Color32(0x06, 0x06, 0x0C, 0xF4);

    [Header("Forma")]
    [Tooltip("Inclinação dos blocos: avanço horizontal do topo como fração da altura (0,25 ≈ 14°).")]
    [Range(0f, 0.6f)] public float slant = 0.25f;

    [Tooltip("Rotação padrão dos títulos (graus; positivo sobe para a direita).")]
    [Range(-15f, 15f)] public float tilt = 4f;
}
