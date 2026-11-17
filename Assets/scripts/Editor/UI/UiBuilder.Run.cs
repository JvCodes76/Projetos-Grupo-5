using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Telas da run: HUD, resultado da fase, escolha de upgrade e fim de run (prefab RunSystems → RunUI).</summary>
public static partial class UiBuilder
{
    private const string RunSystemsPath = "Assets/_Roguelike/Prefabs/RunSystems.prefab";
    private const string CardPath = "Assets/_Roguelike/Prefabs/UpgradeCard.prefab";

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 MidLeft = new Vector2(0f, 0.5f);
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

    public static void RebuildRunUi()
    {
        if (Theme == null) Theme = EnsureTheme();

        RebuildCardPrefab();

        GameObject root = PrefabUtility.LoadPrefabContents(RunSystemsPath);
        try
        {
            Transform canvas = root.transform.Find("RunUI");
            var hud = root.GetComponentInChildren<RunHudView>(true);
            var result = root.GetComponentInChildren<LevelResultView>(true);
            var selection = root.GetComponentInChildren<UpgradeSelectionView>(true);
            var end = root.GetComponentInChildren<RunEndView>(true);
            if (canvas == null || hud == null || result == null || selection == null || end == null)
            {
                Debug.LogError("[UI] - RunSystems sem RunUI ou sem alguma das views; nada alterado");
                return;
            }

            ConfigureCanvas(canvas.gameObject);
            BuildHud(hud);
            BuildLevelResult(result);
            BuildUpgradeSelection(selection);
            BuildRunEnd(end);
            PrefabUtility.SaveAsPrefabAsset(root, RunSystemsPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Esvazia o "Panel" da view (o filho que ela liga e desliga) e o deixa pronto para receber a tela nova.
    private static RectTransform ResetPanel(MonoBehaviour view, bool dimmed, float dimAlphaScale = 1f)
    {
        Transform existing = view.transform.Find("Panel");
        RectTransform panel = existing != null ? (RectTransform)existing : Node("Panel", view.transform);
        ClearChildren(panel);
        Fill(panel);
        Fill(view);

        Remove<Image>(panel.gameObject);
        if (dimmed)
        {
            // Véu escuro que também bloqueia cliques no que estiver atrás.
            var veil = panel.gameObject.AddComponent<Image>();
            Color c = Theme.dim;
            c.a = Mathf.Clamp01(c.a * dimAlphaScale);
            veil.color = c;
            veil.raycastTarget = true;
        }

        panel.gameObject.SetActive(false);
        SetRef(view, "panel", panel.gameObject);
        return panel;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    // ───────────────────────────── HUD ─────────────────────────────

    private static void BuildHud(RunHudView view)
    {
        RectTransform panel = ResetPanel(view, false);

        // Canto superior esquerdo: nome da fase numa placa que sangra para fora da tela, habilidades logo abaixo.
        RectTransform levelGroup = Place(Node("LevelGroup", panel), TopLeft, TopLeft, Vector2.zero, Vector2.zero, Theme.tilt * 0.5f);
        Enter(levelGroup, new Vector2(-760f, 0f), 0.05f, 0.32f);

        UiSlant levelSlab = Shadow(Slab(levelGroup, "LevelSlab", Theme.ink, 72f), Theme.accent, 8f, -8f);
        Place(levelSlab, TopLeft, TopLeft, new Vector2(-40f, -36f), new Vector2(610f, 72f));
        TextMeshProUGUI levelText = AutoSize(Label(levelSlab.transform, "LevelText", "Fase 1 · 1/3", Theme.displayFont, 40f, Theme.paper), 24f);
        Fill(levelText, 86f, 0f, 44f, 0f);

        UiSlant abilitiesTag = Slab(levelGroup, "AbilitiesTag", Theme.accentAlt, 38f);
        Place(abilitiesTag, TopLeft, TopLeft, new Vector2(-40f, -122f), new Vector2(520f, 38f));
        TextMeshProUGUI abilitiesText = AutoSize(Label(abilitiesTag.transform, "AbilitiesText", "Pulo duplo · Gancho", Theme.bodyFont, 24f, Theme.ink), 16f);
        Fill(abilitiesText, 86f, 0f, 30f, 0f);

        // Topo central: cronômetro grande, limite pequeno ao lado, barra de tempo e a meta numa etiqueta.
        RectTransform timerGroup = Place(Node("TimerGroup", panel), TopCenter, TopCenter, Vector2.zero, Vector2.zero);
        Enter(timerGroup, new Vector2(0f, 280f), 0.1f, 0.32f);

        UiSlant timeSlab = Slab(timerGroup, "TimeSlab", Theme.ink, 110f);
        Place(timeSlab, TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(540f, 110f));
        TextMeshProUGUI timeText = Label(timeSlab.transform, "TimeText", "00:00.0", Theme.displayFont, 88f, Theme.paper);
        Place(timeText, MidLeft, MidLeft, new Vector2(46f, -2f), new Vector2(340f, 110f));
        TextMeshProUGUI limitText = Label(timeSlab.transform, "LimitText", "/ 00:20.0", Theme.bodyFont, 28f, WithAlpha(Theme.paper, 0.72f), TextAlignmentOptions.BottomRight);
        Place(limitText, BottomRight, BottomRight, new Vector2(-40f, 16f), new Vector2(160f, 40f));

        UiSlant track = Slab(timerGroup, "TimeBarTrack", Theme.ink, 16f);
        Place(track, TopCenter, TopCenter, new Vector2(-16f, -146f), new Vector2(540f, 16f));
        UiSlant timeBar = Fill(Slab(track.transform, "TimeBar", Theme.accentAlt, 16f));
        timeBar.Fill = 0f;

        UiSlant targetChip = Slab(timerGroup, "TargetChip", Theme.highlight, 44f);
        Place(targetChip, TopCenter, TopCenter, new Vector2(170f, -172f), new Vector2(260f, 44f), -3f);
        TextMeshProUGUI targetText = Label(targetChip.transform, "TargetText", "Alvo 00:12.0", Theme.displayFont, 28f, Theme.ink, TextAlignmentOptions.Center);
        Fill(targetText);

        SetRef(view, "levelText", levelText);
        SetRef(view, "timeText", timeText);
        SetRef(view, "targetText", targetText);
        SetRef(view, "abilitiesText", abilitiesText);
        SetRef(view, "abilitiesRoot", abilitiesTag.gameObject);
        SetRef(view, "limitText", limitText);
        SetRef(view, "timeBar", timeBar);
        SetValue(view, "digitWidthEm", p => p.floatValue = 0.46f);
        SetValue(view, "normalColor", p => p.colorValue = Theme.paper);
        SetValue(view, "warningColor", p => p.colorValue = Theme.accent);
    }

    // ───────────────────────────── Resultado da fase ─────────────────────────────

    private static void BuildLevelResult(LevelResultView view)
    {
        RectTransform panel = ResetPanel(view, true);

        // Tudo que é "manchete" vive num eixo inclinado: faixa clara atravessando a tela, com o tempo em cima dela.
        RectTransform tilted = Place(Node("Tilted", panel), Center, Center, new Vector2(0f, 40f), Vector2.zero, Theme.tilt * 1.5f);

        UiSlant accentBand = Slab(tilted, "AccentBand", Theme.accent, 44f);
        Place(accentBand, Center, Center, new Vector2(0f, -128f), new Vector2(2900f, 44f));
        Enter(accentBand, new Vector2(2600f, 0f), 0.06f, 0.42f);

        UiSlant band = Slab(tilted, "Band", Theme.paper, 320f);
        Place(band, Center, Center, new Vector2(0f, 70f), new Vector2(2900f, 320f));
        Enter(band, new Vector2(-2600f, 0f), 0f, 0.38f);

        UiSlant tag = Slab(tilted, "Tag", Theme.accent, 48f);
        Place(tag, Center, MidLeft, new Vector2(-830f, 262f), new Vector2(340f, 48f));
        Enter(tag, new Vector2(-400f, 0f), 0.16f);
        Fill(Label(tag.transform, "TagText", "Fase concluída", Theme.displayFont, 28f, Theme.paper, TextAlignmentOptions.Center));

        TextMeshProUGUI titleText = AutoSize(Label(tilted, "TitleText", "Fase 1", Theme.displayFont, 100f, Theme.ink), 48f);
        Place(titleText, Center, MidLeft, new Vector2(-800f, 156f), new Vector2(980f, 120f));
        Enter(titleText, new Vector2(-500f, 0f), 0.14f);

        TextMeshProUGUI timeText = Label(tilted, "TimeText", "00:10.0", Theme.displayFont, 168f, Theme.ink);
        Place(timeText, Center, MidLeft, new Vector2(-806f, 14f), new Vector2(900f, 180f));
        Enter(timeText, new Vector2(-500f, 0f), 0.2f);

        UiSlant targetChip = Slab(tilted, "TargetChip", Theme.highlight, 54f);
        Place(targetChip, Center, MidLeft, new Vector2(-800f, -210f), new Vector2(340f, 54f));
        Enter(targetChip, new Vector2(-400f, 0f), 0.26f);
        TextMeshProUGUI targetText = Label(targetChip.transform, "TargetText", "Alvo 00:12.0", Theme.displayFont, 32f, Theme.ink, TextAlignmentOptions.Center);
        Fill(targetText);

        UiSlant limitChip = Shadow(Slab(tilted, "LimitChip", Theme.ink, 54f), Theme.accentAlt, 8f, -8f);
        Place(limitChip, Center, MidLeft, new Vector2(-430f, -210f), new Vector2(360f, 54f));
        Enter(limitChip, new Vector2(-400f, 0f), 0.3f);
        TextMeshProUGUI limitText = Label(limitChip.transform, "LimitText", "Limite 00:20.0", Theme.displayFont, 32f, Theme.paper, TextAlignmentOptions.Center);
        Fill(limitText);

        // Nota: carimbada por cima, girada contra o eixo da faixa.
        RectTransform stamp = Place(Node("GradeStamp", tilted), Center, Center, new Vector2(560f, 30f), new Vector2(440f, 520f), -12f);
        Enter(stamp, Vector2.zero, 0.34f, 0.3f, 2.4f, 30f);
        UiSlant stampPlate = Fill(Shadow(Slab(stamp, "Plate", Theme.ink, 520f, 0.6f), Theme.accent, 20f, -20f));
        TextMeshProUGUI gradeLabel = Label(stamp, "GradeLabel", "Nota", Theme.displayFont, 36f, Theme.paper);
        Place(gradeLabel, TopLeft, TopLeft, new Vector2(74f, -26f), new Vector2(200f, 44f));
        TextMeshProUGUI gradeText = Label(stamp, "GradeText", "S", Theme.displayFont, 400f, Theme.paper, TextAlignmentOptions.Center);
        Fill(gradeText, 0f, -10f, 0f, 30f);

        RectTransform continueRoot = Place(Node("ContinueButton", panel), BottomRight, BottomRight, new Vector2(-130f, 96f), new Vector2(480f, 100f), Theme.tilt * 0.75f);
        Enter(continueRoot, new Vector2(700f, 0f), 0.36f);
        Button continueButton = SkinButton(continueRoot, "Continuar", 52f, out TextMeshProUGUI continueLabel);

        SetRef(view, "titleText", titleText);
        SetRef(view, "timeText", timeText);
        SetRef(view, "targetText", targetText);
        SetRef(view, "limitText", limitText);
        SetRef(view, "gradeText", gradeText);
        SetRef(view, "continueButton", continueButton);
        SetRef(view, "continueLabel", continueLabel);
        SetValue(view, "gradeColors", p =>
        {
            // Ordem de PerformanceGrade: C, B, A, S.
            Color[] colors = { WithAlpha(Theme.paper, 0.8f), Theme.accentAlt, Theme.highlight, Theme.accent };
            p.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++) p.GetArrayElementAtIndex(i).colorValue = colors[i];
        });
    }

    // ───────────────────────────── Escolha de upgrade ─────────────────────────────

    private static void RebuildCardPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CardPath);
        try
        {
            var view = root.GetComponent<UpgradeCardView>();
            if (view == null)
            {
                Debug.LogError("[UI] - UpgradeCard sem UpgradeCardView; carta não alterada");
                return;
            }

            const float width = 400f;
            const float height = 580f;
            const float slantScale = 0.3f;
            float skew = height * Theme.slant * slantScale;

            ClearChildren(root.transform);
            Remove<UiSlantButton>(root);
            Remove<Image>(root);

            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(width, height);
            var element = root.GetComponent<LayoutElement>();
            if (element == null) element = root.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = width;
            element.minHeight = element.preferredHeight = height;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;

            // Raiz: posicionada pelo layout. Enter: entrada. Visual: o que sobe/cresce quando a carta está selecionada.
            RectTransform enter = Fill(Node("Enter", root.transform));
            Enter(enter, new Vector2(0f, -760f), 0.12f, 0.34f);
            RectTransform visual = Fill(Node("Visual", enter));

            // Bloco de trás na cor da raridade, deslocado: a "sombra" colorida da carta.
            UiSlant back = Fill(Slab(visual, "RarityBack", Theme.paper, height, slantScale), 16f, -16f, -16f, 16f);

            UiSlant plate = Fill(Slab(visual, "Plate", Theme.ink, height, slantScale));
            plate.raycastTarget = true;

            UiSlant strip = Slab(visual, "RarityStrip", Theme.paper, 16f, slantScale);
            var stripRt = (RectTransform)strip.transform;
            stripRt.anchorMin = new Vector2(0f, 1f);
            stripRt.anchorMax = new Vector2(1f, 1f);
            stripRt.pivot = new Vector2(0.5f, 1f);
            stripRt.offsetMin = new Vector2(skew * 0.5f, -16f);
            stripRt.offsetMax = new Vector2(skew * 0.5f, 0f);

            TextMeshProUGUI keyHint = Label(visual, "KeyHint", "1", Theme.displayFont, 280f, WithAlpha(Theme.paper, 0.07f), TextAlignmentOptions.BottomRight);
            Place(keyHint, BottomRight, BottomRight, new Vector2(-30f, -10f), new Vector2(260f, 300f));

            TextMeshProUGUI rarityText = Label(visual, "RarityText", "Raridade", Theme.bodyFont, 28f, Theme.paper);
            Place(rarityText, TopLeft, TopLeft, new Vector2(58f, -40f), new Vector2(230f, 40f));

            var icon = Node("Icon", visual).gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Place(icon, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(92f, 92f));

            TextMeshProUGUI nameText = AutoSize(Label(visual, "NameText", "Nome do upgrade", Theme.displayFont, 54f, Theme.paper, TextAlignmentOptions.TopLeft, true), 30f);
            Place(nameText, TopLeft, TopLeft, new Vector2(52f, -100f), new Vector2(310f, 150f));

            TextMeshProUGUI descriptionText = AutoSize(Label(visual, "DescriptionText", "Descrição do upgrade.", Theme.bodyFont, 27f, WithAlpha(Theme.paper, 0.86f), TextAlignmentOptions.TopLeft, true, false), 18f);
            Place(descriptionText, TopLeft, TopLeft, new Vector2(46f, -268f), new Vector2(310f, 210f));

            TextMeshProUGUI stackText = Label(visual, "StackText", "Até 2×", Theme.bodyFont, 24f, Theme.highlight, TextAlignmentOptions.BottomLeft);
            Place(stackText, BottomLeft, BottomLeft, new Vector2(36f, 44f), new Vector2(300f, 36f));

            // Barra de seleção na base: aparece só na carta em foco.
            UiSlant selectBar = Slab(visual, "SelectBar", Theme.accent, 18f, slantScale);
            var barRt = (RectTransform)selectBar.transform;
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.offsetMin = new Vector2(-skew * 0.5f, 0f);
            barRt.offsetMax = new Vector2(-skew * 0.5f, 18f);

            var button = root.GetComponent<Button>();
            if (button == null) button = root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = plate;

            var skin = root.AddComponent<UiSlantButton>();
            skin.Bind(button, visual, plate, null, selectBar);
            skin.SetColors(Theme.ink, new Color(0.11f, 0.11f, 0.18f, 1f), Theme.paper, Theme.paper);
            skin.SetMotion(1.06f, new Vector2(0f, 30f), -1.5f);

            SetRef(view, "button", button);
            SetRef(view, "background", strip);
            SetRef(view, "border", back);
            SetRef(view, "icon", icon);
            SetRef(view, "nameText", nameText);
            SetRef(view, "descriptionText", descriptionText);
            SetRef(view, "rarityText", rarityText);
            SetRef(view, "stackText", stackText);

            PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildUpgradeSelection(UpgradeSelectionView view)
    {
        RectTransform panel = ResetPanel(view, true);

        RectTransform header = Place(Node("Header", panel), TopLeft, TopLeft, Vector2.zero, Vector2.zero, Theme.tilt);
        Enter(header, new Vector2(-1400f, 0f), 0f, 0.36f);
        UiSlant titleSlab = Shadow(Slab(header, "TitleSlab", Theme.accent, 150f), Theme.paper, 14f, -14f);
        Place(titleSlab, TopLeft, TopLeft, new Vector2(-70f, -96f), new Vector2(1230f, 150f));
        TextMeshProUGUI titleText = AutoSize(Label(titleSlab.transform, "TitleText", "Escolha um upgrade", Theme.displayFont, 104f, Theme.paper), 60f);
        if (Theme.displayShadowMaterial != null) titleText.fontSharedMaterial = Theme.displayShadowMaterial;
        Fill(titleText, 130f, 0f, 60f, 0f);

        RectTransform cards = Place(Node("Cards", panel), Center, Center, new Vector2(0f, -80f), new Vector2(1500f, 620f));
        var layout = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 70f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        var cardViews = new UpgradeCardView[3];
        for (int i = 0; i < cardViews.Length; i++)
        {
            var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cards);
            card.name = "UpgradeCard" + (i + 1);
            cardViews[i] = card.GetComponent<UpgradeCardView>();

            // Entrada em cascata e o número da tecla que escolhe a carta.
            card.GetComponentInChildren<UiEnter>(true).Setup(new Vector2(0f, -760f), 0.12f + i * 0.08f, 0.34f);
            Transform hint = card.transform.Find("Enter/Visual/KeyHint");
            if (hint != null) hint.GetComponent<TextMeshProUGUI>().text = (i + 1).ToString();
        }

        TextMeshProUGUI footer = Label(panel, "Hint", "1 · 2 · 3 ou clique para escolher", Theme.bodyFont, 30f, WithAlpha(Theme.paper, 0.75f), TextAlignmentOptions.Center);
        Place(footer, BottomCenter, BottomCenter, new Vector2(0f, 64f), new Vector2(1200f, 44f));

        SetRef(view, "titleText", titleText);
        SetValue(view, "cards", p =>
        {
            p.arraySize = cardViews.Length;
            for (int i = 0; i < cardViews.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = cardViews[i];
        });
    }

    // ───────────────────────────── Fim de run ─────────────────────────────

    private static void BuildRunEnd(RunEndView view)
    {
        RectTransform panel = ResetPanel(view, true, 1.1f);

        // Manchete: faixa preta inclinada com o título na cor do desfecho (a view escolhe a cor).
        RectTransform tilted = Place(Node("Tilted", panel), TopCenter, TopCenter, new Vector2(0f, -250f), Vector2.zero, Theme.tilt * 1.25f);

        UiSlant band = Shadow(Slab(tilted, "Band", Theme.ink, 250f), Theme.accent, 0f, -26f);
        Place(band, Center, Center, Vector2.zero, new Vector2(2900f, 250f));
        Enter(band, new Vector2(-2600f, 0f), 0f, 0.38f);

        TextMeshProUGUI titleText = AutoSize(Label(tilted, "TitleText", "Vitória!", Theme.displayFont, 200f, Theme.highlight), 110f);
        Place(titleText, Center, MidLeft, new Vector2(-830f, -2f), new Vector2(1600f, 240f));
        Enter(titleText, new Vector2(-700f, 0f), 0.12f);

        UiSlant reasonSlab = Slab(tilted, "ReasonSlab", Theme.paper, 64f);
        Place(reasonSlab, Center, MidLeft, new Vector2(-790f, -182f), new Vector2(1040f, 64f));
        Enter(reasonSlab, new Vector2(-600f, 0f), 0.2f);
        TextMeshProUGUI reasonText = AutoSize(Label(reasonSlab.transform, "ReasonText", "Todas as fases concluídas!", Theme.displayFont, 38f, Theme.ink), 22f);
        Fill(reasonText, 44f, 0f, 36f, 0f);

        // Corpo: resumo e fases à esquerda, upgrades à direita.
        RectTransform body = Fill(Node("Body", panel));
        Enter(body, new Vector2(0f, -120f), 0.26f, 0.3f);

        TextMeshProUGUI summaryText = AutoSize(Label(body, "SummaryText", "3/3 fases · 00:45.0", Theme.displayFont, 76f, Theme.paper), 40f);
        Place(summaryText, MidLeft, MidLeft, new Vector2(150f, -70f), new Vector2(820f, 96f));

        UiSlant rule = Slab(body, "Rule", Theme.accentAlt, 10f);
        Place(rule, MidLeft, MidLeft, new Vector2(150f, -128f), new Vector2(760f, 10f));

        TextMeshProUGUI levelsText = Label(body, "LevelsText", "Fase 1 · 00:10.0 · S", Theme.bodyFont, 34f, Theme.paper, TextAlignmentOptions.TopLeft);
        Place(levelsText, MidLeft, TopLeft, new Vector2(154f, -156f), new Vector2(800f, 300f));
        levelsText.lineSpacing = 12f;

        TextMeshProUGUI upgradesText = AutoSize(Label(body, "UpgradesText", "Upgrades: nenhum", Theme.bodyFont, 30f, WithAlpha(Theme.paper, 0.9f), TextAlignmentOptions.TopLeft, true, false), 20f);
        Place(upgradesText, Center, TopLeft, new Vector2(100f, -156f), new Vector2(720f, 250f));

        TextMeshProUGUI seedText = Label(panel, "SeedText", "Seed 0", Theme.bodyFont, 22f, WithAlpha(Theme.paper, 0.45f), TextAlignmentOptions.BottomLeft);
        Place(seedText, BottomLeft, BottomLeft, new Vector2(60f, 40f), new Vector2(500f, 30f));

        RectTransform menuRoot = Place(Node("MenuButton", panel), BottomRight, BottomRight, new Vector2(-130f, 96f), new Vector2(540f, 100f), Theme.tilt * 0.75f);
        Enter(menuRoot, new Vector2(700f, 0f), 0.36f);
        Button menuButton = SkinButton(menuRoot, "Voltar ao menu", 52f, out _);

        SetRef(view, "titleText", titleText);
        SetRef(view, "reasonText", reasonText);
        SetRef(view, "summaryText", summaryText);
        SetRef(view, "levelsText", levelsText);
        SetRef(view, "upgradesText", upgradesText);
        SetRef(view, "seedText", seedText);
        SetRef(view, "menuButton", menuButton);
        SetValue(view, "victoryColor", p => p.colorValue = Theme.highlight);
        SetValue(view, "defeatColor", p => p.colorValue = Theme.accent);
    }
}
