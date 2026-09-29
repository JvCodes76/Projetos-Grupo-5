using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Cameras;
using Roguelike.Levels;
using Roguelike.Movement;
using Roguelike.Stats;
using Roguelike.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Montagem da Etapa M no Editor (SPEC §17: M.13 e M.15), em passos idempotentes que também rodam em batchmode
/// (-executeMethod MovementProjectSetup.RunAllBatch):
/// 1. layer 7 → "KillZone" (DS-13); 2. assets de dados (perfis, kits, tuning, cues) + PlayerBaseStats → MovementProfile
/// + upgrade Dash no pool; 3. Cyborg_Motor.controller; 4. Cyborg.prefab convertido no lugar (mesmo GUID e fileID, §13.2);
/// 5. DevPlayBootstrap.prefab; 6. MovementGym.unity; 7. câmera do MainMenu com o CameraProfile; 8. validador.
/// </summary>
public static class MovementProjectSetup
{
    public const string DataFolder = "Assets/_Roguelike/Data/Movement";
    public const string ProfilePath = DataFolder + "/MovementProfile_Default.asset";
    public const string CameraProfilePath = DataFolder + "/CameraProfile_Default.asset";
    public const string TuningPath = DataFolder + "/FeedbackTuning.asset";
    public const string AudioCuesPath = DataFolder + "/AudioCueSet_Cyborg.asset";
    public const string KitAllPath = DataFolder + "/MovementKit_All.asset";
    public const string CyborgPrefabPath = "Assets/Prefabs/Cyborg.prefab";
    public const string BootstrapPrefabPath = "Assets/Prefabs/Dev/DevPlayBootstrap.prefab";
    public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string PlayerBaseStatsPath = "Assets/_Roguelike/Data/PlayerBaseStats.asset";
    private const string RunConfigPath = "Assets/_Roguelike/Data/RunConfig.asset";
    private const string DashUpgradePath = "Assets/_Roguelike/Data/Upgrades/Upgrade_Dash.asset";
    private const string EpicRarityPath = "Assets/_Roguelike/Data/Rarities/Rarity_Epic.asset";

    [MenuItem("Tools/Movement/Setup/Run All Steps")]
    public static void RunAllMenu()
    {
        RunAll();
    }

    /// <summary>Entrada do batchmode: roda tudo e sai com 0 (ok) ou 1 (validador com erros ou exceção).</summary>
    public static void RunAllBatch()
    {
        int code = 0;
        try
        {
            code = RunAll() ? 0 : 1;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            code = 1;
        }

        EditorApplication.Exit(code);
    }

    public static bool RunAll()
    {
        EnsureKillZoneLayer();
        EnsureDataAssets();
        CyborgAnimatorBuilder.Build();
        ConvertCyborgPrefab();
        EnsureBootstrapPrefab();
        MovementGymBuilder.Build();
        ConfigureMainMenuCamera();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        List<string> errors = CyborgPrefabValidator.Validate(AssetDatabase.LoadAssetAtPath<GameObject>(CyborgPrefabPath));
        foreach (string error in errors) Debug.LogError("[Movement] - Validator: " + error);
        Debug.Log(errors.Count == 0 ? "[Movement] - Setup da Etapa M concluído; validador verde" : $"[Movement] - Setup com {errors.Count} erros no validador");
        return errors.Count == 0;
    }

    // ───────────────────────────── 1. Layer ─────────────────────────────

