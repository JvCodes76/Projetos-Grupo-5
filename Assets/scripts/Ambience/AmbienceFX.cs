using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>Combinações prontas de efeitos (aplicadas por <see cref="AmbienceFX.ApplyPreset"/>).</summary>
public enum AmbiencePreset
{
    Limpo,
    Garoa,
    Chuva,
    Tempestade,
    Neblina,
    Neve,
    Poeira,
    Vagalumes,
    Brasas,
}

/// <summary>
/// Efeitos de ambiência da fase: chuva (com intensidade e trovões), neblina, neve, partículas flutuantes (poeira,
/// vagalumes, brasas) e tom de cor. Basta colocar num GameObject da cena (menu GameObject → Ambiência) e ligar o que
/// quiser no Inspector; tudo é gerado em código, sem depender de prefab, textura ou material do projeto.
///
/// Custo (pensado para não derrubar o framerate):
/// - cada efeito ligado é UM draw call; efeito desligado não cria objeto nenhum;
/// - partículas têm teto fixo (escalado por <see cref="quality"/>), sem colisão, luz, trail nem sub-emissor;
/// - a neblina são no máximo 2 quads com UV rolando (8 vértices), não partículas;
/// - nenhum trabalho por frame aloca memória; texturas (32 px e 128 px) são geradas uma vez e compartilhadas;
/// - os emissores seguem a câmera e só cobrem a área visível.
///
/// Roda depois da câmera (CameraFollow = 50, ParallaxCamera = 60) para não ficar um frame atrasado.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(70)]
[AddComponentMenu("Ambiência/Ambience FX")]
public sealed class AmbienceFX : MonoBehaviour
{
    public enum FogShape
    {
        Uniforme,
        Rasteira,
        Alta,
    }

    public enum MoteStyle
    {
        Poeira,
        Vagalumes,
        Brasas,
    }

    [Serializable]
    public sealed class RainSettings
    {
        public bool enabled;

        [Tooltip("0 = sem chuva, 1 = temporal. Pode ser animado em runtime (RainIntensity / SetRainIntensity).")]
        [Range(0f, 1f)] public float intensity = 0.5f;

        [Tooltip("Inclinação pelo vento: negativo sopra para a esquerda, positivo para a direita.")]
        [Range(-1f, 1f)] public float wind = 0.15f;

        [Tooltip("Velocidade de queda (unidades/s).")]
        [Min(1f)] public float fallSpeed = 24f;

        [Tooltip("Comprimento médio do risco da gota (unidades).")]
        [Min(0.05f)] public float dropLength = 0.9f;

        [Tooltip("Espessura média da gota (unidades).")]
        [Min(0.005f)] public float dropWidth = 0.045f;

        public Color color = new Color(0.78f, 0.86f, 1f, 0.55f);

        [Tooltip("Teto de gotas vivas com intensidade 1 (antes do multiplicador de qualidade).")]
        [Range(50, 2000)] public int maxDrops = 700;

        [Tooltip("Opcional: som de chuva em loop. O volume acompanha a intensidade.")]
        public AudioClip loopClip;

        [Range(0f, 1f)] public float loopVolume = 0.6f;
    }

    [Serializable]
    public sealed class ThunderSettings
    {
        public bool enabled;

        [Tooltip("Só troveja enquanto a chuva estiver caindo.")]
        public bool onlyWhileRaining = true;

        [Tooltip("Intervalo mínimo entre raios (s).")]
        [Min(0.5f)] public float minInterval = 6f;

        [Tooltip("Intervalo máximo entre raios (s).")]
        [Min(0.5f)] public float maxInterval = 16f;

        [Tooltip("Força do clarão na tela. Com a opção de acessibilidade 'reduzir flashes' ligada, vira um pulso fraco e único.")]
        [Range(0f, 1f)] public float flashStrength = 0.55f;

        public Color flashColor = new Color(0.86f, 0.91f, 1f, 1f);

        [Tooltip("Opcional: sons de trovão (um é sorteado por raio).")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 0.8f;

        [Tooltip("Atraso mínimo entre o clarão e o som (s).")]
        [Min(0f)] public float soundDelayMin = 0.15f;

        [Tooltip("Atraso máximo entre o clarão e o som (s).")]
        [Min(0f)] public float soundDelayMax = 1.2f;

        [Tooltip("Chamado a cada raio (ex.: piscar uma Light2D, tremer a câmera).")]
        public UnityEvent onLightning = new UnityEvent();
    }

    [Serializable]
    public sealed class FogSettings
    {
        public bool enabled;

        [Range(0f, 1f)] public float density = 0.45f;

        public Color color = new Color(0.74f, 0.8f, 0.9f, 1f);

        [Tooltip("Uniforme = tela toda; Rasteira = sobe do chão; Alta = desce do topo.")]
        public FogShape shape = FogShape.Uniforme;

        [Tooltip("Fração da altura da tela coberta (Rasteira / Alta). Menos cobertura = menos pixels desenhados.")]
        [Range(0.1f, 1f)] public float coverage = 0.6f;

        [Tooltip("Tamanho do padrão de nuvem no mundo (unidades). Maior = manchas maiores.")]
        [Min(1f)] public float tileSize = 14f;

        [Tooltip("Velocidade de deriva (unidades/s).")]
        public Vector2 scroll = new Vector2(0.6f, 0.05f);

        [Tooltip("0 = colada na tela; 1 = presa no mundo (passa quando a câmera anda).")]
        [Range(0f, 1f)] public float parallax = 0.5f;

        [Tooltip("Segunda camada, maior e mais lenta, para dar profundidade (+1 draw call).")]
        public bool secondLayer = true;
    }

    [Serializable]
    public sealed class SnowSettings
    {
        public bool enabled;

        [Range(0f, 1f)] public float intensity = 0.5f;

        [Range(-1f, 1f)] public float wind = 0.1f;

        [Tooltip("Velocidade de queda (unidades/s).")]
        [Min(0.2f)] public float fallSpeed = 2.2f;

        public Color color = new Color(1f, 1f, 1f, 0.9f);

        [Tooltip("Teto de flocos vivos com intensidade 1 (antes do multiplicador de qualidade).")]
        [Range(20, 1000)] public int maxFlakes = 350;
    }

