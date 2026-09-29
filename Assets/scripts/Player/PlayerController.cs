using System;
using Roguelike.Events;
using Roguelike.Movement;
using Roguelike.MovementPhysics;
using Roguelike.Run;
using Roguelike.Simulation;
using UnityEngine;

/// <summary>
/// Adaptador do <see cref="PlayerMotor"/> (SPEC §2.1, §3, §13): liga o núcleo ao Unity. A cada tick do
/// SimulationRunner lê o input, roda o motor, publica os eventos (ordem da §9.2), escreve a pose de simulação no
/// root, varre os triggers de tick (EndGoal) e repassa o pedido de freeze. No LateUpdate interpola o VisualRoot.
/// Não tem regra de gameplay. Stats: kit de dev até o primeiro PlayerStatsChanged (a run os reemite depois de
/// cada PlayerSpawned, ADR-16). O root fica nos pés, com escala 1 (DS-04).
/// </summary>
[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class PlayerController : MonoBehaviour, ITickable, IPlayerSimulationProbe
{
    [Header("Dados")]
    [Tooltip("Perfil de movimento (todas as constantes de feel).")]
    [SerializeField] private MovementProfile profile;
    [Tooltip("Kit usado até a run mandar PlayerStatsChanged (gym e Play direto). Vazio = kit base do perfil.")]
    [SerializeField] private MovementKit devKit;

    [Header("Hierarquia visual")]
    [Tooltip("Filho que recebe o offset de interpolação (SPEC §3.3).")]
    [SerializeField] private Transform visualRoot;

    [Header("Eventos")]
    [Tooltip("Publica em eventos C# locais (LocalMovementEvents) em vez do EventBus.")]
    [SerializeField] private bool useLocalEvents;

    private PlayerMotor motor;
    private Physics2DCollisionWorld world;
    private InputSampler sampler;
    private IMovementEvents events;
    private IInputSource inputOverride;
    private InputRecording recording;
    private IMovementStatInput statSource;
    private readonly TickTriggerScanner triggerScanner = new TickTriggerScanner();
    private Rigidbody2D body;
    private Vector2 lastWrittenPosition;
    private bool reportedThisLevel;

    public MovementProfile Profile => profile;

    public PlayerMotor Motor => motor;

    public long CurrentTick => motor != null ? motor.CurrentTick : 0;

    /// <summary>Leitura do último tick para views e câmera.</summary>
    public PlayerSnapshot Snapshot { get; private set; }

    /// <summary>Pés interpolados no render (o que a tela mostra).</summary>
    public Vector2 RenderPosition { get; private set; }

    /// <summary>Fonte de input real (overlay e gravação).</summary>
    public InputSampler Sampler => sampler;

    public int TickOrder => 0;

    public bool IsDead => motor != null && motor.IsDead;

    public bool IsGrappling => motor != null && motor.State.GrapplePhase != GrapplePhase.None;

    /// <summary>Disparado depois de cada tick (views que precisam do tick, overlay, gravação).</summary>
    public event Action<PlayerController> Ticked;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.None;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        if (profile == null)
        {
            Debug.LogWarning("[Movement] - MovementProfile não atribuído no PlayerController; usando os valores padrão.");
            profile = ScriptableObject.CreateInstance<MovementProfile>();
        }

        world = new Physics2DCollisionWorld();
        sampler = new InputSampler(profile.StickDeadzone);
        statSource = devKit != null ? devKit.CreateInput(profile) : new KitStatInput(profile);
        MovementStats stats = MovementStatsResolver.Resolve(profile, statSource);

        Vector2 spawn = transform.position;
        motor = new PlayerMotor(profile, world, stats, spawn);
        motor.QueueLevelReset(spawn, CurrentKillPlane());
        lastWrittenPosition = spawn;
        Snapshot = motor.CreateSnapshot();
        RenderPosition = spawn;

        if (useLocalEvents)
        {
            var local = GetComponent<LocalMovementEvents>();
            events = local != null ? local : gameObject.AddComponent<LocalMovementEvents>();
        }
        else
        {
            events = new EventBusMovementEvents();
        }
    }

    private void OnEnable()
    {
        SimulationRunner.Register(this);
        EventBus<PlayerStatsChanged>.Subscribe(HandleStatsChanged);
        EventBus<PlayerSpawned>.Subscribe(HandlePlayerSpawned);
        EventBus<LevelStarted>.Subscribe(HandleLevelStarted);
        EventBus<LevelTimeExpired>.Subscribe(HandleLevelTimeExpired);
        EventBus<LevelGoalReached>.Subscribe(HandleLevelGoalReached);
        MovementProfile.Changed += HandleProfileChanged;
        GameInput.Enable();
    }

    private void OnDisable()
    {
        SimulationRunner.Unregister(this);
        EventBus<PlayerStatsChanged>.Unsubscribe(HandleStatsChanged);
        EventBus<PlayerSpawned>.Unsubscribe(HandlePlayerSpawned);
        EventBus<LevelStarted>.Unsubscribe(HandleLevelStarted);
        EventBus<LevelTimeExpired>.Unsubscribe(HandleLevelTimeExpired);
        EventBus<LevelGoalReached>.Unsubscribe(HandleLevelGoalReached);
        MovementProfile.Changed -= HandleProfileChanged;
    }

    // ───────────────────────────── ITickable ─────────────────────────────

    public void OnFrameStart()
    {
        // Teleporte externo (SPEC §5.6): algum script moveu o transform (spawner legado, cutscene). Vira comando.
        Vector2 current = transform.position;
        if ((current - lastWrittenPosition).sqrMagnitude > 1e-6f)
        {
            MovementLog.Warn($"Teleporte externo detectado para ({current.x:0.00}, {current.y:0.00})");
            motor.QueueTeleport(current, false, RespawnReason.FellOut);
            lastWrittenPosition = current;
        }

        if (inputOverride == null) sampler.PushFrame(PlayerInputReader.Read());
    }

    public void Tick(in TickContext ctx)
    {
        TickInput input = inputOverride != null ? inputOverride.NextTick() : sampler.NextTick();
        recording?.Append(input);

        motor.Tick(input, ctx.Tick);
        ref readonly MotorState s = ref motor.State;

        // Pose de simulação no root (física e triggers veem esta pose; o render é interpolado no LateUpdate).
        transform.position = new Vector3(s.Position.x, s.Position.y, transform.position.z);
        lastWrittenPosition = s.Position;
        Snapshot = motor.CreateSnapshot();

        if (motor.FreezeRequestTicks > 0) SimulationRunner.RequestFreeze(motor.FreezeRequestTicks);

        MovementEventPublisher.Publish(motor.Events, events);
        if (motor.Events.Has(MovementEventFlags.Died)) ReportMovementStats();

        if (s.State == MotorStateId.Normal || s.State == MotorStateId.Dash || s.State == MotorStateId.Grapple)
        {
            triggerScanner.Scan(this, motor.BodyCenter, profile.BodySize, ctx.Tick);
        }

        Ticked?.Invoke(this);
    }

    public void OnResume()
    {
        sampler.Rearm(PlayerInputReader.HeldButtons());
    }

    private void LateUpdate()
    {
        ref readonly MotorState s = ref motor.State;
        Vector2 render = Vector2.LerpUnclamped(s.PrevPosition, s.Position, SimulationRunner.Alpha);
        RenderPosition = render;
        if (visualRoot != null) visualRoot.localPosition = render - s.Position;
    }

    // ───────────────────────────── Eventos da run ─────────────────────────────

    private void HandleStatsChanged(PlayerStatsChanged evt)
    {
        if (!evt.Stats.IsValid) return;
        statSource = new PlayerStatsMovementInput(evt.Stats);
        motor.QueueStats(MovementStatsResolver.Resolve(profile, statSource));
    }

    private void HandlePlayerSpawned(PlayerSpawned evt)
    {
        if (evt.Player != gameObject) return;
        ResetForLevel();
    }

    private void HandleLevelStarted(LevelStarted evt)
    {
        ResetForLevel();
    }

    private void HandleLevelTimeExpired(LevelTimeExpired evt)
    {
        // ADR-10: trava como a morte, sem PlayerDied.
        motor.QueueTimeUp();
        ReportMovementStats();
    }

    private void HandleLevelGoalReached(LevelGoalReached evt)
    {
        motor.QueueDisabled(true); // DS-20
        ReportMovementStats();
    }

    private void HandleProfileChanged(MovementProfile changed)
    {
        if (changed != profile) return;
        sampler.Deadzone = profile.StickDeadzone;
        motor.QueueStats(MovementStatsResolver.Resolve(profile, statSource));
    }

    private void ResetForLevel()
    {
        Vector2 spawn = transform.position;
        motor.QueueLevelReset(spawn, CurrentKillPlane());
        sampler.Rearm(PlayerInputReader.HeldButtons());
        triggerScanner.Clear();
        reportedThisLevel = false;
    }

    private void ReportMovementStats()
    {
        if (reportedThisLevel) return;
        reportedThisLevel = true;
        ref readonly MotorState s = ref motor.State;
        EventBus<MovementStatsReported>.Raise(new MovementStatsReported(s.JumpsFromBuffer, s.BufferExpired, s.EatenInputs,
            s.CoyoteJumps, s.CornerCorrections, s.ActionsDenied, s.FellOuts, s.TicksInLevel));
    }

    private static float CurrentKillPlane()
    {
        CameraBoundary boundary = CameraBoundary.Current;
        return boundary != null ? boundary.KillPlaneY : float.NegativeInfinity;
    }

    // ───────────────────────────── Comandos (fachada characterMovement) ─────────────────────────────

    public void Die(DeathCause cause) => motor.QueueDie(cause);

    public void DisableMovement() => motor.QueueDisabled(true);

    public void EnableMovement() => motor.QueueDisabled(false);

    /// <summary>"Voltar ao spawn" (fachada): teleporte com regras de respawn (Q11: o timer continua).</summary>
    public void ResetToSpawnPoint() => motor.QueueTeleport(motor.State.SpawnFeet, true, RespawnReason.ReturnToSpawn);

    /// <summary>Teleporte sem rastro de interpolação (spawner, ferramentas).</summary>
    public void Teleport(Vector2 feet)
    {
        transform.position = new Vector3(feet.x, feet.y, transform.position.z);
        lastWrittenPosition = feet;
        motor.QueueTeleport(feet, false, RespawnReason.FellOut);
    }

    /// <summary>Troca a fonte de stats (overlay de debug, gym). Vale no próximo tick.</summary>
    public void SetStatSource(IMovementStatInput source)
    {
        statSource = source ?? new KitStatInput(profile);
        motor.QueueStats(MovementStatsResolver.Resolve(profile, statSource));
    }

    public MovementKit DevKit => devKit;

    // ───────────────────────────── Gravação e testes ─────────────────────────────

    public void SetInputOverride(IInputSource source)
    {
        inputOverride = source;
        if (source == null) sampler.Rearm(PlayerInputReader.HeldButtons());
    }

    public void StartRecording(InputRecording target)
    {
        recording = target;
        if (recording != null)
        {
            recording.Clear();
            recording.StartFeet = motor.State.Position;
        }
    }

    public InputRecording StopRecording()
    {
        InputRecording result = recording;
        if (result != null) result.FinalHash = motor.ComputeHash();
        recording = null;
        return result;
    }
}
