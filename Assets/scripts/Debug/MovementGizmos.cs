using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Gizmos do movimento (SPEC §15), só com o objeto selecionado: caixa do corpo, caixas de parede (0,3 u), último
/// chão seguro, plano de queda, raio e alvo do gancho e a trilha dos últimos 120 ticks.
/// </summary>
public sealed class MovementGizmos : MonoBehaviour
{
    private const int TrailLength = 120;

    [SerializeField] private PlayerController controller;

    private readonly Vector2[] trail = new Vector2[TrailLength];
    private int trailCount;
    private int trailHead;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (controller != null) controller.Ticked += HandleTicked;
    }

    private void OnDisable()
    {
        if (controller != null) controller.Ticked -= HandleTicked;
    }

    private void HandleTicked(PlayerController pc)
    {
        trail[trailHead] = pc.Motor.State.Position;
        trailHead = (trailHead + 1) % TrailLength;
        if (trailCount < TrailLength) trailCount++;
    }

    private void OnDrawGizmosSelected()
    {
        if (controller == null || controller.Motor == null) return;

        PlayerMotor motor = controller.Motor;
        ref readonly MotorState s = ref motor.State;
        MovementProfile p = motor.Profile;
        Vector2 center = motor.BodyCenter;

        Gizmos.color = s.Grounded ? Color.green : Color.cyan;
        Gizmos.DrawWireCube(center, p.BodySize);

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        var wallBox = new Vector2(p.BodySize.x - 2f * p.Skin, p.BodySize.y - 2f * p.WallCheckInset);
        Gizmos.DrawWireCube(center + new Vector2(p.WallJumpDistance, 0f), wallBox);
        Gizmos.DrawWireCube(center - new Vector2(p.WallJumpDistance, 0f), wallBox);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(s.LastSafeGround, 0.15f);

        if (!float.IsNegativeInfinity(s.KillPlaneY))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(s.Position.x - 30f, s.KillPlaneY), new Vector3(s.Position.x + 30f, s.KillPlaneY));
        }

        if (motor.Stats.Has(Roguelike.Upgrades.AbilityFlags.GrapplingHook))
        {
            Gizmos.color = new Color(0.3f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(center, motor.Stats.GrappleRadius);
            if (s.HasGrappleTarget)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(center, s.GrappleTarget);
            }
        }

        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        for (int i = 1; i < trailCount; i++)
        {
            Vector2 a = trail[(trailHead - i - 1 + TrailLength) % TrailLength];
            Vector2 b = trail[(trailHead - i + TrailLength) % TrailLength];
            Gizmos.DrawLine(a, b);
        }
    }
}
