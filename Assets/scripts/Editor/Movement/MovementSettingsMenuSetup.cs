using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Integração da M.17 na cena SettingsMenu: dois botões novos no painel de configurações ("ACESSIBILIDADE" e
/// "CONTROLES", clones do BACK) que abrem os painéis FeedbackOptionsPanel e RebindingPanel (sobrepostos, inativos até
/// o clique). O estilo dos painéis é copiado do BACK e do "Fundo" da própria cena. Idempotente (acha por nome e
/// atualiza). Menu: Tools/Movement/Setup/Settings Menu (M.17). Batch: MovementSettingsMenuSetup.SetupBatch.
/// </summary>
public static class MovementSettingsMenuSetup
{
    public const string ScenePath = "Assets/Scenes/SettingsMenu.unity";

    private const string FeedbackPanelName = "FeedbackOptionsPanel";
    private const string RebindPanelName = "RebindingPanel";
    private const string WindowName = "Janela";
    private const int UiLayer = 5;

    [MenuItem("Tools/Movement/Setup/Settings Menu (M.17)")]
    public static void SetupMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Setup();
    }

    public static void SetupBatch()
    {
        Setup();
        EditorApplication.Exit(0);
    }

    public static void Setup()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform canvas = FindRoot(scene, "Canvas");
        Button back = canvas != null ? FindChild<Button>(canvas, "BackBtn") : null;
        Image panelImage = canvas != null ? FindChild<Image>(canvas, "Fundo") : null;
        if (canvas == null || back == null || panelImage == null)
        {
            Debug.LogError("[Movement] - SettingsMenu sem Canvas/BackBtn/Fundo: nada alterado");
            return;
        }

        MenuStyle style = StyleFrom(canvas, back, panelImage);

        GameObject feedbackPanel = EnsurePanel(canvas, FeedbackPanelName, new Vector2(560f, 440f), style);
        GameObject rebindPanel = EnsurePanel(canvas, RebindPanelName, new Vector2(720f, 570f), style);
        Button accessibility = EnsureButton(canvas, back, "BtnAcessibilidade", "ACESSIBILIDADE", new Vector2(-58f, -8f), feedbackPanel);
        Button controls = EnsureButton(canvas, back, "BtnControles", "CONTROLES", new Vector2(82f, -8f), rebindPanel);

        Configure(GetOrAdd<FeedbackOptionsPanel>(feedbackPanel), feedbackPanel, style, accessibility);
        Configure(GetOrAdd<RebindingPanel>(rebindPanel), rebindPanel, style, controls);

        // Painéis por cima de tudo, fechados até o clique.
        feedbackPanel.transform.SetAsLastSibling();
        rebindPanel.transform.SetAsLastSibling();
        feedbackPanel.SetActive(false);
        rebindPanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Movement] - SettingsMenu: painéis de acessibilidade e controles configurados");
    }

    private static MenuStyle StyleFrom(Transform canvas, Button back, Image panelImage)
    {
        var style = new MenuStyle();
        var backLabel = back.GetComponentInChildren<TextMeshProUGUI>(true);
        if (backLabel != null)
        {
            style.Font = backLabel.font;
            style.ButtonTextSize = backLabel.fontSize * 0.85f;
            style.ButtonTextColor = backLabel.color;
        }

        var backImage = back.targetGraphic as Image;
        if (backImage != null) style.ButtonSprite = backImage.sprite;
        style.ButtonColors = back.colors;
        style.PanelSprite = panelImage.sprite;
        style.PanelColor = panelImage.color;
        Image outline = FindChild<Image>(canvas, "Contorno");
        if (outline != null) style.OutlineColor = outline.color;
        return style;
    }

    // Raiz em tela cheia (escurece e bloqueia cliques atrás) com a janela centralizada: contorno + fundo.
    private static GameObject EnsurePanel(Transform canvas, string name, Vector2 size, MenuStyle style)
    {
        Transform existing = canvas.Find(name);
        GameObject root = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        root.layer = UiLayer;
        var rt = (RectTransform)root.transform;
        rt.SetParent(canvas, false);
        MenuUi.Stretch(rt);
        GetOrAdd<Image>(root).color = style.DimColor;

        Transform w = rt.Find(WindowName);
        GameObject outlineGo = w != null ? w.gameObject : new GameObject(WindowName, typeof(RectTransform));
        outlineGo.layer = UiLayer;
        var outlineRt = (RectTransform)outlineGo.transform;
        outlineRt.SetParent(rt, false);
        outlineRt.anchorMin = outlineRt.anchorMax = new Vector2(0.5f, 0.5f);
        outlineRt.sizeDelta = size;
        outlineRt.anchoredPosition = Vector2.zero;
        Image outline = GetOrAdd<Image>(outlineGo);
        outline.sprite = style.PanelSprite;
        outline.type = style.PanelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        outline.color = style.OutlineColor;

        Transform f = outlineRt.Find("Fundo");
        GameObject fillGo = f != null ? f.gameObject : new GameObject("Fundo", typeof(RectTransform));
        fillGo.layer = UiLayer;
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.SetParent(outlineRt, false);
        MenuUi.Stretch(fillRt, 3f);
        Image fill = GetOrAdd<Image>(fillGo);
        fill.sprite = style.PanelSprite;
        fill.type = outline.type;
        fill.color = style.PanelColor;
        return root;
    }

    private static Button EnsureButton(Transform canvas, Button template, string name, string label, Vector2 position, GameObject opens)
    {
        Transform existing = canvas.Find(name);
        GameObject go = existing != null ? existing.gameObject : Object.Instantiate(template.gameObject, canvas);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(135f, 30f);

        var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        var templateText = template.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = templateText != null ? templateText.fontSize : 24f;
        }

        var button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddBoolPersistentListener(button.onClick, new UnityAction<bool>(opens.SetActive), true);
        EditorUtility.SetDirty(button);
        return button;
    }

    private static void Configure(MonoBehaviour panel, GameObject root, MenuStyle style, Selectable opener)
    {
        var so = new SerializedObject(panel);
        so.FindProperty("window").objectReferenceValue = root.transform.Find(WindowName + "/Fundo");
        so.FindProperty("opener").objectReferenceValue = opener;
        so.ApplyModifiedPropertiesWithoutUndo();

        // MenuStyle é uma classe serializável comum: copia por reflexão e marca o componente.
        FieldInfo field = panel.GetType().GetField("style", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(panel, style);
        EditorUtility.SetDirty(panel);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
        }

        return null;
    }

    private static T FindChild<T>(Transform parent, string name) where T : Component
    {
        foreach (T c in parent.GetComponentsInChildren<T>(true))
        {
            if (c.name == name) return c;
        }

        return null;
    }
}
