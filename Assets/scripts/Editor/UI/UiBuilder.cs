using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Reconstrói a UI do jogo a partir do tema (Assets/UI/UiTheme): HUD e telas da run (prefab RunSystems), menu
/// principal e configurações. As views (RunHudView, LevelResultView...) não mudam de lógica: o construtor só refaz a
/// hierarquia visual e religa os campos delas. Idempotente: rodar de novo depois de mexer no tema refaz tudo.
///
/// Menu: Tools → UI. Este arquivo tem o tema, as fontes e as peças comuns; as telas ficam nos outros UiBuilder.*.cs.
/// </summary>
public static partial class UiBuilder
{
    public const string ThemePath = "Assets/UI/UiTheme.asset";

    private const string UiDir = "Assets/UI";
    private const string FontDir = "Assets/UI/Fonts";
    private const string ExampleFontDir = "Assets/TextMesh Pro/Examples & Extras/Fonts";
    private const string LogoFontPath = "Assets/TextMesh Pro/Fonts/ModernCosmo-q25Dr SDF.asset";
    private const string ShadowMaterialPath = FontDir + "/Display Shadow.mat";
    private const int UiLayer = 5;

    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // Tema em uso durante uma reconstrução.
    private static UiTheme Theme;

    // ───────────────────────────── Menu ─────────────────────────────