    [Serializable]
    public sealed class MoteSettings
    {
        public bool enabled;

        public MoteStyle style = MoteStyle.Poeira;

        [Range(0f, 1f)] public float amount = 0.5f;

        [Tooltip("Desligado, usa a cor padrão do estilo.")]
        public bool overrideColor;

        public Color customColor = Color.white;

        [Tooltip("Teto de partículas vivas com quantidade 1 (antes do multiplicador de qualidade).")]
        [Range(10, 400)] public int maxParticles = 120;
    }

    [Serializable]
    public sealed class TintSettings
    {
        public bool enabled;

        [Tooltip("Cor sobre a tela toda; o alfa é a força (ex.: azul escuro para tempestade ou noite).")]
        public Color color = new Color(0.04f, 0.06f, 0.12f, 0.3f);
    }

    // ───────────────────────────── Configuração ─────────────────────────────

    [Header("Geral")]
    [Tooltip("Câmera que os efeitos seguem. Preenchida sozinha com a Main Camera da cena; se ela sumir em runtime " +
             "(ex.: a câmera persistente do menu assume), o componente troca para a câmera ativa por conta própria.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Multiplica a quantidade de partículas de todos os efeitos (0,25 = máquinas fracas).")]
    [Range(0.25f, 1f)] [SerializeField] private float quality = 1f;

    [Tooltip("Sorting layer onde os efeitos são desenhados.")]
    [SerializeField] private string sortingLayer = "Foreground";

    [Tooltip("Order in layer base (cada efeito soma um pequeno deslocamento).")]
    [SerializeField] private int sortingOrder;

    [Tooltip("Distância à frente da câmera onde os efeitos ficam (câmera em z = -10 ⇒ efeitos em z = 0).")]
    [Min(0.1f)] [SerializeField] private float depthFromCamera = 10f;

    [Tooltip("Sobra além da borda da tela (unidades), para a câmera andar sem mostrar o limite do efeito.")]
    [Min(0f)] [SerializeField] private float margin = 1.5f;

    [Tooltip("Tempo (s) para um efeito aparecer ou sumir ao ser ligado, desligado ou ter a intensidade trocada.")]
    [Min(0f)] [SerializeField] private float fadeSeconds = 1.5f;

    [Tooltip("Opcional: material base (precisa aceitar cor de vértice e textura principal). Vazio = Sprites/Default.")]
    [SerializeField] private Material materialOverride;

    [Tooltip("Player de áudio dos sons de chuva e trovão. Preenchido sozinho com o AudioSource deste objeto (criado " +
             "junto com o componente). O volume e o Output dele valem como volume geral e mixer dos sons de ambiência.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Chuva")]
    [SerializeField] private RainSettings rain = new RainSettings();

    [Header("Trovões (clarão + som)")]
    [SerializeField] private ThunderSettings thunder = new ThunderSettings();

    [Header("Neblina")]
    [SerializeField] private FogSettings fog = new FogSettings();

    [Header("Neve")]
    [SerializeField] private SnowSettings snow = new SnowSettings();

    [Header("Partículas flutuantes")]
    [SerializeField] private MoteSettings motes = new MoteSettings();

    [Header("Tom de ambiente")]
    [SerializeField] private TintSettings tint = new TintSettings();

    // ───────────────────────────── Estado em runtime ─────────────────────────────

    private const string RootName = "~AmbienceFX";
    private const float FlashDuration = 0.7f;

    // Deslocamentos de ordem dentro da sorting layer (de trás para a frente).
    private const int OrderTint = 0;
    private const int OrderFogB = 1;
    private const int OrderFogA = 2;
    private const int OrderMotes = 3;
    private const int OrderSnow = 4;
    private const int OrderRain = 5;
    private const int OrderFlash = 6;

    private sealed class Emitter
    {
        public ParticleSystem ps;
        public ParticleSystemRenderer renderer;
        public int orderOffset;
        public float level;       // intensidade atual (com fade), 0..1
        public float baseRate;    // emissão por segundo com level = 1
        public float appliedRate = -1f;
        public float prewarm;     // segundos simulados para já nascer "cheio"
        public bool needsPrewarm;
    }

    private sealed class ScreenQuad
    {
        public MeshRenderer renderer;
        public Mesh mesh;
        public int orderOffset;
        public float xMin, yMin, xMax, yMax;
        public readonly Vector3[] verts = new Vector3[4];
        public readonly Vector2[] uvs = new Vector2[4];
        public readonly Color32[] colors = new Color32[4];

        // Vértices: 0 = baixo-esq, 1 = cima-esq, 2 = cima-dir, 3 = baixo-dir.
        public void SetRect(float x0, float y0, float x1, float y1)
        {
            xMin = x0; yMin = y0; xMax = x1; yMax = y1;
            verts[0] = new Vector3(x0, y0, 0f);
            verts[1] = new Vector3(x0, y1, 0f);
            verts[2] = new Vector3(x1, y1, 0f);
            verts[3] = new Vector3(x1, y0, 0f);
            mesh.vertices = verts;
            mesh.RecalculateBounds();
        }

        public void SetUV(float u0, float v0, float u1, float v1)
        {
            uvs[0] = new Vector2(u0, v0);
            uvs[1] = new Vector2(u0, v1);
            uvs[2] = new Vector2(u1, v1);
            uvs[3] = new Vector2(u1, v0);
            mesh.uv = uvs;
        }

        public void SetColors(Color32 bottom, Color32 top)
        {
            colors[0] = bottom;
            colors[1] = top;
            colors[2] = top;
            colors[3] = bottom;
            mesh.colors32 = colors;
        }
    }

    private Transform root;
    private Camera cachedCamera;
    private float nextCameraSearch;
    private bool built;
    private bool settingsDirty = true;
    private bool firstTick;
    private bool warnedShader;
    private float viewW;
    private float viewH;
    private Vector3 lastCamPos;

    private Material particleMaterial;
    private Material fogMaterial;
    private Material flatMaterial;

    private readonly Emitter rainEmitter = new Emitter();
    private readonly Emitter snowEmitter = new Emitter();
    private readonly Emitter moteEmitter = new Emitter();
    private Emitter[] emitters;

