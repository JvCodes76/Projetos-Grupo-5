using Roguelike.Events;
using Roguelike.Movement;
using UnityEngine;

/// <summary>
/// Dirige o Cyborg_Motor.controller (SPEC §10.2, DS-15) a partir do snapshot: um int "State" + transições Any State
/// de 0 s, sem triggers (acaba com o JumpTrigger pendurado, P12). "Speed" é a |vx| REAL (P17) e "RunSpeedMul" ajusta o
/// ritmo da corrida. A escolha do estado é o <see cref="AnimStateSelector"/> (puro).
/// </summary>
[DefaultExecutionOrder(41)]
public sealed class PlayerAnimatorDriver : MonoBehaviour
{
    private static readonly int StateHash = Animator.StringToHash("State");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int RunSpeedMulHash = Animator.StringToHash("RunSpeedMul");

    [SerializeField] private PlayerController controller;
    [SerializeField] private Animator animator;

    private long lastLandedTick = -10_000;
    private long lastAirJumpTick = -10_000;
    private int lastState = -1;

    /// <summary>Estado aplicado no último frame (overlay e testes).</summary>
    public AnimState CurrentState { get; private set; }

    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        EventBus<PlayerLanded>.Subscribe(HandleLanded);
        EventBus<PlayerJumped>.Subscribe(HandleJumped);
    }

    private void OnDisable()
    {
        EventBus<PlayerLanded>.Unsubscribe(HandleLanded);
        EventBus<PlayerJumped>.Unsubscribe(HandleJumped);
    }

    private void LateUpdate()
    {
        if (controller == null || controller.Motor == null || animator == null || animator.runtimeAnimatorController == null) return;

        PlayerSnapshot snap = controller.Snapshot;
        long tick = controller.CurrentTick;
        int sinceLanded = (int)System.Math.Min(tick - lastLandedTick, int.MaxValue);
        int sinceAirJump = (int)System.Math.Min(tick - lastAirJumpTick, int.MaxValue);

        AnimState state = AnimStateSelector.Select(snap, sinceLanded, sinceAirJump);
        CurrentState = state;
        if ((int)state != lastState)
        {
            animator.SetInteger(StateHash, (int)state);
            lastState = (int)state;
        }

        animator.SetFloat(SpeedHash, Mathf.Abs(snap.Velocity.x));
        animator.SetFloat(RunSpeedMulHash, AnimStateSelector.RunSpeedMultiplier(snap.Velocity.x, snap.MaxSpeed));

        // Morte: congela o frame atual (placeholder até existir arte de morte).
        animator.speed = state == AnimState.Dead ? 0f : 1f;
    }

    private void HandleLanded(PlayerLanded evt) => lastLandedTick = evt.Tick;

    private void HandleJumped(PlayerJumped evt)
    {
        if (evt.Kind == JumpKind.Air) lastAirJumpTick = evt.Tick;
    }
}
