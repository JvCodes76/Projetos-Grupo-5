using Roguelike.Movement;
using UnityEngine;

// Eventos do movimento (SPEC §9.1, PLANO §3.4 "Movimento"). Namespace Roguelike.Events como todo o catálogo (ADR-01):
// o catálogo depende do domínio Movement (enums), nunca o contrário. O núcleo (PlayerMotor) não chama o bus: grava os
// fatos em TickEvents e o PlayerController publica depois do tick, na ordem do MovementEventPublisher (SPEC §9.2).
// Todos carregam o tick em que o fato aconteceu.
namespace Roguelike.Events
{
    /// <summary>Um pulo foi aplicado (chão, coyote, aéreo ou cancelando o gancho).</summary>
    public readonly struct PlayerJumped : IEvent
    {
        public PlayerJumped(JumpKind kind, bool fromBuffer, Vector2 position, Vector2 velocity, long tick)
        {
            Kind = kind;
            FromBuffer = fromBuffer;
            Position = position;
            Velocity = velocity;
            Tick = tick;
        }

        public JumpKind Kind { get; }
        /// <summary>O aperto veio do buffer (pressionado antes do tick em que o pulo saiu).</summary>
        public bool FromBuffer { get; }
        /// <summary>Pés do jogador.</summary>
        public Vector2 Position { get; }
        /// <summary>Velocidade logo depois do pulo.</summary>
        public Vector2 Velocity { get; }
        public long Tick { get; }
    }

    /// <summary>Um wall jump foi aplicado.</summary>
    public readonly struct PlayerWallJumped : IEvent
    {
        public PlayerWallJumped(WallSide side, bool neutral, Vector2 position, long tick)
        {
            Side = side;
            Neutral = neutral;
            Position = position;
            Tick = tick;
        }

        /// <summary>Lado da parede de onde o jogador saiu.</summary>
        public WallSide Side { get; }
        /// <summary>Sem direção segurada (sem trava de input).</summary>
        public bool Neutral { get; }
        public Vector2 Position { get; }
        public long Tick { get; }
    }

    /// <summary>Primeiro tick no chão depois de estar no ar (contato real, RF-37).</summary>
    public readonly struct PlayerLanded : IEvent
    {
        public PlayerLanded(float impactSpeed, float airTime, Vector2 position, long tick)
        {
            ImpactSpeed = impactSpeed;
            AirTime = airTime;
            Position = position;
            Tick = tick;
        }

        /// <summary>Velocidade vertical no impacto (u/s, ≥ 0).</summary>
        public float ImpactSpeed { get; }
        /// <summary>Tempo no ar (s).</summary>
        public float AirTime { get; }
        public Vector2 Position { get; }
        public long Tick { get; }
    }

    /// <summary>O dash fixou a direção (fim do freeze).</summary>
    public readonly struct PlayerDashed : IEvent
    {
        public PlayerDashed(Vector2 direction, int chargesLeft, Vector2 position, long tick)
        {
            Direction = direction;
            ChargesLeft = chargesLeft;
            Position = position;
            Tick = tick;
        }

        /// <summary>Direção normalizada (8 direções).</summary>
        public Vector2 Direction { get; }
        public int ChargesLeft { get; }
        public Vector2 Position { get; }
        public long Tick { get; }
    }

    /// <summary>O jogador entrou ou saiu do deslize na parede.</summary>
    public readonly struct PlayerWallSlideChanged : IEvent
    {
        public PlayerWallSlideChanged(bool started, WallSide side, long tick)
        {
            Started = started;
            Side = side;
            Tick = tick;
        }

        public bool Started { get; }
        public WallSide Side { get; }
        public long Tick { get; }
    }

    /// <summary>Uma recarga aumentou as cargas de um recurso.</summary>
    public readonly struct PlayerAbilityRefilled : IEvent
    {
        public PlayerAbilityRefilled(MovementResource resource, int amount, long tick)
        {
            Resource = resource;
            Amount = amount;
            Tick = tick;
        }

        public MovementResource Resource { get; }
        /// <summary>Quantas cargas foram recuperadas.</summary>
        public int Amount { get; }
        public long Tick { get; }
    }