    private ScreenQuad fogA;
    private ScreenQuad fogB;
    private ScreenQuad flashQuad;
    private ScreenQuad tintQuad;
    private float fogLevel;
    private float appliedFogLevel = -1f;
    private float tintLevel;
    private float appliedTintLevel = -1f;
    private Vector2 fogScrollA;
    private Vector2 fogScrollB;

    private float strikeTimer;
    private float flashTime = -1f;
    private float flashPeak;
    private bool flashReduced;
    private bool soundPending;
    private float soundTimer;
    private AudioSource loopSource;
    private AudioSource oneShotSource;

    // Texturas e gradientes compartilhados por todas as instâncias (gerados uma vez).
    private static Texture2D softDotTexture;
    private static Texture2D fogTexture;
    private static Gradient fadeGradient;
    private static Gradient blinkGradient;

    // ───────────────────────────── API pública ─────────────────────────────

    /// <summary>Intensidade-alvo da chuva (0..1). A mudança é suavizada por <see cref="fadeSeconds"/>.</summary>
    public float RainIntensity
    {
        get => rain.intensity;
        set => rain.intensity = Mathf.Clamp01(value);
    }

    /// <summary>Intensidade da chuva agora, já com o fade (0 = nenhuma gota sendo emitida).</summary>
    public float CurrentRainLevel => rainEmitter.level;

    public bool RainEnabled
    {
        get => rain.enabled;
        set => rain.enabled = value;
    }

    public bool ThunderEnabled
    {
        get => thunder.enabled;
        set => thunder.enabled = value;
    }

    public float FogDensity
    {
        get => fog.density;
        set => fog.density = Mathf.Clamp01(value);
    }

    public bool FogEnabled
    {
        get => fog.enabled;
        set => fog.enabled = value;
    }

    public float SnowIntensity
    {
        get => snow.intensity;
        set => snow.intensity = Mathf.Clamp01(value);
    }

    public bool SnowEnabled
    {
        get => snow.enabled;
        set => snow.enabled = value;
    }

    public bool MotesEnabled
    {
        get => motes.enabled;
        set => motes.enabled = value;
    }

    public bool TintEnabled
    {
        get => tint.enabled;
        set => tint.enabled = value;
    }

    /// <summary>Multiplicador de partículas (0,25..1); pode vir de um menu de opções gráficas.</summary>
    public float Quality
    {
        get => quality;
        set
        {
            quality = Mathf.Clamp(value, 0.25f, 1f);
            settingsDirty = true;
        }
    }

    /// <summary>Partículas vivas somando todos os efeitos (leitura para o Inspector e para profiling).</summary>
    public int ActiveParticleCount
    {
        get
        {
            if (emitters == null) return 0;
            int total = 0;
            for (int i = 0; i < emitters.Length; i++)
            {
                if (emitters[i].ps != null) total += emitters[i].ps.particleCount;
            }

            return total;
        }
    }

    /// <summary>Liga a chuva e define a intensidade; <paramref name="instant"/> pula o fade.</summary>
    public void SetRainIntensity(float value, bool instant = false)
    {
        rain.enabled = true;
        rain.intensity = Mathf.Clamp01(value);
        if (instant) rainEmitter.level = rain.intensity;
    }

    /// <summary>Dispara um raio agora (clarão, som e evento), mesmo com os trovões automáticos desligados.</summary>
    public void TriggerLightning()
    {
        if (!built) return;

        strikeTimer = NextStrikeDelay();
        flashReduced = Application.isPlaying && FeedbackSettings.ReduceFlashes;
        flashPeak = thunder.flashStrength * (flashReduced ? 0.3f : 1f);
        flashTime = 0f;

        if (flashQuad == null)
        {
            flashQuad = CreateQuad("Flash", FlatMaterial(), OrderFlash);
            LayoutFullScreen(flashQuad);
        }

        if (!Application.isPlaying) return;

        if (thunder.clips != null && thunder.clips.Length > 0)
        {
            soundPending = true;
            soundTimer = UnityEngine.Random.Range(thunder.soundDelayMin, Mathf.Max(thunder.soundDelayMin, thunder.soundDelayMax));
        }

        thunder.onLightning?.Invoke();
    }

    /// <summary>Avisa que algum campo mudou por código (o Inspector já avisa sozinho).</summary>
    public void MarkDirty()
    {
        settingsDirty = true;
    }

    /// <summary>Troca tudo por uma combinação pronta. Os valores podem ser ajustados depois.</summary>
    public void ApplyPreset(AmbiencePreset preset)
    {
        rain.enabled = false;
        thunder.enabled = false;
        fog.enabled = false;
        snow.enabled = false;
        motes.enabled = false;
        tint.enabled = false;

        switch (preset)
        {
            case AmbiencePreset.Garoa:
                rain.enabled = true;
                rain.intensity = 0.2f;
                rain.wind = 0.05f;
                rain.fallSpeed = 18f;
                rain.dropLength = 0.55f;
                break;

            case AmbiencePreset.Chuva:
                rain.enabled = true;
                rain.intensity = 0.55f;
                rain.wind = 0.15f;
                rain.fallSpeed = 24f;
                rain.dropLength = 0.9f;
                break;

            case AmbiencePreset.Tempestade:
                rain.enabled = true;
                rain.intensity = 1f;
                rain.wind = 0.45f;
                rain.fallSpeed = 30f;
                rain.dropLength = 1.2f;
                thunder.enabled = true;
                thunder.minInterval = 4f;
                thunder.maxInterval = 12f;
                fog.enabled = true;
                fog.shape = FogShape.Uniforme;
                fog.density = 0.22f;
                fog.scroll = new Vector2(1.6f, 0.1f);
                tint.enabled = true;
                tint.color = new Color(0.04f, 0.06f, 0.12f, 0.3f);
                break;

            case AmbiencePreset.Neblina:
                fog.enabled = true;
                fog.shape = FogShape.Rasteira;
                fog.coverage = 0.65f;
                fog.density = 0.55f;
                fog.scroll = new Vector2(0.6f, 0.05f);
                break;

            case AmbiencePreset.Neve:
                snow.enabled = true;
                snow.intensity = 0.6f;
                snow.wind = 0.1f;
                fog.enabled = true;
                fog.shape = FogShape.Uniforme;
                fog.density = 0.15f;
                fog.scroll = new Vector2(0.3f, 0.02f);
                break;

            case AmbiencePreset.Poeira:
                motes.enabled = true;
                motes.style = MoteStyle.Poeira;
                motes.amount = 0.5f;
                motes.overrideColor = false;
                break;

            case AmbiencePreset.Vagalumes:
                motes.enabled = true;
                motes.style = MoteStyle.Vagalumes;
                motes.amount = 0.35f;
                motes.overrideColor = false;
                tint.enabled = true;
                tint.color = new Color(0.02f, 0.04f, 0.1f, 0.25f);
                break;

            case AmbiencePreset.Brasas:
                motes.enabled = true;
                motes.style = MoteStyle.Brasas;
                motes.amount = 0.5f;
                motes.overrideColor = false;
                tint.enabled = true;
                tint.color = new Color(0.25f, 0.08f, 0.02f, 0.15f);
                break;
        }

        settingsDirty = true;
    }

