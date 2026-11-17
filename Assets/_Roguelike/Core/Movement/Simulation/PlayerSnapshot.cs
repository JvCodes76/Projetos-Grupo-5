using UnityEngine;

namespace Roguelike.Movement
{
    /// <summary>
    /// Leitura do jogador para apresentação e câmera (SPEC §2.1): cópia do que as views precisam depois de um tick.
    /// As views nunca escrevem no núcleo; só leem isto e os eventos.
    /// </summary>
    public readonly struct PlayerSnapshot
    {
        public PlayerSnapshot(in MotorState s, in MovementStats st, Vector2 bodySize, float maxFall, long tick)
        {
            Tick = tick;
            Position = s.Position;
            PrevPosition = s.PrevPosition;
            Velocity = s.Velocity;
            State = s.State;
            Grounded = s.Grounded;
            Facing = s.Facing;
            WallSlideSide = s.WallSlideSide;
            AirJumpsLeft = s.AirJumpsLeft;
            MaxAirJumps = st.MaxAirJumps;
            DashesLeft = s.DashesLeft;
            MaxDashes = st.MaxDashes;
            MaxSpeed = st.MaxSpeed;
            MaxFall = maxFall;
            BodySize = bodySize;
            Teleported = s.Teleported;
            GrapplePhase = s.GrapplePhase;
            GrappleAnchor = s.GrappleAnchor;
            HookTip = s.GrapplePhase == GrapplePhase.Travel && s.HookTravelTotal > 0
                ? Vector2.Lerp(s.HookOrigin, s.GrappleAnchor, 1f - s.HookTravelTicksLeft / (float)s.HookTravelTotal)
                : s.GrappleAnchor;
            HasGrappleTarget = s.HasGrappleTarget;
            GrappleTarget = s.GrappleTarget;
            GrappleUnlocked = st.HasGrappleTool;
            GrappleUsesLeft = s.GrappleUsesLeft;
            MaxGrappleUses = st.MaxGrappleUses;
            GrappleAimVisible = st.HasGrappleTool && s.GrappleUsesLeft > 0
                && s.State == MotorStateId.Normal && s.GrapplePhase == GrapplePhase.None;
            GrappleOnCooldown = s.GrappleCooldownTicks > 0;
            Visible = s.State != MotorStateId.Respawning;
            GravityFlipped = s.GravityFlipTicksLeft > 0;
            GravityFlipsLeft = s.GravityFlipsLeft;
            MaxGravityFlips = st.MaxGravityFlips;
        }

        public long Tick { get; }
        /// <summary>Pés do jogador (pose de simulação).</summary>
        public Vector2 Position { get; }
        public Vector2 PrevPosition { get; }
        public Vector2 Velocity { get; }
        public MotorStateId State { get; }
        public bool Grounded { get; }
        public sbyte Facing { get; }
        public sbyte WallSlideSide { get; }
        public int AirJumpsLeft { get; }
        public int MaxAirJumps { get; }
        public int DashesLeft { get; }
        public int MaxDashes { get; }
        public float MaxSpeed { get; }
        public float MaxFall { get; }
        public Vector2 BodySize { get; }
        /// <summary>O último tick foi um teleporte (spawn, respawn): a câmera corta, a interpolação não arrasta.</summary>
        public bool Teleported { get; }
        public GrapplePhase GrapplePhase { get; }
        public Vector2 GrappleAnchor { get; }
        /// <summary>Posição da ponta do gancho (interpolada na viagem; a âncora no puxão).</summary>
        public Vector2 HookTip { get; }
        /// <summary>A mira acerta uma parede/alvo válido (o disparo prenderia em <see cref="GrappleTarget"/>).</summary>
        public bool HasGrappleTarget { get; }
        /// <summary>Ponto da mira: a âncora prevista, ou o fim do alcance na direção mirada quando não há alvo.</summary>
        public Vector2 GrappleTarget { get; }
        /// <summary>O jogador tem o gancho ou o lançador de teia.</summary>
        public bool GrappleUnlocked { get; }
        /// <summary>Usos do gancho/teia restantes nesta fase.</summary>
        public int GrappleUsesLeft { get; }
        public int MaxGrappleUses { get; }
        /// <summary>A mira deve aparecer: tem a ferramenta, sobra uso na fase e o gancho não está em curso.</summary>
        public bool GrappleAimVisible { get; }
        public bool GrappleOnCooldown { get; }
        /// <summary>O sprite deve aparecer (oculto durante o respawn).</summary>
        public bool Visible { get; }
        /// <summary>Gravidade invertida agora (o sprite fica de cabeça para baixo).</summary>
        public bool GravityFlipped { get; }
        public int GravityFlipsLeft { get; }
        public int MaxGravityFlips { get; }
        public bool IsWallSliding => WallSlideSide != 0;
        public Vector2 BodyCenter => Position + new Vector2(0f, BodySize.y * 0.5f);
    }
}
