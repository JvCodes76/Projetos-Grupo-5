using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector do <see cref="AmbienceFX"/>: presets com um clique, pré-visualização fora do Play Mode, botão de raio e
/// lista de sorting layers. Também cria os objetos pelo menu GameObject → Ambiência.
/// </summary>
[CustomEditor(typeof(AmbienceFX))]
public sealed class AmbienceFXEditor : Editor
{
    private static AmbiencePreset selectedPreset = AmbiencePreset.Chuva;

    private bool previewing;
    private double lastPreviewTime;

    private void OnDisable()
    {
        // A pré-visualização vive só enquanto o objeto está selecionado: nada fica para trás na cena.
        StopPreview();
    }

    public override bool RequiresConstantRepaint()
    {
        return previewing || Application.isPlaying;
    }

    public override void OnInspectorGUI()
    {
        var fx = (AmbienceFX)target;

        DrawPresets(fx);
        DrawPreview(fx);
        EditorGUILayout.Space();

        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_Script") continue;

            if (property.name == "sortingLayer") DrawSortingLayer(property);
            else EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPresets(AmbienceFX fx)
    {
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            selectedPreset = (AmbiencePreset)EditorGUILayout.EnumPopup(selectedPreset);
            if (GUILayout.Button("Aplicar", GUILayout.Width(70f)))
            {
                Undo.RecordObject(fx, "Aplicar preset de ambiência");
                fx.ApplyPreset(selectedPreset);
                EditorUtility.SetDirty(fx);
            }
        }
    }

    private void DrawPreview(AmbienceFX fx)
    {
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox($"Play Mode: os ajustes valem na hora. Partículas vivas: {fx.ActiveParticleCount}", MessageType.Info);
        }
        else
        {
            bool wanted = GUILayout.Toggle(previewing, previewing ? "■ Parar pré-visualização" : "▶ Pré-visualizar na câmera do jogo", "Button");
            if (wanted && !previewing) StartPreview(fx);
            else if (!wanted && previewing) StopPreview();

            if (previewing)
            {
                EditorGUILayout.HelpBox($"Pré-visualizando na Game View (nada é salvo na cena). Partículas vivas: {fx.ActiveParticleCount}", MessageType.Info);
            }
        }

        using (new EditorGUI.DisabledScope(!fx.IsBuilt))
        {
            if (GUILayout.Button("Disparar raio")) fx.TriggerLightning();
        }
    }

    private static void DrawSortingLayer(SerializedProperty property)
    {
        SortingLayer[] layers = SortingLayer.layers;
        var names = new string[layers.Length];
        int current = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            names[i] = layers[i].name;
            if (names[i] == property.stringValue) current = i;
        }

        if (current < 0)
        {
            // Layer apagada ou renomeada: mostra o texto para o usuário corrigir.
            EditorGUILayout.PropertyField(property);
            EditorGUILayout.HelpBox($"A sorting layer '{property.stringValue}' não existe; os efeitos caem na Default.", MessageType.Warning);
        }

        int chosen = EditorGUILayout.Popup(new GUIContent("Sorting Layer", property.tooltip), current, names);
        if (chosen >= 0 && chosen != current) property.stringValue = names[chosen];
    }

    // ───────────────────────────── Pré-visualização ─────────────────────────────

    private void StartPreview(AmbienceFX fx)
    {
        if (previewing || Application.isPlaying) return;

        previewing = true;
        lastPreviewTime = EditorApplication.timeSinceStartup;
        fx.EditorPreviewStart();
        EditorApplication.update += PreviewUpdate;
    }

    private void StopPreview()
    {
        if (!previewing) return;

        previewing = false;
        EditorApplication.update -= PreviewUpdate;

        var fx = target as AmbienceFX;
        if (fx != null) fx.EditorPreviewStop();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private void PreviewUpdate()
    {
        var fx = target as AmbienceFX;
        if (fx == null || Application.isPlaying || !fx.isActiveAndEnabled)
        {
            StopPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - lastPreviewTime), 0.05f);
        lastPreviewTime = now;

        fx.EditorPreviewTick(dt);
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    // ───────────────────────────── Menu de criação ─────────────────────────────

    [MenuItem("GameObject/Ambiência/Chuva", false, 10)]
    private static void CreateRain(MenuCommand command) => Create(command, AmbiencePreset.Chuva);

    [MenuItem("GameObject/Ambiência/Garoa", false, 10)]
    private static void CreateDrizzle(MenuCommand command) => Create(command, AmbiencePreset.Garoa);

    [MenuItem("GameObject/Ambiência/Tempestade (chuva + trovões)", false, 10)]
    private static void CreateStorm(MenuCommand command) => Create(command, AmbiencePreset.Tempestade);

    [MenuItem("GameObject/Ambiência/Neblina", false, 10)]
    private static void CreateFog(MenuCommand command) => Create(command, AmbiencePreset.Neblina);

    [MenuItem("GameObject/Ambiência/Neve", false, 10)]
    private static void CreateSnow(MenuCommand command) => Create(command, AmbiencePreset.Neve);

    [MenuItem("GameObject/Ambiência/Poeira", false, 10)]
    private static void CreateDust(MenuCommand command) => Create(command, AmbiencePreset.Poeira);

    [MenuItem("GameObject/Ambiência/Vagalumes", false, 10)]
    private static void CreateFireflies(MenuCommand command) => Create(command, AmbiencePreset.Vagalumes);

    [MenuItem("GameObject/Ambiência/Brasas", false, 10)]
    private static void CreateEmbers(MenuCommand command) => Create(command, AmbiencePreset.Brasas);

    [MenuItem("GameObject/Ambiência/Vazio (configurar à mão)", false, 11)]
    private static void CreateEmpty(MenuCommand command) => Create(command, AmbiencePreset.Limpo);

    private static void Create(MenuCommand command, AmbiencePreset preset)
    {
        string label = preset == AmbiencePreset.Limpo ? "Ambiência" : $"Ambiência - {preset}";
        var go = new GameObject(label);
        go.AddComponent<AmbienceFX>().ApplyPreset(preset);

        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, $"Criar {label}");
        Selection.activeGameObject = go;
    }
}
