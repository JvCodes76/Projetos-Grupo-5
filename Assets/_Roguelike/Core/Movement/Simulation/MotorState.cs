using Roguelike.Run;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>Aperto guardado no buffer (SPEC §4.3). Idade em ticks; não envelhece no freeze nem durante o Dash.</summary>
    public struct BufferedPress
    {
        public bool Active;
        public int Age;
        public long PressTick;
    }

    /// <summary>
    /// Estado completo do controlador (SPEC §2.3): tudo que o próximo tick precisa. Escrito só pelo PlayerMotor;
    /// exposto como ref readonly para snapshot, hash (RNF-02), overlay e testes. Nenhum campo depende do framerate.
    /// Posições são dos PÉS do jogador (centro da base da caixa).
    /// </summary>
    public struct MotorState
    {
        // --- Corpo ---
        public Vector2 Position;
        public Vector2 PrevPosition;
        public Vector2 Velocity;
        public MotorStateId State;
        public bool Grounded;
        public sbyte Facing;
        public bool Teleported;

        // --- Chão, coyote, pulo ---
        public int TicksSinceGrounded;
        public bool CoyoteArmed;
        public long AirStartTick;
        public int VarJumpTicks;
        public float VarJumpSpeed;
        public long JumpStartTick;
        public float MaxFallCurrent;
        public float LandingImpact;
        public int AirJumpsLeft;

        // --- Trava de input do wall jump ---
        public sbyte ForceMoveX;
        public int ForceMoveXTicks;

        // --- Parede ---
        public sbyte WallSlideSide;
        public sbyte LastWallSlideSide;
        public int TicksSinceWallSlide;

        // --- Retenção de velocidade (RF-04) ---
        public float RetainedVx;
        public int RetentionTicks;

        // --- Buffers ---
        public BufferedPress JumpBuffer;
        public BufferedPress DashBuffer;
        public BufferedPress GrappleBuffer;
        public int EatenWatchTicks;

        // --- Dash ---
        public int DashesLeft;
        public int DashCooldownTicks;
        public int DashRefillCooldownTicks;
        public int DashTicksLeft;
        public DashPhase DashPhase;
        public Vector2 DashDir;
        public Vector2 BeforeDashVelocity;

        // --- Gancho ---
        public GrapplePhase GrapplePhase;
        public Vector2 GrappleAnchor;
        public int HookTravelTicksLeft;
        public int HookTravelTotal;
        public Vector2 HookOrigin;
        public int GrappleCooldownTicks;
        public int GrappleTicks;
        public float GrappleLaunchSpeedCaptured;
        public Vector2 LastPullDir;
        public float GrappleStuckRefDistance;
        public int GrappleStuckTicks;
        public bool HasGrappleTarget;
        public Vector2 GrappleTarget;

        // --- Ciclo de vida ---
        public Vector2 SpawnFeet;
        public float KillPlaneY;
        public Vector2 LastSafeGround;
        public int SafeGroundStableTicks;
        public int RespawnTicksLeft;
        public Vector2 RespawnTarget;
        public RespawnReason RespawnReason;
        public int RestartHeldTicks;
        public bool DiedEmitted;
        public DeathCause DeathCause;

        // --- Telemetria (MovementStatsReported) ---
        public int JumpsFromBuffer;
        public int BufferExpired;
        public int EatenInputs;
        public int CoyoteJumps;
        public int CornerCorrections;
        public int ActionsDenied;
        public int FellOuts;
        public long TicksInLevel;
    }
}