    [MenuItem("Tools/Movement/Setup/1 Layer 7 = KillZone")]
    public static void EnsureKillZoneLayer()
    {
        Object tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var tagManager = new SerializedObject(tagManagerAsset);
        SerializedProperty layers = tagManager.FindProperty("layers");
        SerializedProperty seven = layers.GetArrayElementAtIndex(7);
        if (seven.stringValue == "KillZone") return;
        Debug.Log($"[Movement] - Layer 7 '{seven.stringValue}' → 'KillZone' (DS-13)");
        seven.stringValue = "KillZone";
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    // ───────────────────────────── 2. Dados ─────────────────────────────

    [MenuItem("Tools/Movement/Setup/2 Data Assets")]
    public static void EnsureDataAssets()
    {
        EnsureFolder(DataFolder);

        MovementProfile profile = LoadOrCreate<MovementProfile>(ProfilePath);
        LoadOrCreate<CameraProfile>(CameraProfilePath);
        LoadOrCreate<FeedbackTuning>(TuningPath);
        AudioCueSet cues = LoadOrCreate<AudioCueSet>(AudioCuesPath);
        cues.EnsureAllCues();
        EditorUtility.SetDirty(cues);

        CreateKit("MovementKit_Base", AbilityFlags.None, 0, 0);
        CreateKit("MovementKit_AirJump", AbilityFlags.None, 1, 0);
        CreateKit("MovementKit_Wall", AbilityFlags.WallJump, 0, 0);
        CreateKit("MovementKit_AirWall", AbilityFlags.WallJump, 1, 0);
        CreateKit("MovementKit_All", AbilityFlags.WallJump | AbilityFlags.GrapplingHook, 1, 1);

        // PlayerBaseStats → MovementProfile (fonte única das bases e tetos, SPEC §17.3 / 2.1).
        var baseStats = AssetDatabase.LoadAssetAtPath<PlayerBaseStats>(PlayerBaseStatsPath);
        if (baseStats != null)
        {
            var so = new SerializedObject(baseStats);
            SerializedProperty prop = so.FindProperty("movementProfile");
            if (prop.objectReferenceValue != profile)
            {
                prop.objectReferenceValue = profile;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EnsureDashUpgrade();
        AssetDatabase.SaveAssets();
    }

    private static void CreateKit(string name, AbilityFlags flags, int airJumps, int dashes)
    {
        MovementKit kit = LoadOrCreate<MovementKit>(DataFolder + "/" + name + ".asset");
        var so = new SerializedObject(kit);
        so.FindProperty("abilities").intValue = (int)flags;
        so.FindProperty("maxAirJumps").intValue = airJumps;
        so.FindProperty("maxDashes").intValue = dashes;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Dash é P1 e Épico (Q5, PRD §9.4): sem este upgrade a mecânica não aparece na run.
    private static void EnsureDashUpgrade()
    {
        var epic = AssetDatabase.LoadAssetAtPath<RarityDefinition>(EpicRarityPath);
        if (epic == null)
        {
            Debug.LogWarning("[Movement] - Raridade épica não encontrada; upgrade Dash não criado");
            return;
        }

        UpgradeDefinition dash = LoadOrCreate<UpgradeDefinition>(DashUpgradePath);
        var so = new SerializedObject(dash);
        so.FindProperty("id").stringValue = "dash";
        so.FindProperty("displayName").stringValue = "Dash";
        so.FindProperty("description").stringValue = "Ganha um dash em 8 direções (recarrega no chão).";
        so.FindProperty("rarity").objectReferenceValue = epic;
        so.FindProperty("maxStacks").intValue = 1;
        SerializedProperty modifiers = so.FindProperty("modifiers");
        modifiers.arraySize = 1;
        SerializedProperty modifier = modifiers.GetArrayElementAtIndex(0);
        modifier.FindPropertyRelative("stat").intValue = (int)StatType.MaxDashes;
        modifier.FindPropertyRelative("operation").intValue = (int)ModifierOperation.Add;
        modifier.FindPropertyRelative("value").floatValue = 1f;
        so.FindProperty("unlocks").intValue = (int)AbilityFlags.None;
        so.ApplyModifiedPropertiesWithoutUndo();

        var runConfig = AssetDatabase.LoadAssetAtPath<RunConfig>(RunConfigPath);
        if (runConfig == null) return;
        var rc = new SerializedObject(runConfig);
        SerializedProperty pool = rc.FindProperty("upgradePool");
        for (int i = 0; i < pool.arraySize; i++)
        {
            if (pool.GetArrayElementAtIndex(i).objectReferenceValue == dash) return;
        }

        pool.arraySize++;
        pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = dash;
        rc.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[Movement] - Upgrade 'dash' (Épico) adicionado ao pool do RunConfig");
    }

    // ───────────────────────────── 4. Cyborg.prefab (M.15) ─────────────────────────────

    [MenuItem("Tools/Movement/Setup/4 Convert Cyborg Prefab")]
    public static void ConvertCyborgPrefab()
    {
        var profile = AssetDatabase.LoadAssetAtPath<MovementProfile>(ProfilePath);
        var tuning = AssetDatabase.LoadAssetAtPath<FeedbackTuning>(TuningPath);
        var cues = AssetDatabase.LoadAssetAtPath<AudioCueSet>(AudioCuesPath);
        var animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CyborgAnimatorBuilder.ControllerPath);

        GameObject root = PrefabUtility.LoadPrefabContents(CyborgPrefabPath);
        try
        {
            Transform rootT = root.transform;
            bool alreadyConverted = root.GetComponent<PlayerController>() != null && rootT.localScale == Vector3.one;

            // Estado antigo: escala 1,7 no root e pivô ~0,777 u acima dos pés, com o colisor deslocado −0,088 u em X.
            Vector3 oldScale = rootT.localScale;
            var feetOffset = new Vector2(0.088f, 0.777f);

            // Componentes que saem (§13.2): scripts apagados (CharacterAnimator, GrapplingHook), PlayerInput, GroundCheck.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            PlayerInput playerInput = root.GetComponent<PlayerInput>();
            if (playerInput != null) Object.DestroyImmediate(playerInput, true);
            Transform groundCheck = rootT.Find("GroundCheck");
            if (groundCheck != null) Object.DestroyImmediate(groundCheck.gameObject, true);

            // Hierarquia visual: VisualRoot → SquashPivot → Sprite (SpriteRenderer + Animator, escala 1,7).
            Transform visualRoot = FindOrCreateChild(rootT, "VisualRoot");
            Transform squashPivot = FindOrCreateChild(visualRoot, "SquashPivot");
            Transform spriteT = FindOrCreateChild(squashPivot, "Sprite");
            spriteT.localPosition = new Vector3(feetOffset.x, feetOffset.y, 0f);
            spriteT.localScale = new Vector3(1.7f, 1.7f, 1f);

            SpriteRenderer spriteRenderer = spriteT.GetComponent<SpriteRenderer>();
            SpriteRenderer oldRenderer = root.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = spriteT.gameObject.AddComponent<SpriteRenderer>();
                if (oldRenderer != null) EditorUtility.CopySerialized(oldRenderer, spriteRenderer);
            }

            if (oldRenderer != null) Object.DestroyImmediate(oldRenderer, true);
            spriteRenderer.flipX = false;

            Animator oldAnimator = root.GetComponent<Animator>();
            if (oldAnimator != null) Object.DestroyImmediate(oldAnimator, true);
            Animator animator = spriteT.GetComponent<Animator>();
            if (animator == null) animator = spriteT.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Filhos do gancho: reposicionados para a origem nos pés e escala 1 no root (HookTip × 1,7).
            if (!alreadyConverted)
            {
                foreach (string childName in new[] { "HookTip", "RopeVisual", "FirePoint" })
                {
                    Transform child = rootT.Find(childName);
                    if (child == null) continue;
                    Vector3 lp = child.localPosition;
                    child.localPosition = new Vector3(lp.x * oldScale.x + feetOffset.x, lp.y * oldScale.y + feetOffset.y, 0f);
                    Vector3 ls = child.localScale;
                    child.localScale = new Vector3(ls.x * oldScale.x, ls.y * oldScale.y, 1f);
                }
            }

            rootT.localScale = Vector3.one;

            // Corpo físico (§5.1): cinemático, contatos cinemáticos completos, sem interpolação da engine.
            var body = root.GetComponent<Rigidbody2D>();
            if (body == null) body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.sharedMaterial = null;

            var box = root.GetComponent<BoxCollider2D>();
            if (box == null) box = root.AddComponent<BoxCollider2D>();
            Vector2 bodySize = profile != null ? profile.BodySize : new Vector2(0.51f, 1.26f);
            box.size = bodySize;
            box.offset = new Vector2(0f, bodySize.y * 0.5f);
            box.edgeRadius = 0f;
            box.isTrigger = false;
            box.sharedMaterial = null;

            // Poeira nos pés (partículas em espaço de mundo, disparadas por Emit).
            Transform dustT = FindOrCreateChild(visualRoot, "Dust");
            ParticleSystem dust = dustT.GetComponent<ParticleSystem>();
            if (dust == null) dust = ConfigureDust(dustT.gameObject, spriteRenderer);

            Transform afterT = FindOrCreateChild(visualRoot, "Afterimages");
            AfterimagePool afterimages = GetOrAdd<AfterimagePool>(afterT.gameObject);

            // Componentes novos no root.
            PlayerController controller = GetOrAdd<PlayerController>(root);
            SetRef(controller, "profile", profile);
            SetRef(controller, "visualRoot", visualRoot);

            GetOrAdd<characterMovement>(root); // fachada: mantém o GUID e as chamadas antigas (EnemyBullet)

            PlayerView view = GetOrAdd<PlayerView>(root);
            SetRef(view, "controller", controller);
            SetRef(view, "tuning", tuning);
            SetRef(view, "squashPivot", squashPivot);
            SetRef(view, "sprite", spriteRenderer);

            PlayerAnimatorDriver driver = GetOrAdd<PlayerAnimatorDriver>(root);
            SetRef(driver, "controller", controller);
            SetRef(driver, "animator", animator);

            PlayerFeedback feedback = GetOrAdd<PlayerFeedback>(root);
            SetRef(feedback, "controller", controller);
            SetRef(feedback, "view", view);
            SetRef(feedback, "tuning", tuning);
            SetRef(feedback, "audioCues", cues);
            SetRef(feedback, "dust", dust);
            SetRef(feedback, "afterimages", afterimages);

            ResourceIndicatorView indicator = GetOrAdd<ResourceIndicatorView>(root);
            SetRef(indicator, "controller", controller);
            SetRef(indicator, "tuning", tuning);
            SetRef(indicator, "anchor", spriteT);

            GrappleView grapple = GetOrAdd<GrappleView>(root);
            SetRef(grapple, "controller", controller);
            SetRef(grapple, "firePoint", rootT.Find("FirePoint"));
            SetRef(grapple, "hookTip", rootT.Find("HookTip"));
            Transform ropeT = rootT.Find("RopeVisual");
            SetRef(grapple, "rope", ropeT != null ? ropeT.GetComponent<LineRenderer>() : null);

            SetRef(GetOrAdd<MovementGizmos>(root), "controller", controller);
            SetRef(GetOrAdd<MovementDebugOverlay>(root), "controller", controller);
            SetRef(GetOrAdd<InputRecorder>(root), "controller", controller);

            root.tag = "Player";
            root.layer = 3;

            PrefabUtility.SaveAsPrefabAsset(root, CyborgPrefabPath);
            Debug.Log("[Movement] - Cyborg.prefab convertido no lugar (SPEC §13.2)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static ParticleSystem ConfigureDust(GameObject go, SpriteRenderer reference)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.35f;
        main.startSpeed = 0f;
        main.startSize = 0.09f;
        main.startColor = new Color(0.85f, 0.85f, 0.9f, 0.85f);
        main.gravityModifier = 0.3f;
        main.maxParticles = 256;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (reference != null)
        {
            renderer.sortingLayerID = reference.sortingLayerID;
            renderer.sortingOrder = reference.sortingOrder + 1;
        }

        return ps;
    }

    // ───────────────────────────── 5. DevPlayBootstrap ─────────────────────────────

    [MenuItem("Tools/Movement/Setup/5 DevPlayBootstrap Prefab")]
    public static void EnsureBootstrapPrefab()
    {
        EnsureFolder("Assets/Prefabs/Dev");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
        GameObject go = prefab != null ? PrefabUtility.LoadPrefabContents(BootstrapPrefabPath) : new GameObject("DevPlayBootstrap");
        try
        {
            DevPlayBootstrap bootstrap = GetOrAdd<DevPlayBootstrap>(go);
            SetRef(bootstrap, "playerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(CyborgPrefabPath));
            SetRef(bootstrap, "cameraProfile", AssetDatabase.LoadAssetAtPath<CameraProfile>(CameraProfilePath));
            SetRef(bootstrap, "feedbackTuning", AssetDatabase.LoadAssetAtPath<FeedbackTuning>(TuningPath));
            PrefabUtility.SaveAsPrefabAsset(go, BootstrapPrefabPath);
        }
        finally
        {
            if (prefab != null) PrefabUtility.UnloadPrefabContents(go);
            else Object.DestroyImmediate(go);
        }
    }

    // ───────────────────────────── 7. MainMenu ─────────────────────────────

    [MenuItem("Tools/Movement/Setup/7 MainMenu Camera")]
    public static void ConfigureMainMenuCamera()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
        if (follow == null)
        {
            Debug.LogWarning("[Movement] - MainMenu sem CameraFollow");
            return;
        }

        var so = new SerializedObject(follow);
        so.FindProperty("profile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CameraProfile>(CameraProfilePath);
        so.FindProperty("feedbackTuning").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FeedbackTuning>(TuningPath);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Movement] - MainMenu: CameraFollow com CameraProfile e FeedbackTuning");
    }

    // ───────────────────────────── Utilitários ─────────────────────────────

    public static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    public static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null) return child;
        var go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    public static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogError($"[Movement] - Campo '{field}' não existe em {target.GetType().Name}");
            return;
        }

        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
