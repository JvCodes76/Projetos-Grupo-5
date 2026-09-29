using System;
using Roguelike.Run;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>Fatos que aconteceram num tick (bits de <see cref="TickEvents"/>).</summary>
    [Flags]
    public enum MovementEventFlags : uint
    {
        None = 0,
        Jumped = 1 << 0,
        WallJumped = 1 << 1,
        Landed = 1 << 2,
        Dashed = 1 << 3,
        WallSlideStarted = 1 << 4,
        WallSlideEnded = 1 << 5,
        AirJumpRefilled = 1 << 6,
        DashRefilled = 1 << 7,
        DashDenied = 1 << 8,
        AirJumpDenied = 1 << 9,
        GrappleDenied = 1 << 10,
        GrappleFired = 1 << 11,
        GrappleAttached = 1 << 12,
        GrappleReleased = 1 << 13,
        FellOut = 1 << 14,
        Respawned = 1 << 15,
        Died = 1 << 16,
        Teleported = 1 << 17,
        CornerCorrected = 1 << 18,
    }

    /// <summary>
    /// Fatos do último tick (SPEC §9.2): bits + um payload por tipo. O núcleo não chama o EventBus; os testes leem
    /// PlayerMotor.Events direto e o PlayerController publica pelo MovementEventPublisher. Zerado no passo 1 do tick.
    /// </summary>
    public struct TickEvents
    {
        public long Tick;
        public MovementEventFlags Flags;

        public JumpKind JumpKind;
        public bool JumpFromBuffer;
        public Vector2 JumpPosition;
        public Vector2 JumpVelocity;

        public sbyte WallJumpSide;
        public bool WallJumpNeutral;
        public Vector2 WallJumpPosition;

        public float LandImpactSpeed;
        public float LandAirTime;
        public Vector2 LandPosition;

        public Vector2 DashDirection;
        public int DashChargesLeft;
        public Vector2 DashPosition;

        public sbyte WallSlideStartSide;
        public sbyte WallSlideEndSide;

        public int AirJumpRefillAmount;
        public int DashRefillAmount;

        public DenyReason DashDenyReason;
        public DenyReason AirJumpDenyReason;
        public DenyReason GrappleDenyReason;

        public Vector2 GrappleTarget;
        public Vector2 GrappleAnchor;
        public Vector2 GrappleLaunchVelocity;
        public GrappleReleaseReason GrappleReleaseReason;

        public Vector2 FellOutPosition;
        public Vector2 FellOutLastSafeGround;

        public Vector2 RespawnPosition;
        public RespawnReason RespawnReason;

        public DeathCause DeathCause;
        public Vector2 DeathPosition;

        public bool Has(MovementEventFlags flag) => (Flags & flag) != 0;

        public void Clear(long tick)
        {
            this = default;
            Tick = tick;
        }
    }
}
