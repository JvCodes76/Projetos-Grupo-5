using Roguelike.Upgrades;

namespace Roguelike.Movement
{
    /// <summary>
    /// Valores EFETIVOS do movimento, já com upgrades, tetos e tempos convertidos em ticks (SPEC §8.2).
    /// Montado só pelo <see cref="MovementStatsResolver"/>. Struct copiado por valor: o motor guarda a própria cópia e
    /// troca de uma vez no passo 3 do tick (QueueStats), então quem o recebe nunca vê um valor mudar pela metade.
    /// </summary>
    public struct MovementStats
    {
        // --- Stats (u, u/s, u/s²) ---
        public float MaxSpeed;
        public float GroundAccel;
        /// <summary>AirMultBase × AirControl.</summary>
        public float AirMult;
        public float OverspeedDecay;
        public float JumpHBoost;
        public float JumpHeight;
        /// <summary>Derivado de JumpHeight pelo JumpSolver (gravidade fixa, DS-07).</summary>
        public float JumpSpeed;
        /// <summary>Derivado de JumpHeight × AirJumpHeightRatio.</summary>
        public float AirJumpSpeed;
        public float WallSlideSpeed;
        public float GrappleRadius;
        public float GrappleLaunchSpeed;

        // --- Contagens ---
        public int MaxAirJumps;
        public int MaxDashes;
        public AbilityFlags Flags;

        // --- Janelas e durações (ticks) ---
        public int CoyoteTicks;
        public int GrappleCooldownTicks;
        public int VarJumpTicks;
        public int CeilingGraceTicks;
        public int JumpBufferTicks;
        public int DashBufferTicks;
        public int GrappleBufferTicks;
        public int RetentionTicks;
        public int WallCoyoteTicks;
        public int WallJumpForceTicks;
        public int DashTicks;
        public int DashCooldownTicks;
        public int DashRefillCooldownTicks;
        public int DashFreezeTicks;
        public int GrappleMaxTicks;
        public int GrappleStuckWindowTicks;
        public int RespawnDelayTicks;
        public int ReturnToSpawnHoldTicks;

        /// <summary>Frequência usada para converter os tempos (60 no jogo; 50 no teste de invariância da queda).</summary>
        public int TickRate;

        public bool Has(AbilityFlags flag)
        {
            return flag != AbilityFlags.None && (Flags & flag) == flag;
        }
    }
}