    [MenuItem("Tools/UI/Reconstruir tudo")]
    private static void RebuildAllMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RebuildAll();
    }

    [MenuItem("Tools/UI/Reconstruir só a UI da run (HUD, resultado, upgrades, fim)")]
    private static void RebuildRunMenu()
    {
        Theme = EnsureTheme();
        RebuildRunUi();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/UI/Abrir tema")]
    private static void OpenTheme()
    {
        UiTheme theme = EnsureTheme();
        Selection.activeObject = theme;
        EditorGUIUtility.PingObject(theme);
    }

    public static void RebuildAll()
    {
        Theme = EnsureTheme();
        RebuildRunUi();
        RebuildMainMenu();
        RebuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[UI] - UI reconstruída a partir do tema (run, menu principal e configurações)");
    }

    // ───────────────────────────── Tema e fontes ─────────────────────────────

    public static UiTheme EnsureTheme()
    {
        Directory.CreateDirectory(FontDir);
        AssetDatabase.Refresh();

        var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UiTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        TMP_FontAsset fallback = TMP_Settings.defaultFontAsset;
        if (theme.displayFont == null) theme.displayFont = EnsureFont("Anton", fallback);
        if (theme.bodyFont == null) theme.bodyFont = EnsureFont("Oswald-Bold", fallback);
        if (theme.logoFont == null)
        {
            var logo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LogoFontPath);
            theme.logoFont = logo != null ? logo : theme.displayFont;
        }

        if (theme.displayShadowMaterial == null && theme.displayFont != null)
        {
            theme.displayShadowMaterial = EnsureShadowMaterial(theme);
        }

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssetIfDirty(theme);
        return theme;
    }

    // Copia o TTF (licença OFL) dos exemplos do TextMesh Pro para Assets/UI/Fonts e gera um font asset próprio, para a
    // UI não depender da pasta "Examples & Extras".
    private static TMP_FontAsset EnsureFont(string fontName, TMP_FontAsset fallback)
    {
        string assetPath = $"{FontDir}/{fontName} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        string ttfPath = $"{FontDir}/{fontName}.ttf";
        if (AssetDatabase.LoadAssetAtPath<Font>(ttfPath) == null)
        {
            string source = $"{ExampleFontDir}/{fontName}.ttf";
            if (AssetDatabase.LoadAssetAtPath<Font>(source) == null || !AssetDatabase.CopyAsset(source, ttfPath))
            {
                Debug.LogWarning($"[UI] - Fonte '{fontName}.ttf' não encontrada; usando a fonte padrão do TextMesh Pro");
                return fallback;
            }
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 96, 12, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        if (asset == null) return fallback;

        asset.name = fontName + " SDF";
        AssetDatabase.CreateAsset(asset, assetPath);
        asset.material.name = fontName + " SDF Material";
        asset.atlasTexture.name = fontName + " SDF Atlas";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);

        // Pré-carrega ASCII + Latin-1 (acentos do português) para o atlas não mudar a cada texto novo.
        var chars = new StringBuilder();
        for (int c = 32; c < 127; c++) chars.Append((char)c);
        for (int c = 160; c < 256; c++) chars.Append((char)c);
        chars.Append("–—…•");
        asset.TryAddCharacters(chars.ToString(), out _);

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    // Variante da fonte de títulos com sombra dura deslocada (Underlay do shader SDF).
    private static Material EnsureShadowMaterial(UiTheme theme)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(ShadowMaterialPath);
        if (existing != null) return existing;

        var material = new Material(theme.displayFont.material) { name = "Display Shadow" };
        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_UnderlayColor", theme.ink);
        material.SetFloat("_UnderlayOffsetX", 0.55f);
        material.SetFloat("_UnderlayOffsetY", -0.55f);
        material.SetFloat("_UnderlayDilate", 0f);
        material.SetFloat("_UnderlaySoftness", 0f);
        AssetDatabase.CreateAsset(material, ShadowMaterialPath);
        return material;
    }

    // ───────────────────────────── Peças comuns ─────────────────────────────

    private static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Ancora num ponto do pai (âncora e pivô em 0..1), com posição e tamanho em px de referência.</summary>
    private static T Place<T>(T component, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, float angle = 0f) where T : Component
    {
        var rt = (RectTransform)component.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        return component;
    }

    /// <summary>Estica para preencher o pai, com margens (esquerda, baixo, direita, cima).</summary>
    private static T Fill<T>(T component, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f) where T : Component
    {
        var rt = (RectTransform)component.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return component;
    }

    /// <summary>Bloco inclinado. <paramref name="height"/> define a inclinação (altura × slant do tema).</summary>
    private static UiSlant Slab(Transform parent, string name, Color color, float height, float slantScale = 1f)
    {
        var slab = Node(name, parent).gameObject.AddComponent<UiSlant>();
        slab.color = color;
        slab.Skew = height * Theme.slant * slantScale;
        slab.raycastTarget = false;
        return slab;
    }

    private static UiSlant Shadow(UiSlant slab, Color color, float x = 10f, float y = -10f)
    {
        slab.ShadowColor = color;
        slab.ShadowOffset = new Vector2(x, y);
        return slab;
    }

    private static Image Solid(Transform parent, string name, Color color, bool blocksClicks)
    {
        var image = Node(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = blocksClicks;
        return image;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left, bool wrap = false, bool upper = true)
    {
        var tmp = Node(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Encolhe o texto para caber na caixa (nomes de fase e de upgrade variam de tamanho).</summary>
    private static TextMeshProUGUI AutoSize(TextMeshProUGUI tmp, float minSize)
    {
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = tmp.fontSize;
        tmp.fontSizeMin = minSize;
        return tmp;
    }

    private static UiEnter Enter(Component target, Vector2 fromOffset, float delay, float duration = 0.28f, float scale = 1f, float angle = 0f)
    {
        var enter = target.gameObject.AddComponent<UiEnter>();
        enter.Setup(fromOffset, delay, duration, scale, angle);
        return enter;
    }

    private static UiDrift Drift(Component target, Vector2 move, float angle, float scale, float speed, float phase)
    {
        var drift = target.gameObject.AddComponent<UiDrift>();
        drift.Setup(move, angle, scale, speed, phase);
        return drift;
    }

    /// <summary>
    /// Botão do kit dentro de <paramref name="root"/> (que já pode ter um Button com onClick configurado): placa preta
    /// com sombra de destaque; selecionado, inverte para placa clara com texto escuro, avança e cresce.
    /// </summary>
    private static Button SkinButton(RectTransform root, string label, float fontSize, out TextMeshProUGUI text,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        // Reconstrução: tira a pele anterior antes de montar a nova.
        Remove<UiSlantButton>(root.gameObject);

        float height = root.sizeDelta.y;
        RectTransform visual = Fill(Node("Visual", root));

        UiSlant plate = Fill(Slab(visual, "Plate", Theme.ink, height));
        Shadow(plate, Theme.accent);
        plate.raycastTarget = true;

        // Cunha de destaque na ponta esquerda: o "cursor" do item selecionado.
        UiSlant cursor = Slab(visual, "Cursor", Theme.accent, height);
        Place(cursor, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(26f, height));

        text = Label(visual, "Label", label, Theme.displayFont, fontSize, Theme.paper, alignment);
        Fill(text, height * 0.45f, 0f, height * 0.35f, 0f);
        AutoSize(text, fontSize * 0.5f);

        var button = root.GetComponent<Button>();
        if (button == null) button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = plate;

        var skin = root.gameObject.AddComponent<UiSlantButton>();
        skin.Bind(button, visual, plate, text, cursor);
        skin.SetColors(Theme.ink, Theme.paper, Theme.paper, Theme.ink);
        skin.SetMotion(1.08f, new Vector2(26f, 0f), 0f);
        return button;
    }

    // ───────────────────────────── Utilitários ─────────────────────────────

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void Remove<TComponent>(GameObject go) where TComponent : Component
    {
        var component = go.GetComponent<TComponent>();
        if (component != null) UnityEngine.Object.DestroyImmediate(component);
    }

    private static void SetRef(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[UI] - Campo '{property}' não existe em {target.GetType().Name}");
            return;
        }

        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetValue(UnityEngine.Object target, string property, Action<SerializedProperty> write)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[UI] - Campo '{property}' não existe em {target.GetType().Name}");
            return;
        }

        write(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCanvas(GameObject canvasObject)
    {
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    /// <summary>Abre a cena (se ainda não estiver aberta), aplica a edição, salva e fecha de novo.</summary>
    private static void EditScene(string path, Action<Scene> edit)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

        try
        {
            edit(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }

        return null;
    }
}
