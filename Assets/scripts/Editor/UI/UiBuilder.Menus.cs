using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Menu principal e configurações (cenas MainMenu e SettingsMenu).</summary>
public static partial class UiBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string SettingsScenePath = "Assets/Scenes/SettingsMenu.unity";
    private const string LegacyLayerName = "LegacyLayer";
    private const string ScreenName = "Screen";

    // ───────────────────────────── Menu principal ─────────────────────────────

    public static void RebuildMainMenu()
    {
        if (Theme == null) Theme = EnsureTheme();

        EditScene(MainMenuScenePath, scene =>
        {
            GameObject canvas = FindRoot(scene, "Canvas");
            MainMenu menu = canvas != null ? canvas.GetComponentInChildren<MainMenu>(true) : null;
            if (menu == null)
            {
                Debug.LogError("[UI] - MainMenu sem Canvas ou sem o componente MainMenu; cena não alterada");
                return;
            }

            WrapLegacyLayer(canvas.transform, "Background", "RainFX");
            ConfigureCanvas(canvas);

            var root = (RectTransform)menu.transform;
            ClearChildren(root);
            Fill(root);
            root.localScale = Vector3.one;
            root.SetAsLastSibling();

            Wedge(root);

            // Nome do jogo em duas linhas e duas cores, fora do eixo.
            RectTransform title = Place(Node("Title", root), TopLeft, TopLeft, new Vector2(120f, -90f), Vector2.zero, Theme.tilt * 1.5f);
            Enter(title, new Vector2(-900f, 0f), 0.05f, 0.4f);
            TextMeshProUGUI line1 = Label(title, "TitleLine1", "Cyber", Theme.logoFont, 150f, Theme.paper);
            Place(line1, TopLeft, TopLeft, new Vector2(0f, 0f), new Vector2(1000f, 200f));
            TextMeshProUGUI line2 = Label(title, "TitleLine2", "Delivery", Theme.logoFont, 150f, Theme.paper);
            Place(line2, TopLeft, TopLeft, new Vector2(40f, -140f), new Vector2(1000f, 200f));

            Button play = MenuButton(root, "PlayButton", "Novo jogo", new Vector2(150f, -130f), 560f, 0.2f);
            Button settings = MenuButton(root, "Settings", "Configurações", new Vector2(200f, -250f), 640f, 0.27f);
            Button exit = MenuButton(root, "ExitButton", "Sair", new Vector2(250f, -370f), 400f, 0.34f);

            UnityEventTools.AddPersistentListener(play.onClick, menu.PlayGame);
            UnityEventTools.AddPersistentListener(settings.onClick, menu.OpenSettings);
            UnityEventTools.AddPersistentListener(exit.onClick, menu.QuitGame);

            SelectFirst(scene, play.gameObject);
        });
    }

    private static Button MenuButton(RectTransform parent, string name, string label, Vector2 position, float width, float delay)
    {
        RectTransform root = Place(Node(name, parent), MidLeft, MidLeft, position, new Vector2(width, 96f), Theme.tilt);
        Enter(root, new Vector2(-900f, 0f), delay);
        return SkinButton(root, label, 56f, out _, TextAlignmentOptions.Left);
    }

    // Cunha escura na esquerda (com um filete de destaque), para o texto ter contraste sobre a arte de fundo.
    private static void Wedge(RectTransform parent)
    {
        const float height = 1500f;
        float skew = height * Theme.slant * 0.9f;

        UiSlant accent = Slab(parent, "WedgeAccent", Theme.accent, height, 0.9f);
        Place(accent, MidLeft, MidLeft, new Vector2(-250f, 0f), new Vector2(1150f, height));
        accent.Skew = skew;
        Drift(accent, new Vector2(10f, 0f), 0f, 0f, 0.12f, 0f);

        UiSlant ink = Slab(parent, "WedgeInk", Theme.ink, height, 0.9f);
        Place(ink, MidLeft, MidLeft, new Vector2(-300f, 0f), new Vector2(1150f, height));
        ink.Skew = skew;
    }

    // ───────────────────────────── Configurações ─────────────────────────────

    public static void RebuildSettings()
    {
        if (Theme == null) Theme = EnsureTheme();

        EditScene(SettingsScenePath, scene =>
        {
            GameObject canvasObject = FindRoot(scene, "Canvas");
            if (canvasObject == null)
            {
                Debug.LogError("[UI] - SettingsMenu sem Canvas; cena não alterada");
                return;
            }

            Transform canvas = canvasObject.transform;
            Slider volume = FindNamed<Slider>(canvas, "MusicVolume");
            Toggle vsync = FindNamed<Toggle>(canvas, "VSync");
            Button back = FindNamed<Button>(canvas, "BackBtn");
            if (volume == null || vsync == null || back == null)
            {
                Debug.LogError("[UI] - SettingsMenu sem MusicVolume, VSync ou BackBtn; cena não alterada");
                return;
            }

            WrapLegacyLayer(canvas, "Background", "RainFX");
            ConfigureCanvas(canvasObject);

            // Os três controles da cena são mantidos (têm scripts e eventos ligados); o resto da tela antiga sai.
            volume.transform.SetParent(canvas, false);
            vsync.transform.SetParent(canvas, false);
            back.transform.SetParent(canvas, false);
            var keep = new HashSet<Transform> { volume.transform, vsync.transform, back.transform };
            for (int i = canvas.childCount - 1; i >= 0; i--)
            {
                Transform child = canvas.GetChild(i);
                if (child.name == LegacyLayerName || keep.Contains(child)) continue;
                Object.DestroyImmediate(child.gameObject);
            }

            RectTransform screen = Fill(Node(ScreenName, canvas));
            Wedge(screen);
            Header(screen, "Configurações");

            // Linhas em escada, na mesma inclinação dos títulos.
            RectTransform volumeRow = OptionRow(screen, "VolumeRow", "Volume", 0, out UiSlant volumePlate, out TextMeshProUGUI volumeLabel, out UiSlant volumeCursor);
            SkinSlider(volume, volumeRow, volumePlate, volumeLabel, volumeCursor);

            RectTransform vsyncRow = OptionRow(screen, "VSyncRow", "V-Sync", 1, out UiSlant vsyncPlate, out TextMeshProUGUI vsyncLabel, out UiSlant vsyncCursor);
            SkinToggle(vsync, vsyncRow, vsyncPlate, vsyncLabel, vsyncCursor);

            Button controls = RowButton(screen, "BtnControles", "Controles", 2);
            Button accessibility = RowButton(screen, "BtnAcessibilidade", "Acessibilidade", 3);

            ResetInteractive(back.gameObject);
            Place(back, MidLeft, MidLeft, RowPosition(4) + new Vector2(0f, -40f), new Vector2(420f, 92f), Theme.tilt);
            back.transform.SetParent(screen, false);
            Enter(back, new Vector2(-900f, 0f), 0.4f);
            SkinButton((RectTransform)back.transform, "Voltar", 52f, out _, TextAlignmentOptions.Left);
            if (back.onClick.GetPersistentEventCount() == 0)
            {
                var controller = Object.FindFirstObjectByType<SettingsBack>(FindObjectsInactive.Include);
                if (controller != null) UnityEventTools.AddPersistentListener(back.onClick, controller.Back);
            }

            GameObject rebindPanel = OptionsPanel<RebindingPanel>(canvas, "RebindingPanel", new Vector2(720f, 570f), 1.7f, controls);
            GameObject feedbackPanel = OptionsPanel<FeedbackOptionsPanel>(canvas, "FeedbackOptionsPanel", new Vector2(560f, 440f), 1.9f, accessibility);
            UnityEventTools.AddBoolPersistentListener(controls.onClick, new UnityAction<bool>(rebindPanel.SetActive), true);
            UnityEventTools.AddBoolPersistentListener(accessibility.onClick, new UnityAction<bool>(feedbackPanel.SetActive), true);

            SelectFirst(scene, volume.gameObject);
        });
    }

    private static Vector2 RowPosition(int index)
    {
        return new Vector2(150f + index * 44f, 150f - index * 118f);
    }

    // Faixa de título no canto superior esquerdo (mesma peça da tela de upgrades).
    private static void Header(RectTransform parent, string text)
    {
        RectTransform header = Place(Node("Header", parent), TopLeft, TopLeft, Vector2.zero, Vector2.zero, Theme.tilt);
        Enter(header, new Vector2(-1400f, 0f), 0f, 0.36f);
        UiSlant slab = Shadow(Slab(header, "TitleSlab", Theme.accent, 150f), Theme.paper, 14f, -14f);
        Place(slab, TopLeft, TopLeft, new Vector2(-70f, -96f), new Vector2(1160f, 150f));
        TextMeshProUGUI title = AutoSize(Label(slab.transform, "TitleText", text, Theme.displayFont, 104f, Theme.paper), 60f);
        if (Theme.displayShadowMaterial != null) title.fontSharedMaterial = Theme.displayShadowMaterial;
        Fill(title, 130f, 0f, 60f, 0f);
    }

    // Linha de opção: etiqueta com o nome à esquerda; o controle (slider, toggle) entra à direita dela.
    private static RectTransform OptionRow(RectTransform parent, string name, string label, int index,
        out UiSlant plate, out TextMeshProUGUI text, out UiSlant cursor)
    {
        const float height = 84f;
        RectTransform row = Place(Node(name, parent), MidLeft, MidLeft, RowPosition(index), new Vector2(1100f, height), Theme.tilt);
        Enter(row, new Vector2(-900f, 0f), 0.12f + index * 0.07f);

        plate = Shadow(Slab(row, "LabelPlate", Theme.ink, height), Theme.accent, 8f, -8f);
        Place(plate, MidLeft, MidLeft, Vector2.zero, new Vector2(400f, height));
        text = Label(plate.transform, "Label", label, Theme.displayFont, 46f, Theme.paper);
        Fill(text, 44f, 0f, 20f, 0f);

        cursor = Slab(row, "Cursor", Theme.accent, height);
        Place(cursor, MidLeft, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(26f, height));
        return row;
    }

    private static Button RowButton(RectTransform parent, string name, string label, int index)
    {
        RectTransform root = Place(Node(name, parent), MidLeft, MidLeft, RowPosition(index), new Vector2(520f, 84f), Theme.tilt);
        Enter(root, new Vector2(-900f, 0f), 0.12f + index * 0.07f);
        return SkinButton(root, label, 46f, out _, TextAlignmentOptions.Left);
    }

    // Tira o visual antigo de um controle que vai ser mantido (filhos, sprite e pele anterior).
    private static void ResetInteractive(GameObject go)
    {
        ClearChildren(go.transform);
        Remove<UiSlantButton>(go);
        Remove<UiEnter>(go);
        Remove<CanvasGroup>(go);
        Remove<Image>(go);
        go.transform.localScale = Vector3.one;
    }

    private static void SkinSlider(Slider slider, RectTransform row, UiSlant labelPlate, TextMeshProUGUI label, UiSlant cursor)
    {
        const float height = 40f;
        ResetInteractive(slider.gameObject);
        var root = (RectTransform)slider.transform;
        root.SetParent(row, false);
        Place(root, MidLeft, MidLeft, new Vector2(450f, 0f), new Vector2(420f, height));

        UiSlant track = Fill(Slab(root, "Track", Theme.ink, height));
        track.raycastTarget = true;

        RectTransform fillArea = Fill(Node("Fill Area", root));
        UiSlant fill = Fill(Slab(fillArea, "Fill", Theme.accentAlt, height));

        RectTransform handleArea = Fill(Node("Handle Slide Area", root));
        UiSlant handle = Slab(handleArea, "Handle", Theme.paper, height + 28f);
        var handleRt = (RectTransform)handle.transform;
        handleRt.anchorMin = new Vector2(0f, 0f);
        handleRt.anchorMax = new Vector2(0f, 1f);
        handleRt.sizeDelta = new Vector2(24f, 28f);
        handle.raycastTarget = true;

        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = handleRt;
        slider.targetGraphic = handle;
        slider.transition = Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;

        // Selecionado, quem acende é a etiqueta da linha.
        var skin = slider.gameObject.AddComponent<UiSlantButton>();
        skin.Bind(slider, null, labelPlate, label, cursor);
        skin.SetColors(Theme.ink, Theme.paper, Theme.paper, Theme.ink);
    }

    private static void SkinToggle(Toggle toggle, RectTransform row, UiSlant labelPlate, TextMeshProUGUI label, UiSlant cursor)
    {
        const float size = 72f;
        ResetInteractive(toggle.gameObject);
        var root = (RectTransform)toggle.transform;
        root.SetParent(row, false);
        Place(root, MidLeft, MidLeft, new Vector2(450f, 0f), new Vector2(96f, size));

        UiSlant box = Fill(Shadow(Slab(root, "Box", Theme.ink, size), Theme.accentAlt, 6f, -6f));
        box.raycastTarget = true;
        UiSlant check = Fill(Slab(root, "Check", Theme.accentAlt, size - 24f), 14f, 12f, 14f, 12f);

        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.transition = Selectable.Transition.None;

        var skin = toggle.gameObject.AddComponent<UiSlantButton>();
        skin.Bind(toggle, null, labelPlate, label, cursor);
        skin.SetColors(Theme.ink, Theme.paper, Theme.paper, Theme.ink);
    }

    // Painel de opções montado em runtime (controles, acessibilidade): véu em tela cheia + janela inclinada. As linhas
    // de dentro são criadas pelo próprio painel com o MenuStyle configurado aqui.
    private static GameObject OptionsPanel<TPanel>(Transform canvas, string name, Vector2 windowSize, float scale, Selectable opener)
        where TPanel : MonoBehaviour
    {
        RectTransform root = Fill(Node(name, canvas));
        Image veil = root.gameObject.AddComponent<Image>();
        veil.color = Theme.dim;
        veil.raycastTarget = true;

        RectTransform frame = Place(Node("Janela", root), Center, Center, Vector2.zero, windowSize);
        frame.localScale = new Vector3(scale, scale, 1f);
        Fill(Shadow(Slab(frame, "Placa", Theme.ink, windowSize.y, 0.1f), Theme.accent, 10f, -10f), -16f, -8f, -16f, -8f);
        RectTransform window = Fill(Node("Fundo", frame));

        var panel = root.gameObject.AddComponent<TPanel>();
        SetRef(panel, "window", window);
        SetRef(panel, "opener", opener);
        SetValue(panel, "style", style =>
        {
            style.FindPropertyRelative("Font").objectReferenceValue = Theme.bodyFont;
            style.FindPropertyRelative("TitleFont").objectReferenceValue = Theme.displayFont;
            style.FindPropertyRelative("TitleSize").floatValue = 36f;
            style.FindPropertyRelative("LabelSize").floatValue = 22f;
            style.FindPropertyRelative("ButtonTextSize").floatValue = 20f;
            style.FindPropertyRelative("TextColor").colorValue = Theme.paper;
            style.FindPropertyRelative("DimColor").colorValue = Theme.dim;
            style.FindPropertyRelative("Kinetic").boolValue = true;
            style.FindPropertyRelative("InkColor").colorValue = new Color(0.16f, 0.16f, 0.25f, 1f);
            style.FindPropertyRelative("PaperColor").colorValue = Theme.paper;
            style.FindPropertyRelative("AccentColor").colorValue = Theme.accent;
            style.FindPropertyRelative("Slant").floatValue = Theme.slant;
        });

        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    // ───────────────────────────── Utilitários das cenas ─────────────────────────────

    // As cenas de menu foram montadas num canvas de 800 px de largura. Para trocar a referência para 1920×1080 sem
    // refazer a arte de fundo, os elementos antigos vão para uma camada que reproduz o canvas antigo em escala.
    private static void WrapLegacyLayer(Transform canvas, params string[] childNames)
    {
        if (canvas.Find(LegacyLayerName) != null) return;

        var scaler = canvas.GetComponent<CanvasScaler>();
        float oldWidth = scaler != null && scaler.referenceResolution.x > 0f ? scaler.referenceResolution.x : ReferenceResolution.x;
        float scale = ReferenceResolution.x / oldWidth;

        RectTransform layer = Place(Node(LegacyLayerName, canvas), Center, Center, Vector2.zero, ReferenceResolution / scale);
        layer.localScale = new Vector3(scale, scale, 1f);
        layer.SetAsFirstSibling();

        foreach (string childName in childNames)
        {
            Transform child = canvas.Find(childName);
            if (child != null) child.SetParent(layer, false);
        }
    }

    private static TComponent FindNamed<TComponent>(Transform parent, string name) where TComponent : Component
    {
        foreach (TComponent component in parent.GetComponentsInChildren<TComponent>(true))
        {
            if (component.name == name) return component;
        }

        return null;
    }

    private static void SelectFirst(Scene scene, GameObject target)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var eventSystem = root.GetComponentInChildren<EventSystem>(true);
            if (eventSystem == null) continue;
            eventSystem.firstSelectedGameObject = target;
            EditorUtility.SetDirty(eventSystem);
            return;
        }
    }
}
