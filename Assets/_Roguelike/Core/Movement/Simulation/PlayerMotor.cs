using System;
using Roguelike.Run;
using Roguelike.Simulation;
using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Núcleo do controlador de movimento do Cyborg (SPEC §2–§7): corpo cinemático próprio, resolução por eixo,
    /// máquina de estados e mecânicas, a um tick fixo. C# puro: recebe o mundo por <see cref="ICollisionWorld"/>, o
    /// input por <see cref="TickInput"/> e os stats por <see cref="MovementStats"/>; não lê tempo de frame, nem sorteia,
    /// nem mexe em componentes. Testável em EditMode sem cena.
    /// A ordem de operações de <see cref="Tick"/> é contrato (SPEC §6.3): testes e replays dependem dela.
    /// Comandos externos (Queue*) são aplicados no passo 3 do próximo tick, em ordem fixa.
    /// </summary>
    public sealed partial class PlayerMotor
    {
        private const int MaxGrappleCandidates = 16;

        private readonly MovementProfile p;
        private readonly ICollisionWorld world;
        private readonly int tickRate;
        private readonly float dt;
        private readonly Vector2[] grappleCandidates = new Vector2[MaxGrappleCandidates];

        private MovementStats st;
        private MotorState s;
        private TickEvents events;
        private long currentTick;
        private TickInput input;
        private sbyte moveX;
        private bool beginReturnToSpawn;

        // Comandos pendentes (passo 3).
        private bool pendingDie;
        private bool pendingDieEmits;
        private DeathCause pendingDieCause;
        private int pendingDisabled = -1;
        private bool pendingLevelReset;
        private Vector2 pendingResetSpawn;
        private float pendingResetKillPlane;
        private bool pendingTeleport;
        private Vector2 pendingTeleportFeet;
        private bool pendingTeleportAsRespawn;
        private RespawnReason pendingTeleportReason;
        private bool pendingStats;
        private MovementStats pendingStatsValue;

        public PlayerMotor(MovementProfile profile, ICollisionWorld world, in MovementStats stats, Vector2 spawnFeet,
            int tickRate = TickMath.DefaultTickRate)
        {
            p = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));

            this.tickRate = tickRate;
            dt = 1f / tickRate;
            st = stats;
            ResetForLevel(spawnFeet, float.NegativeInfinity);
            events.Clear(0);
        }

        /// <summary>Estado completo (hash, snapshot, overlay, testes).</summary>
        public ref readonly MotorState State => ref s;

        /// <summary>Fatos do último tick (SPEC §9).</summary>
        public ref readonly TickEvents Events => ref events;

        /// <summary>Stats efetivos em uso.</summary>
        public ref readonly MovementStats Stats => ref st;

        public MovementProfile Profile => p;

        public ICollisionWorld World => world;

        public int TickRate => tickRate;

        public float Dt => dt;

        /// <summary>Último tick simulado.</summary>
        public long CurrentTick => currentTick;

        /// <summary>Pedido de freeze feito no último tick (o PlayerController repassa ao SimulationLoop).</summary>
        public int FreezeRequestTicks { get; private set; }

        /// <summary>Morto, ou com a morte já pedida (verdadeiro na hora da chamada, SPEC §12).</summary>
        public bool IsDead => s.State == MotorStateId.Dead || pendingDie;

        /// <summary>Último input recebido (overlay e gravação).</summary>
        public TickInput LastInput => input;

        /// <summary>Centro da caixa do corpo (pose de simulação).</summary>
        public Vector2 BodyCenter => s.Position + new Vector2(0f, p.BodySize.y * 0.5f);

        /// <summary>Leitura para views e câmera.</summary>
        public PlayerSnapshot CreateSnapshot()
        {
            return new PlayerSnapshot(in s, in st, p.BodySize, s.MaxFallCurrent, currentTick);
        }

        /// <summary>Um tick de simulação (SPEC §6.3). <paramref name="tick"/> é o relógio de simulação.</summary>
        public void Tick(in TickInput tickInput, long tick)
        {
            // 1. Início do tick.
            s.PrevPosition = s.Position;
            events.Clear(tick);
            currentTick = tick;
            s.Teleported = false;
            FreezeRequestTicks = 0;

            // 2. Input do tick (já vem da fonte: sampler, gravação ou roteiro).
            input = tickInput;

            // 3. Comandos pendentes, em ordem fixa.
            ApplyPendingCommands();

            // 4. Timers e variáveis (não roda morto nem desabilitado).
            if (s.State != MotorStateId.Dead && s.State != MotorStateId.Disabled)
            {
                UpdateGroundedTimers();
                UpdateCountdowns();
                UpdateBuffers();
                UpdateInputDerived();
            }
            else
            {
                moveX = 0;
                beginReturnToSpawn = false;
            }

            // 5. Atualização do estado.
            switch (s.State)
            {
                case MotorStateId.Normal: s.State = NormalUpdate(); break;
                case MotorStateId.Dash: s.State = DashUpdate(); break;
                case MotorStateId.Grapple: s.State = GrappleUpdate(); break;
                case MotorStateId.Respawning: s.State = RespawnUpdate(); break;
                default: s.Velocity = Vector2.zero; break;
            }

            // 6. Teto global de velocidade por eixo.
            float cap = p.GlobalSpeedCap;
            s.Velocity.x = MathUtil.Clamp(s.Velocity.x, -cap, cap);
            s.Velocity.y = MathUtil.Clamp(s.Velocity.y, -cap, cap);

            // 7. Movimento com colisão (X antes de Y).
            if (IsSimulatedBody(s.State))
            {
                MoveX(s.Velocity.x * dt);
                MoveY(s.Velocity.y * dt);
            }

            // 8. Pós-movimento.
            PostMovement();

            // 9. Telemetria.
            s.TicksInLevel++;
            if (s.Teleported) events.Flags |= MovementEventFlags.Teleported;
        }

        // ───────────────────────────── Comandos ─────────────────────────────

        /// <summary>Morte (RF-41): idempotente; PlayerDied sai uma vez, no próximo tick.</summary>
        public void QueueDie(DeathCause cause)
        {
            if (s.State == MotorStateId.Dead || pendingDie) return;
            pendingDie = true;
            pendingDieEmits = true;
            pendingDieCause = cause;
        }

        /// <summary>
        /// Tempo esgotado (LevelTimeExpired): trava o corpo como a morte, mas SEM PlayerDied (ADR-10; adapta a DS-20).
        /// </summary>
        public void QueueTimeUp()
        {
            if (s.State == MotorStateId.Dead || pendingDie) return;
            pendingDie = true;
            pendingDieEmits = false;
            pendingDieCause = DeathCause.Unknown;
        }

        /// <summary>Liga/desliga o controle (DisableMovement, LevelGoalReached → Disabled, DS-20).</summary>
        public void QueueDisabled(bool disabled)
        {
            pendingDisabled = disabled ? 1 : 0;
        }

        /// <summary>Teleporte (spawn, "voltar ao spawn", transform movido por script legado, SPEC §5.6).</summary>
        public void QueueTeleport(Vector2 feet, bool asRespawn, RespawnReason reason)
        {
            pendingTeleport = true;
            pendingTeleportFeet = feet;
            pendingTeleportAsRespawn = asRespawn;
            pendingTeleportReason = reason;
        }

        /// <summary>Novos stats (PlayerStatsChanged): valem do próximo tick; ações em curso mantêm o capturado (RNF-07).</summary>
        public void QueueStats(in MovementStats stats)
        {
            pendingStats = true;
            pendingStatsValue = stats;
        }

        /// <summary>Fase nova: volta ao Normal (inclusive de Dead), no spawn, com tudo recarregado.</summary>
        public void QueueLevelReset(Vector2 spawnFeet, float killPlaneY)
        {
            pendingLevelReset = true;
            pendingResetSpawn = spawnFeet;
            pendingResetKillPlane = killPlaneY;
        }

        /// <summary>FNV-1a sobre o <see cref="MotorState"/> inteiro (RNF-02).</summary>
        public ulong ComputeHash()
        {
            return MotorStateHash.Compute(in s);
        }

        /// <summary>Só testes: põe o corpo numa pose/velocidade sem passar pelos comandos.</summary>
        internal void DebugSetBody(Vector2 feet, Vector2 velocity, bool grounded)
        {
            s.Position = feet;
            s.PrevPosition = feet;
            s.Velocity = velocity;
            s.Grounded = grounded;
            s.TicksSinceGrounded = grounded ? 0 : TickMath.Never;
            s.CoyoteArmed = grounded;
            s.AirStartTick = currentTick;
        }

        /// <summary>
        /// Só ferramentas e testes (analisador de alcançabilidade): reinicia o motor numa pose, sem passar pelos
        /// comandos, para reaproveitar a mesma instância em milhares de simulações.
        /// </summary>
        internal void DebugReset(Vector2 feet, float killPlaneY, Vector2 velocity, bool grounded, in MovementStats stats)
        {
            st = stats;
            pendingDie = false;
            pendingDisabled = -1;
            pendingLevelReset = false;
            pendingTeleport = false;
            pendingStats = false;
            currentTick = 0;
            ResetForLevel(feet, killPlaneY);
            s.Velocity = velocity;
            s.Grounded = grounded;
            s.CoyoteArmed = grounded;
            s.TicksSinceGrounded = grounded ? 0 : TickMath.Never;
            events.Clear(0);
        }

        /// <summary>Só testes: cargas de pulo aéreo e dash.</summary>
        internal void DebugSetCharges(int airJumps, int dashes)
        {
            s.AirJumpsLeft = airJumps;
            s.DashesLeft = dashes;
        }

        private void ApplyPendingCommands()
        {
            if (pendingDie)
            {
                pendingDie = false;
                ApplyDie(pendingDieCause, pendingDieEmits);
            }

            if (pendingDisabled >= 0)
            {
                bool disable = pendingDisabled == 1;
                pendingDisabled = -1;
                if (s.State != MotorStateId.Dead)
                {
                    if (disable) EnterDisabled();
                    else if (s.State == MotorStateId.Disabled) s.State = MotorStateId.Normal;
                }
            }

            if (pendingLevelReset)
            {
                pendingLevelReset = false;
                ResetForLevel(pendingResetSpawn, pendingResetKillPlane);
            }

            if (pendingTeleport)
            {
                pendingTeleport = false;
                ApplyTeleport(pendingTeleportFeet, pendingTeleportAsRespawn, pendingTeleportReason);
            }

            if (pendingStats)
            {
                pendingStats = false;
                st = pendingStatsValue;
                if (s.AirJumpsLeft > st.MaxAirJumps) s.AirJumpsLeft = st.MaxAirJumps;
                if (s.DashesLeft > st.MaxDashes) s.DashesLeft = st.MaxDashes;
            }
        }

        private static bool IsSimulatedBody(MotorStateId state)
        {
            return state == MotorStateId.Normal || state == MotorStateId.Dash || state == MotorStateId.Grapple;
        }
    }
}