    // ───────────────────────────── Ciclo de vida ─────────────────────────────

    private void OnEnable()
    {
        if (Application.isPlaying) Build();
    }

    private void OnDisable()
    {
        Teardown();
    }

    private void OnValidate()
    {
        if (thunder.maxInterval < thunder.minInterval) thunder.maxInterval = thunder.minInterval;
        if (thunder.soundDelayMax < thunder.soundDelayMin) thunder.soundDelayMax = thunder.soundDelayMin;
        settingsDirty = true;
#if UNITY_EDITOR
        AutoFillReferences();
#endif
    }

#if UNITY_EDITOR
    // Chamado pela Unity ao adicionar o componente (ou no "Reset" do menu de contexto): já sai com tudo ligado.
    private void Reset()
    {
        AutoFillReferences();
        if (audioSource != null || !gameObject.scene.IsValid()) return;

        audioSource = UnityEditor.Undo.AddComponent<AudioSource>(gameObject);
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    // Preenche o que estiver vazio com o que já existe: o AudioSource deste objeto e a Main Camera da mesma cena.
    // Fora de cena (asset de prefab) não faz nada, para não gravar referência de cena dentro do prefab.
    private void AutoFillReferences()
    {
        if (Application.isPlaying || !gameObject.scene.IsValid()) return;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (targetCamera == null)
        {
            Camera main = Camera.main;
            if (main != null && main.gameObject.scene == gameObject.scene) targetCamera = main;
        }
    }
#endif

    private void LateUpdate()
    {
        if (built) Tick(Time.deltaTime);
    }

    private void Build()
    {
        if (built) return;

        // Fora da hierarquia da cena e nunca salvo: não suja a cena nem herda a escala deste objeto.
        var go = new GameObject(RootName) { hideFlags = HideFlags.HideAndDontSave };
        root = go.transform;
        emitters = new[] { rainEmitter, snowEmitter, moteEmitter };
        built = true;
        settingsDirty = true;
        firstTick = true;
        strikeTimer = NextStrikeDelay();
    }

    private void Teardown()
    {
        if (!built) return;
        built = false;

        Kill(fogA != null ? fogA.mesh : null);
        Kill(fogB != null ? fogB.mesh : null);
        Kill(flashQuad != null ? flashQuad.mesh : null);
        Kill(tintQuad != null ? tintQuad.mesh : null);
        fogA = fogB = flashQuad = tintQuad = null;

        if (root != null) Kill(root.gameObject);
        root = null;

        Kill(particleMaterial);
        Kill(fogMaterial);
        Kill(flatMaterial);
        particleMaterial = fogMaterial = flatMaterial = null;

        for (int i = 0; i < emitters.Length; i++)
        {
            Emitter e = emitters[i];
            e.ps = null;
            e.renderer = null;
            e.level = 0f;
            e.appliedRate = -1f;
            e.needsPrewarm = false;
        }

        loopSource = null;
        oneShotSource = null;
        fogLevel = 0f;
        tintLevel = 0f;
        appliedFogLevel = -1f;
        appliedTintLevel = -1f;
        flashTime = -1f;
        soundPending = false;
    }

    private static void Kill(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    // ───────────────────────────── Atualização por frame ─────────────────────────────

    private void Tick(float dt)
    {
        Camera cam = ResolveCamera();
        if (cam == null || root == null) return;

        Transform camTransform = cam.transform;
        Vector3 camPos = camTransform.position;
        root.SetPositionAndRotation(camPos + camTransform.forward * depthFromCamera, camTransform.rotation);

        UpdateViewSize(cam);

        // Corte de câmera (respawn, troca de alvo): as partículas ficaram para trás; nasce tudo de novo já cheio.
        bool instant = firstTick;
        bool teleported = !firstTick && (camPos - lastCamPos).sqrMagnitude > viewW * viewW * 0.25f;
        lastCamPos = camPos;
        firstTick = false;

        if (settingsDirty)
        {
            settingsDirty = false;
            ApplySettings();
        }

        UpdateRain(dt, teleported, instant);
        UpdateSnow(dt, teleported, instant);
        UpdateMotes(dt, teleported, instant);
        UpdateFog(dt, camPos, instant);
        UpdateTint(dt, instant);
        UpdateThunder(dt);
    }

    private Camera ResolveCamera()
    {
        // A do Inspector vale enquanto existir e estiver ligada. Neste projeto a câmera da fase se destrói quando a
        // câmera persistente do menu já existe: aí a referência "morre" e a busca automática assume.
        if (IsUsable(targetCamera)) return targetCamera;
        if (IsUsable(cachedCamera)) return cachedCamera;

        // Sem câmera nenhuma, procura de novo só 2×/s em vez de todo frame.
        float now = Time.realtimeSinceStartup;
        if (now < nextCameraSearch) return null;
        nextCameraSearch = now + 0.5f;

        cachedCamera = Camera.main;
        if (cachedCamera == null && Camera.allCamerasCount > 0) cachedCamera = Camera.allCameras[0];
        return cachedCamera;
    }

    private static bool IsUsable(Camera cam)
    {
        return cam != null && cam.isActiveAndEnabled;
    }

    // Player de áudio: o do Inspector; senão o AudioSource deste objeto; senão um criado na hora.
    private AudioSource ResolveAudioPlayer()
    {
        if (audioSource != null) return audioSource;

        if (oneShotSource == null && !TryGetComponent(out oneShotSource))
        {
            oneShotSource = CreateAudioSource("Thunder", false, null);
        }

        return oneShotSource;
    }

    private void UpdateViewSize(Camera cam)
    {
        float h = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * depthFromCamera * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * cam.aspect;

        // Tolerância de 2%: zoom suave da câmera não refaz os emissores a cada frame (a margem cobre a diferença).
        if (Mathf.Abs(w - viewW) > viewW * 0.02f || Mathf.Abs(h - viewH) > viewH * 0.02f)
        {
            viewW = w;
            viewH = h;
            settingsDirty = true;
        }
    }

    private void ApplySettings()
    {
        for (int i = 0; i < emitters.Length; i++)
        {
            Emitter e = emitters[i];
            if (e.renderer == null) continue;
            e.renderer.sortingLayerName = sortingLayer;
            e.renderer.sortingOrder = sortingOrder + e.orderOffset;
        }

        ApplySorting(fogA);
        ApplySorting(fogB);
        ApplySorting(flashQuad);
        ApplySorting(tintQuad);

        if (rainEmitter.ps != null) ConfigureRain();
        if (snowEmitter.ps != null) ConfigureSnow();
        if (moteEmitter.ps != null) ConfigureMotes();

        if (fogA != null) LayoutFog(fogA);
        if (fogB != null) LayoutFog(fogB);
        if (flashQuad != null) LayoutFullScreen(flashQuad);
        if (tintQuad != null) LayoutFullScreen(tintQuad);

        appliedFogLevel = -1f;
        appliedTintLevel = -1f;
    }

    private void ApplySorting(ScreenQuad quad)
    {
        if (quad == null) return;
        quad.renderer.sortingLayerName = sortingLayer;
        quad.renderer.sortingOrder = sortingOrder + quad.orderOffset;
    }

    private float Fade(float current, float target, float dt, bool instant)
    {
        if (instant || fadeSeconds <= 0f) return target;
        return Mathf.MoveTowards(current, target, dt / fadeSeconds);
    }

    private void StepEmitter(Emitter e, float target, float dt, bool teleported, bool instant)
    {
        if (e.ps == null) return;

        e.level = Fade(e.level, target, dt, instant);
        if (e.level <= 0.0005f)
        {
            // As partículas vivas terminam o caminho; só a emissão para.
            if (e.ps.isEmitting) e.ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            return;
        }

        float rate = e.baseRate * e.level;
        if (Mathf.Abs(rate - e.appliedRate) > 0.01f * Mathf.Max(1f, rate))
        {
            ParticleSystem.EmissionModule emission = e.ps.emission;
            emission.rateOverTime = rate;
            e.appliedRate = rate;
        }

        if (teleported || e.needsPrewarm)
        {
            e.needsPrewarm = false;
            e.ps.Simulate(e.prewarm, false, true);
            e.ps.Play(false);
        }
        else if (!e.ps.isEmitting)
        {
            e.ps.Play(false);
        }
    }

    // ───────────────────────────── Chuva ─────────────────────────────

    private void UpdateRain(float dt, bool teleported, bool instant)
    {
        float target = rain.enabled ? rain.intensity : 0f;
        if (target > 0f && rainEmitter.ps == null)
        {
            CreateEmitter(rainEmitter, "Rain", OrderRain, instant);
            ConfigureRain();
        }

        StepEmitter(rainEmitter, target, dt, teleported, instant);
        UpdateRainLoop();
    }

    private void ConfigureRain()
    {
        Emitter e = rainEmitter;
        float w = viewW + margin * 2f;
        float h = viewH + margin * 2f;
        float speed = Mathf.Max(1f, rain.fallSpeed);
        float slowest = speed * 0.85f;
        float windX = rain.wind * speed * 0.6f;
        float life = h / slowest;
        int count = Mathf.CeilToInt(rain.maxDrops * quality);
        float width = Mathf.Max(0.005f, rain.dropWidth);

        Color near = rain.color;
        Color far = near;
        far.a *= 0.45f;

        ParticleSystem.MainModule main = e.ps.main;
        main.startLifetime = life;
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.7f, width * 1.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(far, near);
        main.maxParticles = count + 16;

        // Linha no topo da tela, alargada e deslocada contra o vento para a chuva inclinada cobrir a tela inteira.
        ParticleSystem.ShapeModule shape = e.ps.shape;
        shape.scale = new Vector3(w + Mathf.Abs(windX) * life, 0.05f, 0.05f);
        shape.position = new Vector3(-windX * life * 0.5f, h * 0.5f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = e.ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windX * 0.95f, windX * 1.05f);
        velocity.y = new ParticleSystem.MinMaxCurve(-speed * 1.15f, -slowest);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Gota = ponto macio esticado na direção da velocidade.
        e.renderer.renderMode = ParticleSystemRenderMode.Stretch;
        e.renderer.lengthScale = rain.dropLength / width;
        e.renderer.velocityScale = 0f;
        e.renderer.cameraVelocityScale = 0f;

        e.baseRate = count / life;
        e.prewarm = life;
    }

    private void UpdateRainLoop()
    {
        float volume = Application.isPlaying && rain.loopClip != null ? rainEmitter.level * rain.loopVolume : 0f;
        if (volume <= 0.001f)
        {
            if (loopSource != null && loopSource.isPlaying) loopSource.Stop();
            return;
        }

        // O loop toca numa fonte própria (o volume dela acompanha a chuva), mas com o mixer e o volume geral do player.
        AudioSource player = ResolveAudioPlayer();
        if (loopSource == null) loopSource = CreateAudioSource("RainLoop", true, player);
        if (loopSource.clip != rain.loopClip) loopSource.clip = rain.loopClip;
        loopSource.volume = volume * player.volume;
        if (!loopSource.isPlaying) loopSource.Play();
    }

    // ───────────────────────────── Trovões ─────────────────────────────

    private void UpdateThunder(float dt)
    {
        if (thunder.enabled && (!thunder.onlyWhileRaining || rainEmitter.level > 0.05f))
        {
            strikeTimer -= dt;
            if (strikeTimer <= 0f) TriggerLightning();
        }

        if (flashTime >= 0f && flashQuad != null)
        {
            flashTime += dt;
            if (flashTime >= FlashDuration)
            {
                flashTime = -1f;
                flashQuad.renderer.enabled = false;
            }
            else
            {
                Color c = thunder.flashColor;
                c.a *= flashPeak * FlashEnvelope(flashTime, flashReduced);
                Color32 c32 = c;
                flashQuad.SetColors(c32, c32);
                flashQuad.renderer.enabled = true;
            }
        }

        if (soundPending)
        {
            soundTimer -= dt;
            if (soundTimer <= 0f)
            {
                soundPending = false;
                PlayThunderSound();
            }
        }
    }

    // Clarão forte, queda rápida e um segundo pulso (o "pisca" do raio). Reduzido: um pulso único e lento.
    private static float FlashEnvelope(float t, bool reduced)
    {
        if (reduced) return Mathf.Exp(-t * 4f);

        float a = Mathf.Exp(-t * 9f);
        if (t > 0.11f) a = Mathf.Max(a, 0.75f * Mathf.Exp(-(t - 0.11f) * 7f));
        return a;
    }

    private float NextStrikeDelay()
    {
        return UnityEngine.Random.Range(thunder.minInterval, Mathf.Max(thunder.minInterval, thunder.maxInterval));
    }

    private void PlayThunderSound()
    {
        AudioClip[] clips = thunder.clips;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip == null) return;

        ResolveAudioPlayer().PlayOneShot(clip, thunder.volume);
    }

    // ───────────────────────────── Neve ─────────────────────────────

    private void UpdateSnow(float dt, bool teleported, bool instant)
    {
        float target = snow.enabled ? snow.intensity : 0f;
        if (target > 0f && snowEmitter.ps == null)
        {
            CreateEmitter(snowEmitter, "Snow", OrderSnow, instant);
            ConfigureSnow();
        }

        StepEmitter(snowEmitter, target, dt, teleported, instant);
    }

    private void ConfigureSnow()
    {
        Emitter e = snowEmitter;
        float w = viewW + margin * 2f;
        float h = viewH + margin * 2f;
        float speed = Mathf.Max(0.2f, snow.fallSpeed);
        float slowest = speed * 0.7f;
        float windX = snow.wind * 3f;
        float life = h / slowest;
        int count = Mathf.CeilToInt(snow.maxFlakes * quality);

        Color near = snow.color;
        Color far = near;
        far.a *= 0.5f;

        ParticleSystem.MainModule main = e.ps.main;
        main.startLifetime = life;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(far, near);
        main.maxParticles = count + 16;

        ParticleSystem.ShapeModule shape = e.ps.shape;
        shape.scale = new Vector3(w + Mathf.Abs(windX) * life, 0.05f, 0.05f);
        shape.position = new Vector3(-windX * life * 0.5f, h * 0.5f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = e.ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(windX - 0.3f, windX + 0.3f);
        velocity.y = new ParticleSystem.MinMaxCurve(-speed * 1.3f, -slowest);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Balanço lateral: ruído de 1 oitava na qualidade mais barata, só no eixo X.
        SetNoise(e.ps, 0.6f, 0.35f, true);

        e.renderer.renderMode = ParticleSystemRenderMode.Billboard;
        e.baseRate = count / life;
        e.prewarm = life;
    }

    // ───────────────────────────── Partículas flutuantes ─────────────────────────────

    private void UpdateMotes(float dt, bool teleported, bool instant)
    {
        float target = motes.enabled ? motes.amount : 0f;
        if (target > 0f && moteEmitter.ps == null)
        {
            CreateEmitter(moteEmitter, "Motes", OrderMotes, instant);
            ConfigureMotes();
        }

        StepEmitter(moteEmitter, target, dt, teleported, instant);
    }

    private void ConfigureMotes()
    {
        Emitter e = moteEmitter;
        float w = viewW + margin * 2f;
        float h = viewH + margin * 2f;
        int count = Mathf.CeilToInt(motes.maxParticles * quality);

        float lifeMin, lifeMax, sizeMin, sizeMax, driftX, riseMin, riseMax, noiseStrength;
        bool fromBottom = false;
        bool blink = false;
        Color colorA, colorB;

        switch (motes.style)
        {
            case MoteStyle.Vagalumes:
                lifeMin = 4f; lifeMax = 7f;
                sizeMin = 0.06f; sizeMax = 0.12f;
                driftX = 0.3f; riseMin = -0.2f; riseMax = 0.2f;
                noiseStrength = 0.5f;
                blink = true;
                colorA = new Color(0.78f, 1f, 0.42f, 0.95f);
                colorB = new Color(0.95f, 1f, 0.55f, 0.95f);
                break;

            case MoteStyle.Brasas:
                lifeMin = 3f; lifeMax = 6f;
                sizeMin = 0.03f; sizeMax = 0.07f;
                driftX = 0.4f; riseMin = 0.8f; riseMax = 2.2f;
                noiseStrength = 0.45f;
                fromBottom = true;
                colorA = new Color(1f, 0.45f, 0.12f, 0.95f);
                colorB = new Color(1f, 0.8f, 0.3f, 0.95f);
                break;

            default: // Poeira
                lifeMin = 5f; lifeMax = 9f;
                sizeMin = 0.03f; sizeMax = 0.08f;
                driftX = 0.15f; riseMin = -0.05f; riseMax = 0.1f;
                noiseStrength = 0.12f;
                colorA = new Color(1f, 0.96f, 0.86f, 0.25f);
                colorB = new Color(1f, 0.96f, 0.86f, 0.45f);
                break;
        }

        if (motes.overrideColor)
        {
            colorA = motes.customColor;
            colorB = motes.customColor;
            colorA.a *= 0.6f;
        }

        ParticleSystem.MainModule main = e.ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.maxParticles = count + 16;

        // Poeira e vagalumes nascem na tela toda (o fade esconde o aparecimento); brasas sobem da borda de baixo.
        ParticleSystem.ShapeModule shape = e.ps.shape;
        if (fromBottom)
        {
            shape.scale = new Vector3(w, 0.2f, 0.05f);
            shape.position = new Vector3(0f, -h * 0.5f, 0f);
        }
        else
        {
            shape.scale = new Vector3(w, h, 0.05f);
            shape.position = Vector3.zero;
        }

        ParticleSystem.VelocityOverLifetimeModule velocity = e.ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-driftX, driftX);
        velocity.y = new ParticleSystem.MinMaxCurve(riseMin, riseMax);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        SetNoise(e.ps, noiseStrength, 0.4f, false);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = e.ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(blink ? BlinkGradient() : FadeGradient());

        e.renderer.renderMode = ParticleSystemRenderMode.Billboard;
        e.baseRate = count / ((lifeMin + lifeMax) * 0.5f);
        e.prewarm = lifeMax;
    }

    private static void SetNoise(ParticleSystem ps, float strength, float frequency, bool horizontalOnly)
    {
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = strength > 0f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.octaveCount = 1;
        noise.frequency = frequency;
        noise.scrollSpeed = 0.25f;
        noise.damping = true;
        noise.separateAxes = true;
        noise.strengthX = strength;
        noise.strengthY = horizontalOnly ? 0f : strength;
        noise.strengthZ = 0f;
    }

    // ───────────────────────────── Neblina ─────────────────────────────

    private void UpdateFog(float dt, Vector3 camPos, bool instant)
    {
        float target = fog.enabled ? fog.density : 0f;
        fogLevel = Fade(fogLevel, target, dt, instant);

        if (fogLevel <= 0.0005f)
        {
            if (fogA != null) fogA.renderer.enabled = false;
            if (fogB != null) fogB.renderer.enabled = false;
            appliedFogLevel = -1f;
            return;
        }

        if (fogA == null)
        {
            fogA = CreateQuad("FogA", FogMaterial(), OrderFogA);
            LayoutFog(fogA);
        }

        if (fog.secondLayer && fogB == null)
        {
            fogB = CreateQuad("FogB", FogMaterial(), OrderFogB);
            LayoutFog(fogB);
        }

        fogA.renderer.enabled = true;
        if (fogB != null) fogB.renderer.enabled = fog.secondLayer;

        if (Mathf.Abs(fogLevel - appliedFogLevel) > 0.002f)
        {
            appliedFogLevel = fogLevel;
            ColorFog(fogA, fogLevel);
            if (fogB != null) ColorFog(fogB, fogLevel * 0.6f);
        }

        // UV rolando: deriva do vento + parallax da câmera. Tudo mantido em [0, 1) para não perder precisão.
        float tileA = Mathf.Max(1f, fog.tileSize);
        fogScrollA.x = Mathf.Repeat(fogScrollA.x + fog.scroll.x * dt / tileA, 1f);
        fogScrollA.y = Mathf.Repeat(fogScrollA.y + fog.scroll.y * dt / tileA, 1f);
        ScrollFog(fogA, camPos, tileA, fog.parallax, fogScrollA);

        if (fogB != null && fog.secondLayer)
        {
            float tileB = tileA * 1.7f;
            fogScrollB.x = Mathf.Repeat(fogScrollB.x - fog.scroll.x * 0.6f * dt / tileB, 1f);
            fogScrollB.y = Mathf.Repeat(fogScrollB.y + fog.scroll.y * 0.8f * dt / tileB, 1f);
            ScrollFog(fogB, camPos, tileB, fog.parallax * 0.6f, fogScrollB + new Vector2(0.37f, 0.61f));
        }
    }

    private void LayoutFog(ScreenQuad quad)
    {
        float halfW = viewW * 0.5f + margin;
        float halfH = viewH * 0.5f + margin;
        float covered = halfH * 2f * fog.coverage;

        switch (fog.shape)
        {
            case FogShape.Rasteira:
                quad.SetRect(-halfW, -halfH, halfW, -halfH + covered);
                break;
            case FogShape.Alta:
                quad.SetRect(-halfW, halfH - covered, halfW, halfH);
                break;
            default:
                quad.SetRect(-halfW, -halfH, halfW, halfH);
                break;
        }
    }

    private void ColorFog(ScreenQuad quad, float level)
    {
        Color solid = fog.color;
        solid.a *= level;
        Color clear = solid;
        clear.a = 0f;

        switch (fog.shape)
        {
            case FogShape.Rasteira:
                quad.SetColors(solid, clear);
                break;
            case FogShape.Alta:
                quad.SetColors(clear, solid);
                break;
            default:
                quad.SetColors(solid, solid);
                break;
        }
    }

    private static void ScrollFog(ScreenQuad quad, Vector3 camPos, float tile, float parallax, Vector2 scroll)
    {
        float u = Mathf.Repeat(camPos.x * parallax / tile, 1f) + scroll.x;
        float v = Mathf.Repeat(camPos.y * parallax / tile, 1f) + scroll.y;
        quad.SetUV(u + quad.xMin / tile, v + quad.yMin / tile, u + quad.xMax / tile, v + quad.yMax / tile);
    }

    // ───────────────────────────── Tom de ambiente ─────────────────────────────

    private void UpdateTint(float dt, bool instant)
    {
        tintLevel = Fade(tintLevel, tint.enabled ? 1f : 0f, dt, instant);

        if (tintLevel <= 0.0005f)
        {
            if (tintQuad != null) tintQuad.renderer.enabled = false;
            appliedTintLevel = -1f;
            return;
        }

        if (tintQuad == null)
        {
            tintQuad = CreateQuad("Tint", FlatMaterial(), OrderTint);
            LayoutFullScreen(tintQuad);
        }

        tintQuad.renderer.enabled = true;
        if (Mathf.Abs(tintLevel - appliedTintLevel) > 0.002f)
        {
            appliedTintLevel = tintLevel;
            Color c = tint.color;
            c.a *= tintLevel;
            Color32 c32 = c;
            tintQuad.SetColors(c32, c32);
        }
    }

    private void LayoutFullScreen(ScreenQuad quad)
    {
        float halfW = viewW * 0.5f + margin;
        float halfH = viewH * 0.5f + margin;
        quad.SetRect(-halfW, -halfH, halfW, halfH);
        quad.SetUV(0f, 0f, 1f, 1f);
    }

    // ───────────────────────────── Criação de objetos ─────────────────────────────

    private GameObject NewChild(string childName)
    {
        var go = new GameObject(childName) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        go.transform.SetParent(root, false);
        return go;
    }

    private void CreateEmitter(Emitter e, string emitterName, int orderOffset, bool prewarmed)
    {
        GameObject go = NewChild(emitterName);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // a câmera anda e as partículas ficam no mundo
        main.startSpeed = 0f;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.sharedMaterial = ParticleMaterial();
        psRenderer.shadowCastingMode = ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;
        psRenderer.lightProbeUsage = LightProbeUsage.Off;
        psRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        psRenderer.sortingLayerName = sortingLayer;
        psRenderer.sortingOrder = sortingOrder + orderOffset;

        e.ps = ps;
        e.renderer = psRenderer;
        e.orderOffset = orderOffset;
        e.level = 0f;
        e.appliedRate = -1f;
        e.needsPrewarm = prewarmed; // efeito ligado desde o início da fase já nasce cheio; ligado depois, entra em fade
    }

    private ScreenQuad CreateQuad(string quadName, Material material, int orderOffset)
    {
        GameObject go = NewChild(quadName);
        var quad = new ScreenQuad { orderOffset = orderOffset };

        var mesh = new Mesh { name = quadName, hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        mesh.vertices = quad.verts;
        mesh.uv = quad.uvs;
        mesh.colors32 = quad.colors;
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quad.mesh = mesh;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.sortingLayerName = sortingLayer;
        meshRenderer.sortingOrder = sortingOrder + orderOffset;
        quad.renderer = meshRenderer;
        return quad;
    }

    private AudioSource CreateAudioSource(string sourceName, bool loop, AudioSource template)
    {
        var source = NewChild(sourceName).AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        if (template != null) source.outputAudioMixerGroup = template.outputAudioMixerGroup;
        return source;
    }

    // ───────────────────────────── Materiais e texturas ─────────────────────────────

    private Material ParticleMaterial()
    {
        if (particleMaterial == null) particleMaterial = CreateMaterial(SoftDotTexture());
        return particleMaterial;
    }

    private Material FogMaterial()
    {
        if (fogMaterial == null) fogMaterial = CreateMaterial(FogTexture());
        return fogMaterial;
    }

    private Material FlatMaterial()
    {
        if (flatMaterial == null) flatMaterial = CreateMaterial(Texture2D.whiteTexture);
        return flatMaterial;
    }

    private Material CreateMaterial(Texture texture)
    {
        Material material;
        if (materialOverride != null)
        {
            material = new Material(materialOverride);
        }
        else
        {
            // Sprites/Default: sem luz, cor de vértice × textura, e já usado pelas partículas do projeto.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                if (!warnedShader)
                {
                    warnedShader = true;
                    Debug.LogWarning("[Ambience] - Shader 'Sprites/Default' não encontrado; atribua um Material Override no AmbienceFX");
                }

                return null;
            }

            material = new Material(shader);
        }

        material.name = "AmbienceFX";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.mainTexture = texture;
        return material;
    }

    // Ponto branco com borda macia (gotas, flocos, poeira).
    private static Texture2D SoftDotTexture()
    {
        if (softDotTexture != null) return softDotTexture;

        const int size = 32;
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        softDotTexture = BuildTexture("AmbienceSoftDot", size, pixels, TextureWrapMode.Clamp);
        return softDotTexture;
    }

    // Nuvem que se repete sem emenda: 3 oitavas de value noise periódico.
    private static Texture2D FogTexture()
    {
        if (fogTexture != null) return fogTexture;

        const int size = 128;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float n = ValueNoise(u, v, 4, 11) * 0.55f + ValueNoise(u, v, 8, 23) * 0.3f + ValueNoise(u, v, 16, 37) * 0.15f;
                float a = Mathf.InverseLerp(0.25f, 0.8f, n);
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        fogTexture = BuildTexture("AmbienceFog", size, pixels, TextureWrapMode.Repeat);
        return fogTexture;
    }

    private static Texture2D BuildTexture(string textureName, int size, Color32[] pixels, TextureWrapMode wrap)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = textureName,
            wrapMode = wrap,
            filterMode = FilterMode.Bilinear,

            // Sem DontUnloadUnusedAsset: enquanto o campo estático aponta para ela, fica viva; depois de um reload
            // de domínio no Editor ela é recolhida em vez de vazar.
            hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true); // sem cópia na CPU depois de enviar para a GPU
        return texture;
    }