    /// <summary>Uma ação foi negada (DS-17): na hora (Locked/NoTarget) ou quando o aperto expirou do buffer.</summary>
    public readonly struct PlayerActionDenied : IEvent
    {
        public PlayerActionDenied(DeniedAction action, DenyReason reason, long tick)
        {
            Action = action;
            Reason = reason;
            Tick = tick;
        }

        public DeniedAction Action { get; }
        public DenyReason Reason { get; }
        public long Tick { get; }
    }

    /// <summary>O gancho foi disparado (a ponta começa a viajar).</summary>
    public readonly struct PlayerGrappleFired : IEvent
    {
        public PlayerGrappleFired(Vector2 target, long tick)
        {
            Target = target;
            Tick = tick;
        }

        public Vector2 Target { get; }
        public long Tick { get; }
    }

    /// <summary>A ponta do gancho prendeu: começa o puxão.</summary>
    public readonly struct PlayerGrappleAttached : IEvent
    {
        public PlayerGrappleAttached(Vector2 anchor, long tick)
        {
            Anchor = anchor;
            Tick = tick;
        }

        public Vector2 Anchor { get; }
        public long Tick { get; }
    }

    /// <summary>O gancho terminou (lançamento, cancelamento ou interrupção).</summary>
    public readonly struct PlayerGrappleReleased : IEvent
    {
        public PlayerGrappleReleased(Vector2 launchVelocity, GrappleReleaseReason reason, long tick)
        {
            LaunchVelocity = launchVelocity;
            Reason = reason;
            Tick = tick;
        }

        /// <summary>Velocidade de lançamento (zero quando não houve lançamento).</summary>
        public Vector2 LaunchVelocity { get; }
        public GrappleReleaseReason Reason { get; }
        public long Tick { get; }
    }

    /// <summary>O jogador saiu da fase por baixo ou entrou numa KillZone (Q3: não é morte; o LevelTimer não para).</summary>
    public readonly struct PlayerFellOut : IEvent
    {
        public PlayerFellOut(Vector2 position, Vector2 lastSafeGround, long tick)
        {
            Position = position;
            LastSafeGround = lastSafeGround;
            Tick = tick;
        }

        public Vector2 Position { get; }
        /// <summary>Para onde o respawn vai levar o jogador.</summary>
        public Vector2 LastSafeGround { get; }
        public long Tick { get; }
    }

    /// <summary>O controle foi devolvido depois de um respawn (queda ou "voltar ao spawn").</summary>
    public readonly struct PlayerRespawned : IEvent
    {
        public PlayerRespawned(Vector2 position, RespawnReason reason, long tick)
        {
            Position = position;
            Reason = reason;
            Tick = tick;
        }

        public Vector2 Position { get; }
        public RespawnReason Reason { get; }
        public long Tick { get; }
    }

    /// <summary>
    /// Contadores de telemetria do movimento na fase (PRD §11.2). Emitido pelo PlayerController junto com
    /// LevelGoalReached ou PlayerDied (ou no LevelTimeExpired). Ouvintes: telemetria (4.3).
    /// </summary>
    public readonly struct MovementStatsReported : IEvent
    {
        public MovementStatsReported(int jumpsFromBuffer, int bufferExpired, int eatenInputs, int coyoteJumps,
            int cornerCorrections, int actionsDenied, int fellOuts, long ticks)
        {
            JumpsFromBuffer = jumpsFromBuffer;
            BufferExpired = bufferExpired;
            EatenInputs = eatenInputs;
            CoyoteJumps = coyoteJumps;
            CornerCorrections = cornerCorrections;
            ActionsDenied = actionsDenied;
            FellOuts = fellOuts;
            Ticks = ticks;
        }

        public int JumpsFromBuffer { get; }
        /// <summary>Apertos de pulo que expiraram no buffer.</summary>
        public int BufferExpired { get; }
        /// <summary>Apertos que expiraram e, ≤ 2 ticks depois, o jogador passou a poder pular.</summary>
        public int EatenInputs { get; }
        public int CoyoteJumps { get; }
        public int CornerCorrections { get; }
        public int ActionsDenied { get; }
        public int FellOuts { get; }
        /// <summary>Ticks simulados na fase.</summary>
        public long Ticks { get; }
    }
}
