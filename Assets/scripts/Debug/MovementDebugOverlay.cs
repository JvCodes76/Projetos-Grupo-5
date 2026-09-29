using System.Text;
using Roguelike.Movement;
using Roguelike.Upgrades;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Overlay de tuning do movimento (SPEC §15, RF-68): F1 liga/desliga. Mostra tick, estado, pose, velocidade,
/// chão/coyote, buffers, hold, cargas, cooldowns, altura e distância do último pulo (pouso a pouso), consultas de
/// física no tick e freeze. Botões mexem no kit (flags, pulos aéreos, dashes) e ligam o MovementLog.
/// Só existe no Editor e em Development Build; aloca só quando visível.
/// </summary>
public sealed class MovementDebugOverlay : MonoBehaviour
{
    [SerializeField] private PlayerController controller;
    [SerializeField] private bool visible;

    private readonly StringBuilder sb = new StringBuilder(1024);
    private AbilityFlags kitFlags;
    private int kitAirJumps;
    private int kitDashes;
    private bool kitInitialized;

    // Medida do último pulo: do chão ao chão.
    private bool airborne;
    private Vector2 takeoff;
    private float peakY;
    private float lastJumpHeight;
    private float lastJumpDistance;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (!Debug.isDebugBuild) enabled = false; // só no Editor e em Development Build
    }

    private void OnEnable()
    {
        if (controller != null) controller.Ticked += HandleTicked;
    }

    private void OnDisable()
    {
        if (controller != null) controller.Ticked -= HandleTicked;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f1Key.wasPressedThisFrame) visible = !visible;
    }

    private void HandleTicked(PlayerController pc)
    {
        ref readonly MotorState s = ref pc.Motor.State;
        if (!airborne && !s.Grounded)
        {
            airborne = true;
            takeoff = s.Position;
            peakY = s.Position.y;
        }
        else if (airborne)
        {
            peakY = Mathf.Max(peakY, s.Position.y);
            if (s.Grounded)
            {
                airborne = false;
                lastJumpHeight = peakY - takeoff.y;
                lastJumpDistance = Mathf.Abs(s.Position.x - takeoff.x);
            }
        }
    }

    private void OnGUI()
    {
        if (!visible || controller == null || controller.Motor == null) return;

        PlayerMotor motor = controller.Motor;
        ref readonly MotorState s = ref motor.State;
        ref readonly MovementStats st = ref motor.Stats;

        sb.Clear();
        sb.Append("Tick ").Append(motor.CurrentTick).Append("   estado ").Append(s.State)
            .Append(s.WallSlideSide != 0 ? " (deslize)" : string.Empty).Append('\n');
        sb.Append("pos (").Append(s.Position.x.ToString("0.00")).Append("; ").Append(s.Position.y.ToString("0.00"))
            .Append(")   v (").Append(s.Velocity.x.ToString("0.00")).Append("; ").Append(s.Velocity.y.ToString("0.00")).Append(")\n");
        sb.Append("chão ").Append(s.Grounded).Append("   ticks no ar ").Append(s.TicksSinceGrounded >= 1_000_000 ? "∞" : s.TicksSinceGrounded.ToString())
            .Append("   coyote ").Append(s.CoyoteArmed && s.TicksSinceGrounded <= st.CoyoteTicks).Append('\n');
        sb.Append("buffers  pulo ").Append(Buffer(s.JumpBuffer)).Append("  dash ").Append(Buffer(s.DashBuffer))
            .Append("  gancho ").Append(Buffer(s.GrappleBuffer)).Append('\n');
        sb.Append("hold ").Append(s.VarJumpTicks).Append("   trava ").Append(s.ForceMoveXTicks)
            .Append("   queda máx ").Append(s.MaxFallCurrent.ToString("0.0")).Append('\n');
        sb.Append("aéreos ").Append(s.AirJumpsLeft).Append('/').Append(st.MaxAirJumps)
            .Append("   dashes ").Append(s.DashesLeft).Append('/').Append(st.MaxDashes)
            .Append("   cd dash ").Append(s.DashCooldownTicks).Append("  cd gancho ").Append(s.GrappleCooldownTicks).Append('\n');
        sb.Append("último pulo: altura ").Append(lastJumpHeight.ToString("0.00")).Append(" u, distância ")
            .Append(lastJumpDistance.ToString("0.00")).Append(" u\n");
        sb.Append("chão seguro (").Append(s.LastSafeGround.x.ToString("0.0")).Append("; ").Append(s.LastSafeGround.y.ToString("0.0"))
            .Append(")   quedas ").Append(s.FellOuts).Append("   quinas ").Append(s.CornerCorrections).Append('\n');
        sb.Append("freeze pendente ").Append(SimulationRunner.Exists ? SimulationRunner.Instance.Loop.PendingFreezeSteps : 0)
            .Append("   freeze ").Append(FeedbackSettings.FreezeEnabled ? "ligado" : "desligado").Append('\n');

        GUILayout.BeginArea(new Rect(10, 10, 460, 420), GUI.skin.box);
        GUILayout.Label(sb.ToString());

        if (!kitInitialized)
        {
            kitFlags = st.Flags;
            kitAirJumps = st.MaxAirJumps;
            kitDashes = st.MaxDashes;
            kitInitialized = true;
        }

        GUILayout.BeginHorizontal();
        bool changed = false;
        changed |= Toggle(ref kitFlags, AbilityFlags.WallJump, "Parede");
        changed |= Toggle(ref kitFlags, AbilityFlags.GrapplingHook, "Gancho");
        changed |= Toggle(ref kitFlags, AbilityFlags.DashRefillOnWallJump, "Recarga parede");
        changed |= Toggle(ref kitFlags, AbilityFlags.AirJumpRefillOnGrapple, "Âncora");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Aéreos " + kitAirJumps)) { kitAirJumps = (kitAirJumps + 1) % 3; changed = true; }
        if (GUILayout.Button("Dashes " + kitDashes)) { kitDashes = (kitDashes + 1) % 3; changed = true; }
        MovementLog.Enabled = GUILayout.Toggle(MovementLog.Enabled, "Log");
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

        if (changed)
        {
            controller.SetStatSource(new KitStatInput(controller.Profile, kitFlags, kitAirJumps, kitDashes));
        }
    }

    private static string Buffer(in BufferedPress press) => press.Active ? press.Age.ToString() : "-";

    private static bool Toggle(ref AbilityFlags flags, AbilityFlags flag, string label)
    {
        bool on = (flags & flag) != 0;
        bool next = GUILayout.Toggle(on, label);
        if (next == on) return false;
        flags = next ? flags | flag : flags & ~flag;
        return true;
    }
}