    private static float ValueNoise(float u, float v, int cells, int seed)
    {
        float x = u * cells;
        float y = v * cells;
        int x0 = (int)x;
        int y0 = (int)y;
        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        x0 %= cells;
        y0 %= cells;
        int x1 = (x0 + 1) % cells;
        int y1 = (y0 + 1) % cells;

        float bottom = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx);
        float top = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx);
        return Mathf.Lerp(bottom, top, fy);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }

    private static Gradient FadeGradient()
    {
        if (fadeGradient != null) return fadeGradient;

        fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        return fadeGradient;
    }

    private static Gradient BlinkGradient()
    {
        if (blinkGradient != null) return blinkGradient;

        blinkGradient = new Gradient();
        blinkGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.2f, 0.4f),
                new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0.2f, 0.8f), new GradientAlphaKey(0f, 1f),
            });
        return blinkGradient;
    }

#if UNITY_EDITOR
    // ───────────────────────────── Pré-visualização no Editor (fora do Play Mode) ─────────────────────────────

    /// <summary>Só Editor: os efeitos existem agora (Play Mode ou pré-visualização ligada).</summary>
    public bool IsBuilt => built;

    /// <summary>Só Editor: cria os efeitos fora do Play Mode. Nada é salvo na cena.</summary>
    public void EditorPreviewStart()
    {
        if (!Application.isPlaying) Build();
    }

    /// <summary>Só Editor: remove os efeitos criados pela pré-visualização.</summary>
    public void EditorPreviewStop()
    {
        if (!Application.isPlaying) Teardown();
    }

    /// <summary>Só Editor: avança a pré-visualização (fora do Play Mode as partículas não andam sozinhas).</summary>
    public void EditorPreviewTick(float dt)
    {
        if (Application.isPlaying || !built) return;

        Tick(dt);
        for (int i = 0; i < emitters.Length; i++)
        {
            ParticleSystem ps = emitters[i].ps;
            if (ps != null) ps.Simulate(dt, false, false, false);
        }
    }
#endif
}
