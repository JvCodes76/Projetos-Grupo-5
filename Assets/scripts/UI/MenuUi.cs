using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Estilo dos painéis montados em runtime na SettingsMenu (M.17). O MovementSettingsMenuSetup copia os valores do botão
/// BACK e do painel "Fundo" da cena, para as telas novas herdarem a aparência das que já existem.
/// </summary>
[Serializable]
public sealed class MenuStyle
{
    public TMP_FontAsset Font;
    public float TitleSize = 32f;
    public float LabelSize = 22f;
    public float ButtonTextSize = 20f;
    public Color TextColor = Color.white;
    public Color ButtonTextColor = new Color(0.196f, 0.196f, 0.196f, 1f);
    public Sprite ButtonSprite;
    public ColorBlock ButtonColors = ColorBlock.defaultColorBlock;
    public Sprite PanelSprite;
    public Color PanelColor = new Color(0f, 0f, 0f, 0.95f);
    public Color OutlineColor = new Color(0f, 1f, 0.62f, 1f);
    public Color DimColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Kit inclinado (UiTheme)")]
    [Tooltip("Ligado, os botões são placas inclinadas do kit (UiSlant + UiSlantButton) em vez de sprites.")]
    public bool Kinetic;
    public TMP_FontAsset TitleFont;
    public Color InkColor = new Color(0.043f, 0.043f, 0.071f, 1f);
    public Color PaperColor = new Color(0.96f, 0.96f, 0.94f, 1f);
    public Color AccentColor = new Color(1f, 0.18f, 0.53f, 1f);

    [Tooltip("Inclinação das placas: avanço do topo como fração da altura.")]
    public float Slant = 0.25f;
}

/// <summary>Montagem de linhas de menu (uGUI + TextMeshPro) em runtime, com navegação por teclado e gamepad.</summary>
public static class MenuUi
{
    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : 5;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, MenuStyle style, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rt = Rect("Texto", parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (style.Font != null) tmp.font = style.Font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = size * 0.5f;
        tmp.fontSizeMax = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Título de painel: usa a fonte de títulos do estilo, quando houver.</summary>
    public static TextMeshProUGUI Title(Transform parent, string text, MenuStyle style)
    {
        TextMeshProUGUI tmp = Text(parent, text, style, style.TitleSize, style.TextColor);
        if (style.TitleFont != null) tmp.font = style.TitleFont;
        return tmp;
    }

    public static Button Button(Transform parent, string label, MenuStyle style, UnityAction onClick, out TextMeshProUGUI text)
    {
        if (style.Kinetic) return SlantButton(parent, label, style, onClick, out text);

        RectTransform rt = Rect("Botão " + label, parent);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = style.ButtonSprite;
        image.type = style.ButtonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = style.ButtonColors;
        if (onClick != null) button.onClick.AddListener(onClick);

        text = Text(rt, label, style, style.ButtonTextSize, style.ButtonTextColor);
        Stretch((RectTransform)text.transform, 6f);
        return button;
    }

    // Botão do kit: a raiz (posicionada pelo layout) segura o Button; o filho "Visual" é o que se mexe ao selecionar.
    private static Button SlantButton(Transform parent, string label, MenuStyle style, UnityAction onClick, out TextMeshProUGUI text)
    {
        RectTransform rt = Rect("Botão " + label, parent);
        RectTransform visual = Rect("Visual", rt);
        Stretch(visual);

        RectTransform plateRt = Rect("Placa", visual);
        Stretch(plateRt);
        var plate = plateRt.gameObject.AddComponent<UiSlant>();
        plate.color = style.InkColor;

        var button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = plate;
        if (onClick != null) button.onClick.AddListener(onClick);

        text = Text(visual, label, style, style.ButtonTextSize, style.PaperColor);
        if (style.TitleFont != null) text.font = style.TitleFont;
        Stretch((RectTransform)text.transform, 6f);

        var skin = rt.gameObject.AddComponent<UiSlantButton>();
        skin.Bind(button, visual, plate, text, null);
        skin.SetColors(style.InkColor, style.AccentColor, style.PaperColor, style.InkColor);
        skin.SetMotion(1.04f, Vector2.zero, 0f);

        // A inclinação depende da altura final da linha, que só existe depois do layout: usa a altura mínima da linha.
        var parentElement = parent.GetComponent<LayoutElement>();
        float height = parentElement != null && parentElement.minHeight > 0f ? parentElement.minHeight : 40f;
        plate.Skew = height * style.Slant;
        return button;
    }

    /// <summary>Linha horizontal de altura fixa com as células dividindo a largura pelos pesos.</summary>
    public static RectTransform Row(Transform parent, float height, float spacing = 12f)
    {
        RectTransform rt = Rect("Linha", parent);
        var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        var element = rt.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        return rt;
    }

    public static LayoutElement Weight(Component cell, float flexibleWidth)
    {
        LayoutElement element = cell.GetComponent<LayoutElement>();
        if (element == null) element = cell.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = flexibleWidth;
        element.minWidth = 0f;
        return element;
    }

    /// <summary>Coluna vertical que preenche o pai (com margem) para empilhar as linhas.</summary>
    public static RectTransform Column(Transform parent, float padding, float spacing)
    {
        RectTransform rt = Rect("Conteúdo", parent);
        Stretch(rt, padding);
        var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        return rt;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
