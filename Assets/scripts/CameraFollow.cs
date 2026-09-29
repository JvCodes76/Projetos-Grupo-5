using Roguelike.Cameras;
using Roguelike.Events;
using Roguelike.Movement;
using Roguelike.Simulation;
using UnityEngine;

/// <summary>
/// Câmera do jogo (SPEC §11, Q7, DS-14), reescrita no mesmo arquivo/GUID: a Main Camera DDOL do MainMenu a
/// referencia. A lógica está no <see cref="CameraSolver"/> (puro): esta classe é ITickable (TickOrder 100, depois do
/// jogador no mesmo tick), só interpola no render, soma o shake e aplica. Parâmetros no CameraProfile; sem
/// referência, cria um com os padrões e avisa. Alvo legado (sem PlayerController) segue o transform com a mesma
/// suavização e sem look-ahead (removido na 4.1).
/// </summary>
[DefaultExecutionOrder(50)]
public class CameraFollow : MonoBehaviour, ITickable
{
    public const int Order = 100;

    [Header("Dados")]
    [SerializeField] private CameraProfile profile;
    [Tooltip("Shake do dash e da morte (amplitude, duração, frequência). Vazio = padrões.")]
    [SerializeField] private FeedbackTuning feedbackTuning;

    [Header("Alvo")]
    [SerializeField] private Transform target;

    private Camera cam;
    private CameraSolver solver;
    private PlayerController player;
    private ShakeModel shake;
    private bool pendingSnap = true;

    public CameraSolver Solver => solver;

    public int TickOrder => Order;

    private void Awake()
    {
        // Garante que apenas uma câmera exista (a do MainMenu é DDOL).
        CameraFollow[] existing = FindObjectsByType<CameraFollow>(FindObjectsSortMode.None);
        if (existing.Length > 1)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);

        if (profile == null)
        {
            Debug.LogWarning("[CameraFollow] - CameraProfile não atribuído; usando os valores padrão.");
            profile = ScriptableObject.CreateInstance<CameraProfile>();
        }

        if (feedbackTuning == null) feedbackTuning = ScriptableObject.CreateInstance<FeedbackTuning>();

        cam = GetComponent<Camera>();
        solver = new CameraSolver(profile);
        ApplyAspect();
        SetTarget(target);
    }

    private void OnEnable()
    {
        SimulationRunner.Register(this);
        EventBus<PlayerSpawned>.Subscribe(HandlePlayerSpawned);
        EventBus<PlayerDashed>.Subscribe(HandlePlayerDashed);
        EventBus<PlayerDied>.Subscribe(HandlePlayerDied);
        EventBus<PlayerRespawned>.Subscribe(HandlePlayerRespawned);
    }

    private void OnDisable()
    {
        SimulationRunner.Unregister(this);
        EventBus<PlayerSpawned>.Unsubscribe(HandlePlayerSpawned);
        EventBus<PlayerDashed>.Unsubscribe(HandlePlayerDashed);
        EventBus<PlayerDied>.Unsubscribe(HandlePlayerDied);
        EventBus<PlayerRespawned>.Unsubscribe(HandlePlayerRespawned);
    }

    private void Start()
    {
        if (Camera.main != null) Camera.main.gameObject.tag = "MainCamera";
    }

    /// <summary>Troca os dados (câmera criada em código, ex.: DevPlayBootstrap).</summary>
    public void Configure(CameraProfile cameraProfile, FeedbackTuning tuning)
    {
        if (cameraProfile != null)
        {
            profile = cameraProfile;
            solver = new CameraSolver(profile);
            ApplyAspect();
            pendingSnap = true;
        }

        if (tuning != null) feedbackTuning = tuning;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        player = target != null ? target.GetComponent<PlayerController>() : null;
        pendingSnap = true;
    }

    // ───────────────────────────── Tick ─────────────────────────────

    public void OnFrameStart()
    {
    }

    public void OnResume()
    {
    }

    public void Tick(in TickContext ctx)
    {
        if (solver == null || target == null) return;

        CameraInput input = BuildInput();
        CameraBounds bounds = CurrentBounds();
        if (pendingSnap)
        {
            solver.Snap(input, bounds);
            pendingSnap = false;
            return;
        }

        solver.Tick(input, bounds, ctx.Dt);
    }

    private void LateUpdate()
    {
        if (solver == null) return;
        ApplyAspect();
        if (target == null) return;

        if (pendingSnap)
        {
            solver.Snap(BuildInput(), CurrentBounds());
            pendingSnap = false;
        }

        Vector2 pos = Vector2.LerpUnclamped(solver.PreviousPosition, solver.Position, SimulationRunner.Alpha);
        pos += shake.Update(Time.deltaTime);
        transform.position = new Vector3(pos.x, pos.y, profile.CameraZ);
    }

    private CameraInput BuildInput()
    {
        if (player != null)
        {
            PlayerSnapshot snap = player.Snapshot;
            return CameraInput.FromSnapshot(snap);
        }

        // Alvo legado: segue o ponto sem look-ahead nem platform snapping.
        return CameraInput.ForLegacyTarget(target.position, false);
    }

    private static CameraBounds CurrentBounds()
    {
        CameraBoundary boundary = CameraBoundary.Current;
        return boundary != null ? boundary.ToBounds() : CameraBounds.None;
    }

    private void ApplyAspect()
    {
        if (solver == null || cam == null || !cam.orthographic) return;
        float size = solver.SetAspect(cam.aspect);
        if (!Mathf.Approximately(cam.orthographicSize, size)) cam.orthographicSize = size;
    }

    // ───────────────────────────── Eventos ─────────────────────────────

    private void HandlePlayerSpawned(PlayerSpawned evt)
    {
        if (evt.Player == null) return;
        SetTarget(evt.Player.transform);
        Debug.Log("[CameraFollow] - Câmera recebeu referência do jogador via evento");
    }

    private void HandlePlayerRespawned(PlayerRespawned evt)
    {
        pendingSnap = true; // corte (RF-42)
    }

    private void HandlePlayerDashed(PlayerDashed evt)
    {
        shake.Start(evt.Direction, feedbackTuning.DashShakeDuration, feedbackTuning.ShakeAmplitudePixels,
            FeedbackSettings.ShakeScale, feedbackTuning.ShakeFrequency);
    }

    private void HandlePlayerDied(PlayerDied evt)
    {
        shake.Start(new Vector2(1f, 1f), feedbackTuning.DeathShakeDuration, feedbackTuning.ShakeAmplitudePixels,
            FeedbackSettings.ShakeScale, feedbackTuning.ShakeFrequency);
    }
}
